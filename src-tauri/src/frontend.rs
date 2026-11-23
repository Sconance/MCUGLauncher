//! 第三方前端的加载与宿主信息注入。
//!
//! 内核只认一个约定：一个目录 + `frontend.json`。目录通过环境变量
//! `MCUGL_FRONTEND` 指定；命中时窗口地址换成 `mcugl://` 自定义协议，由本模块
//! 直接从该目录读文件（不打包、不重编译 Rust）。
//!
//! `frontend.json` 的最小形状：
//!
//! ```json
//! { "name": "我的前端", "version": "1.0.0", "kernelApi": "^1.0", "entry": "index.html" }
//! ```
//!
//! 详见 `docs/KERNEL_API_V1.md`。

use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};
use tauri::http::{header, Request, Response, StatusCode};

/// 内核 API 的版本。破坏性改动时升主版本，并在 `frontend.json` 的
/// `kernelApi` 里用 semver 范围声明兼容性。
pub const KERNEL_API_VERSION: &str = "1.0.0";

/// 协议标识，供 `window.__MCUGL__.protocol` 与文档引用。
pub const KERNEL_PROTOCOL: &str = "mcugl.kernel/v1";

/// 显式指定前端目录的环境变量。
pub const FRONTEND_ENV: &str = "MCUGL_FRONTEND";

/// 自定义协议名。Windows/Android 上实际以 `http://mcugl.localhost/...` 提供。
pub const FRONTEND_SCHEME: &str = "mcugl";

/// `frontend.json` 的 DTO。
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FrontendManifest {
    pub name: String,
    pub version: String,
    /// 前端要求的内核 API 范围，例如 `^1.0`、`>=1.0, <2`。
    pub kernel_api: String,
    /// 入口文件，默认 `index.html`。
    #[serde(default)]
    pub entry: Option<String>,
    /// 反向域名标识，安装 `.mcuglf` 包时必填；目录模式下可选。
    #[serde(default)]
    pub identifier: Option<String>,
    #[serde(default)]
    pub author: Option<String>,
    #[serde(default)]
    pub homepage: Option<String>,
    #[serde(default)]
    pub description: Option<String>,
    /// 图标相对路径（可选，供宿主展示）。
    #[serde(default)]
    pub icon: Option<String>,
}

/// 已校验通过的前端。
#[derive(Debug, Clone)]
pub struct Frontend {
    pub dir: PathBuf,
    pub manifest: FrontendManifest,
    pub entry: String,
    /// 加载方式：`directory` = `MCUGL_FRONTEND` 指向目录；`package` = 已安装的 `.mcuglf`。
    pub mode: &'static str,
}

/// 注入给页面的 `window.__MCUGL__` 形状（序列化为 JSON）。
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FrontendInfo {
    pub mode: &'static str,
    pub name: String,
    pub version: String,
    pub entry: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub identifier: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub author: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub icon: Option<String>,
}

/// 由 setup 放进 Tauri 托管状态。协议处理器和 `kernel_manifest` 都读同一个槽位，
/// 因此「重启前端」可以在不重启进程的情况下换掉正在服务的目录。
pub struct FrontendState(pub std::sync::Arc<std::sync::Mutex<Option<Frontend>>>);

impl FrontendState {
    pub fn current(&self) -> Option<Frontend> {
        self.0.lock().ok().and_then(|slot| slot.clone())
    }

    pub fn set(&self, frontend: Option<Frontend>) {
        if let Ok(mut slot) = self.0.lock() {
            *slot = frontend;
        }
    }
}

impl Frontend {
    pub fn info(&self) -> FrontendInfo {
        FrontendInfo {
            mode: self.mode,
            name: self.manifest.name.clone(),
            version: self.manifest.version.clone(),
            entry: self.entry.clone(),
            identifier: self.manifest.identifier.clone(),
            author: self.manifest.author.clone(),
            icon: self.manifest.icon.clone(),
        }
    }

    /// 窗口地址。Windows/Android 上自定义协议走 `http://{scheme}.localhost`，
    /// 其他平台走 `{scheme}://localhost`，与 tauri::protocol::origin 保持一致。
    pub fn url(&self) -> Result<tauri::Url, tauri::Error> {
        let origin = if cfg!(any(windows, target_os = "android")) {
            format!("http://{FRONTEND_SCHEME}.localhost")
        } else {
            format!("{FRONTEND_SCHEME}://localhost")
        };
        tauri::Url::parse(&format!("{origin}/{}", self.entry)).map_err(tauri::Error::InvalidUrl)
    }

    /// 页面加载前注入的引导脚本：让前端不调用命令也能确认自己跑在内核里。
    pub fn initialization_script(&self) -> String {
        let payload = serde_json::json!({
            "apiVersion": KERNEL_API_VERSION,
            "protocol": KERNEL_PROTOCOL,
            "frontend": self.info(),
        });
        // JSON 作为对象字面量嵌入，`</script>` 之类不可能出现（本脚本不经 HTML 解析）。
        format!("window.__MCUGL__ = {payload};")
    }
}

/// 读 `MCUGL_FRONTEND`；未设置或空字符串返回 `None`。
pub fn env_frontend() -> Option<PathBuf> {
    let raw = std::env::var_os(FRONTEND_ENV)?;
    if raw.is_empty() {
        return None;
    }
    let path = PathBuf::from(raw);
    Some(if path.is_absolute() {
        path
    } else {
        std::env::current_dir().ok()?.join(path)
    })
}

/// `MCUGL_FRONTEND` 指向的是 `.mcuglf`（或 `.zip`）包时返回包路径。
pub fn env_package() -> Option<PathBuf> {
    env_frontend().filter(|path| path.is_file())
}

/// 解析 `MCUGL_FRONTEND`。未设置时返回 `Ok(None)`（用打包进二进制的官方前端）；
/// 设置但校验失败时返回错误，由启动流程弹窗提示，而不是静默退回官方前端。
///
/// 路径既可以是目录（开发中直接改源码），也可以是 `.mcuglf` / `.zip` 包：
/// 包会被解压到 `<数据目录>/frontends/.dev/` 下，方便验收打包产物。
pub fn resolve_from_env(data_dir: &Path) -> Result<Option<Frontend>, String> {
    let Some(path) = env_frontend() else {
        return Ok(None);
    };
    if path.is_file() {
        return crate::frontends::resolve_dev_package(data_dir, &path).map(Some);
    }
    resolve_dir(&path).map(Some)
}

/// 校验一个前端目录，返回可直接挂窗口的 `Frontend`。
pub fn resolve_dir(dir: &Path) -> Result<Frontend, String> {
    let dir = dir
        .canonicalize()
        .map_err(|error| format!("前端目录不可访问（{}）：{error}", dir.display()))?;
    if !dir.is_dir() {
        return Err(format!("前端路径不是目录：{}", dir.display()));
    }

    let manifest_path = dir.join("frontend.json");
    let text = std::fs::read_to_string(&manifest_path).map_err(|error| {
        format!(
            "读取 {} 失败：{error}（第三方前端目录根部必须提供 frontend.json）",
            manifest_path.display()
        )
    })?;
    let manifest: FrontendManifest = serde_json::from_str(&text).map_err(|error| {
        format!(
            "解析 {} 失败：{error}（需要 {{name, version, kernelApi}} 三个字符串字段）",
            manifest_path.display()
        )
    })?;

    if manifest.name.trim().is_empty() {
        return Err("frontend.json 的 name 不能为空".into());
    }
    if manifest.version.trim().is_empty() {
        return Err("frontend.json 的 version 不能为空".into());
    }
    check_kernel_api(&manifest.kernel_api, &manifest_path)?;

    let entry = manifest
        .entry
        .clone()
        .unwrap_or_else(|| "index.html".to_string());
    if !is_safe_relative(&entry) {
        return Err(format!(
            "frontend.json 的 entry 必须是目录内的相对路径，且不能包含 `..`：{entry}"
        ));
    }
    if !dir.join(&entry).is_file() {
        return Err(format!(
            "入口文件不存在：{}",
            dir.join(&entry).display()
        ));
    }

    Ok(Frontend {
        dir,
        manifest,
        entry,
        mode: "directory",
    })
}

fn check_kernel_api(range: &str, manifest_path: &Path) -> Result<(), String> {
    let requirement = semver::VersionReq::parse(range).map_err(|error| {
        format!(
            "{} 的 kernelApi 不是合法的 semver 范围（{range}）：{error}",
            manifest_path.display()
        )
    })?;
    let version = semver::Version::parse(KERNEL_API_VERSION)
        .expect("KERNEL_API_VERSION 必须是合法 semver");
    if !requirement.matches(&version) {
        return Err(format!(
            "该前端要求内核 API {range}，当前内核是 {KERNEL_API_VERSION}；请升级前端或换用兼容的内核版本"
        ));
    }
    Ok(())
}

/// 只接受目录内的相对路径：没有盘符、没有前导 `/`、没有 `..`、没有 `\`。
fn is_safe_relative(path: &str) -> bool {
    if path.is_empty() || path.starts_with('/') || path.contains('\\') || path.contains(':') {
        return false;
    }
    path.split('/')
        .all(|segment| !segment.is_empty() && segment != "." && segment != "..")
}

/// 自定义协议处理器：把请求路径映射到前端目录里的文件。
///
/// - 找不到且路径没有扩展名时回退到入口文件（SPA 路由）；
/// - 任何越出前端目录的路径（`..`、符号链接指向外部）都按 404 处理。
pub fn respond(frontend: Option<&Frontend>, request: &Request<Vec<u8>>) -> Response<Vec<u8>> {
    let Some(frontend) = frontend else {
        return text(StatusCode::NOT_FOUND, "MCUGL_FRONTEND 未配置");
    };
    let Some(path) = request.uri().path_and_query().map(|pq| pq.path()) else {
        return text(StatusCode::BAD_REQUEST, "无效请求");
    };
    let Some(relative) = decode_path(path) else {
        return text(StatusCode::BAD_REQUEST, "路径编码无效");
    };
    // URL 路径以 `/` 开头，目录内相对路径不带。
    let relative = relative.trim_start_matches('/');

    // 根路径直接给入口。
    if relative.is_empty() {
        return serve(frontend, &frontend.entry);
    }
    if !is_safe_relative(relative) {
        return text(StatusCode::NOT_FOUND, "404 Not Found");
    }

    match resolve_file(frontend, relative) {
        Some(file) => read_file(&file),
        None => {
            let has_extension = relative
                .rsplit('/')
                .next()
                .map(|name| name.contains('.'))
                .unwrap_or(false);
            if has_extension {
                text(StatusCode::NOT_FOUND, "404 Not Found")
            } else {
                // SPA 路由回退到入口文件。
                serve(frontend, &frontend.entry)
            }
        }
    }
}

fn serve(frontend: &Frontend, relative: &str) -> Response<Vec<u8>> {
    match resolve_file(frontend, relative) {
        Some(file) => read_file(&file),
        None => text(StatusCode::NOT_FOUND, "入口文件不存在"),
    }
}

/// 解析到目录内真实存在的普通文件；不接受逃逸出目录的符号链接。
fn resolve_file(frontend: &Frontend, relative: &str) -> Option<PathBuf> {
    let mut path = frontend.dir.clone();
    for segment in relative.split('/') {
        path.push(segment);
    }
    let canonical = path.canonicalize().ok()?;
    if !canonical.starts_with(&frontend.dir) || !canonical.is_file() {
        return None;
    }
    Some(canonical)
}

fn read_file(path: &Path) -> Response<Vec<u8>> {
    match std::fs::read(path) {
        Ok(bytes) => {
            let mut response = Response::new(bytes);
            let mime = mime_for(path);
            response.headers_mut().insert(
                header::CONTENT_TYPE,
                header::HeaderValue::from_static(mime),
            );
            // 开发期前端经常改文件，不做缓存，避免作者改了看不到效果。
            response.headers_mut().insert(
                header::CACHE_CONTROL,
                header::HeaderValue::from_static("no-store"),
            );
            response
        }
        Err(error) => text(
            StatusCode::INTERNAL_SERVER_ERROR,
            &format!("读取 {} 失败：{error}", path.display()),
        ),
    }
}

fn text(status: StatusCode, body: &str) -> Response<Vec<u8>> {
    let mut response = Response::new(body.as_bytes().to_vec());
    *response.status_mut() = status;
    response.headers_mut().insert(
        header::CONTENT_TYPE,
        header::HeaderValue::from_static("text/plain; charset=utf-8"),
    );
    response
}

fn mime_for(path: &Path) -> &'static str {
    let extension = path
        .extension()
        .and_then(|value| value.to_str())
        .unwrap_or("")
        .to_ascii_lowercase();
    match extension.as_str() {
        "html" | "htm" => "text/html; charset=utf-8",
        "js" | "mjs" => "text/javascript; charset=utf-8",
        "css" => "text/css; charset=utf-8",
        "json" | "map" => "application/json",
        "svg" => "image/svg+xml",
        "png" => "image/png",
        "jpg" | "jpeg" => "image/jpeg",
        "gif" => "image/gif",
        "webp" => "image/webp",
        "ico" => "image/x-icon",
        "woff" => "font/woff",
        "woff2" => "font/woff2",
        "ttf" => "font/ttf",
        "wasm" => "application/wasm",
        "txt" => "text/plain; charset=utf-8",
        "webmanifest" => "application/manifest+json",
        _ => "application/octet-stream",
    }
}

/// 百分号解码（请求路径里的空格、中文等）；非法编码返回 None。
fn decode_path(path: &str) -> Option<String> {
    let bytes = path.as_bytes();
    let mut out = Vec::with_capacity(bytes.len());
    let mut index = 0;
    while index < bytes.len() {
        match bytes[index] {
            b'%' => {
                let hex = bytes.get(index + 1..index + 3)?;
                let text = std::str::from_utf8(hex).ok()?;
                out.push(u8::from_str_radix(text, 16).ok()?);
                index += 3;
            }
            byte => {
                out.push(byte);
                index += 1;
            }
        }
    }
    String::from_utf8(out).ok()
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    fn fixture(manifest: &str) -> tempfile::TempDir {
        let dir = tempfile::tempdir().unwrap();
        let mut file = std::fs::File::create(dir.path().join("frontend.json")).unwrap();
        file.write_all(manifest.as_bytes()).unwrap();
        std::fs::write(dir.path().join("index.html"), b"<html>ok</html>").unwrap();
        std::fs::create_dir_all(dir.path().join("assets")).unwrap();
        std::fs::write(dir.path().join("assets/app.js"), b"console.log(1)").unwrap();
        dir
    }

    fn request(path: &str) -> Request<Vec<u8>> {
        Request::builder()
            .uri(format!("http://mcugl.localhost{path}"))
            .body(Vec::new())
            .unwrap()
    }

    #[test]
    fn resolves_a_valid_directory() {
        let dir = fixture(r#"{"name":"Demo","version":"1.0.0","kernelApi":"^1.0"}"#);
        let frontend = resolve_dir(dir.path()).unwrap();
        assert_eq!(frontend.entry, "index.html");
        assert_eq!(frontend.manifest.name, "Demo");
        assert!(frontend.url().unwrap().as_str().ends_with("/index.html"));
        assert!(frontend.initialization_script().contains("\"apiVersion\""));
    }

    #[test]
    fn rejects_incompatible_kernel_api() {
        let dir = fixture(r#"{"name":"Demo","version":"1.0.0","kernelApi":"^2.0"}"#);
        let error = resolve_dir(dir.path()).unwrap_err();
        assert!(error.contains("^2.0") && error.contains(KERNEL_API_VERSION), "{error}");
    }

    #[test]
    fn rejects_entry_escaping_the_directory() {
        let dir = fixture(
            r#"{"name":"Demo","version":"1.0.0","kernelApi":"^1.0","entry":"../evil.html"}"#,
        );
        let error = resolve_dir(dir.path()).unwrap_err();
        assert!(error.contains("entry"), "{error}");
    }

    #[test]
    fn serves_entry_and_assets_with_mime() {
        let dir = fixture(r#"{"name":"Demo","version":"1.0.0","kernelApi":"^1.0"}"#);
        let frontend = resolve_dir(dir.path()).unwrap();

        let index = respond(Some(&frontend), &request("/"));
        assert_eq!(index.status(), StatusCode::OK);
        assert_eq!(index.body(), b"<html>ok</html>");
        assert_eq!(
            index.headers().get(header::CONTENT_TYPE).unwrap(),
            "text/html; charset=utf-8"
        );

        let asset = respond(Some(&frontend), &request("/assets/app.js"));
        assert_eq!(asset.status(), StatusCode::OK);
        assert_eq!(
            asset.headers().get(header::CONTENT_TYPE).unwrap(),
            "text/javascript; charset=utf-8"
        );
    }

    #[test]
    fn falls_back_to_entry_for_spa_routes() {
        let dir = fixture(r#"{"name":"Demo","version":"1.0.0","kernelApi":"^1.0"}"#);
        let frontend = resolve_dir(dir.path()).unwrap();
        let response = respond(Some(&frontend), &request("/instances/abc"));
        assert_eq!(response.status(), StatusCode::OK);
        assert_eq!(response.body(), b"<html>ok</html>");
    }

    #[test]
    fn blocks_path_traversal() {
        let dir = fixture(r#"{"name":"Demo","version":"1.0.0","kernelApi":"^1.0"}"#);
        let frontend = resolve_dir(dir.path()).unwrap();
        // 把秘密文件放在前端目录之外
        let secret = dir.path().parent().unwrap().join("mcugl-secret.txt");
        std::fs::write(&secret, b"secret").unwrap();
        let response = respond(Some(&frontend), &request("/../mcugl-secret.txt"));
        assert_eq!(response.status(), StatusCode::NOT_FOUND);
        assert_ne!(response.body(), b"secret");
        let nested = respond(Some(&frontend), &request("/assets/../../mcugl-secret.txt"));
        assert_eq!(nested.status(), StatusCode::NOT_FOUND);
        std::fs::remove_file(secret).unwrap();
    }

    #[test]
    fn missing_asset_is_404_not_entry() {
        let dir = fixture(r#"{"name":"Demo","version":"1.0.0","kernelApi":"^1.0"}"#);
        let frontend = resolve_dir(dir.path()).unwrap();
        let response = respond(Some(&frontend), &request("/assets/missing.js"));
        assert_eq!(response.status(), StatusCode::NOT_FOUND);
    }

    /// 仓库自带的示例前端必须始终能通过校验并读出入口，否则 P0 的验收对象就坏了。
    #[test]
    fn shipped_example_frontend_resolves() {
        let dir = Path::new(env!("CARGO_MANIFEST_DIR")).join("../examples/third-party-frontend");
        let frontend = resolve_dir(&dir).unwrap();
        assert_eq!(frontend.entry, "index.html");
        let index = respond(Some(&frontend), &request("/"));
        assert_eq!(index.status(), StatusCode::OK);
        assert!(String::from_utf8_lossy(index.body()).contains("__TAURI_INTERNALS__"));
    }
}
