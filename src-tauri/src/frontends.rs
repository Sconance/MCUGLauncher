//! 第三方前端包（`.mcuglf`）的安装与管理。
//!
//! `.mcuglf` 就是一个普通 zip 包：根部放 `frontend.json`，旁边是入口页面和资源。
//! 安装时把包解压到 `<数据目录>/frontends/<identifier>/<version>/`，运行时仍按
//! 普通目录提供文件（自定义协议不解析 zip），所以安装一次之后与 `MCUGL_FRONTEND`
//! 指向目录的加载方式完全一致。
//!
//! 出于「离线可构建」的约束，这里没有引入 `zip` crate，只实现了读取所需的最小
//! zip 子集：store / deflate、UTF-8 文件名、拒绝加密与符号链接、校验 CRC32。
//! 打包脚本在 `scripts/pack-frontend.mjs`，同样不依赖第三方库。

use std::{
    collections::HashSet,
    fs,
    io::Read,
    path::{Path, PathBuf},
    time::{SystemTime, UNIX_EPOCH},
};

use flate2::read::DeflateDecoder;

use crate::frontend::{resolve_dir, Frontend, FrontendManifest};

/// 已安装前端的存放目录（相对数据目录）。
pub const STORE_DIR: &str = "frontends";
/// 原始 `.mcuglf` 包的存档目录（相对 `frontends/`）。
const PACKAGES_DIR: &str = "packages";
/// `MCUGL_FRONTEND` 指向包文件时的临时解压目录（相对 `frontends/`）。
const DEV_DIR: &str = ".dev";

const MAX_ENTRIES: usize = 2048;
const MAX_ENTRY_BYTES: u64 = 128 * 1024 * 1024;
const MAX_TOTAL_BYTES: u64 = 256 * 1024 * 1024;

/// 包内图标的默认文件名：与 `frontend.json` 同级。清单里的 `icon` 可以覆盖它。
pub const DEFAULT_ICON: &str = "icon.png";
/// 图标文件上限。图标只在设置页里显示，没必要允许塞壁纸。
const MAX_ICON_BYTES: u64 = 512 * 1024;

const EOCD_SIG: u32 = 0x0605_4b50;
const CENTRAL_SIG: u32 = 0x0201_4b50;
const LOCAL_SIG: u32 = 0x0403_4b50;

/// 安装失败的三类原因，命令层据此映射成稳定的 `ApiError.code`。
#[derive(Debug)]
pub enum PackageError {
    /// 包结构/清单校验失败（VALIDATION_ERROR）。
    Invalid(String),
    /// 同 identifier + version 已经安装（CONFLICT）。
    Conflict(String),
    /// 读写文件失败（IO_ERROR）。
    Io(String),
}

impl PackageError {
    pub fn message(&self) -> &str {
        match self {
            PackageError::Invalid(message) | PackageError::Conflict(message) => message,
            PackageError::Io(message) => message,
        }
    }
}

/// 一个已安装的前端版本。
#[derive(Debug, Clone)]
pub struct InstalledFrontend {
    pub identifier: String,
    pub name: String,
    pub version: String,
    pub kernel_api: String,
    pub entry: String,
    pub author: Option<String>,
    pub description: Option<String>,
    pub homepage: Option<String>,
    /// 图标文件的绝对路径。缺失时为 `None`，由宿主显示内置占位图标。
    pub icon: Option<PathBuf>,
    pub dir: PathBuf,
}

/// 列出数据目录里所有可用的前端版本。坏掉的版本目录只打日志，不阻断其它前端。
pub fn list(data_dir: &Path) -> Result<Vec<InstalledFrontend>, String> {
    let root = data_dir.join(STORE_DIR);
    if !root.is_dir() {
        return Ok(Vec::new());
    }
    let mut items = Vec::new();
    let entries =
        fs::read_dir(&root).map_err(|error| format!("读取前端目录 {} 失败：{error}", root.display()))?;
    for entry in entries {
        let entry = entry.map_err(|error| error.to_string())?;
        if !entry.file_type().map(|kind| kind.is_dir()).unwrap_or(false) {
            continue;
        }
        let dir_name = entry.file_name().to_string_lossy().to_string();
        if dir_name == PACKAGES_DIR || dir_name == DEV_DIR || dir_name.starts_with(".staging-") {
            continue;
        }
        let identifier_dir = entry.path();
        let versions = match fs::read_dir(&identifier_dir) {
            Ok(versions) => versions,
            Err(error) => {
                eprintln!("[mcugl] 跳过无法读取的前端 {}：{error}", identifier_dir.display());
                continue;
            }
        };
        for version in versions {
            let version = match version {
                Ok(version) => version,
                Err(_) => continue,
            };
            if !version.file_type().map(|kind| kind.is_dir()).unwrap_or(false) {
                continue;
            }
            let version_dir = version.path();
            match resolve_dir(&version_dir) {
                Ok(frontend) => {
                    let identifier = frontend
                        .manifest
                        .identifier
                        .clone()
                        .unwrap_or_else(|| dir_name.clone());
                    items.push(summary(identifier, frontend));
                }
                Err(reason) => {
                    eprintln!("[mcugl] 跳过无效前端 {}：{reason}", version_dir.display());
                }
            }
        }
    }
    items.sort_by(|a, b| {
        a.name
            .cmp(&b.name)
            .then_with(|| a.identifier.cmp(&b.identifier))
            .then_with(|| a.version.cmp(&b.version))
    });
    Ok(items)
}

/// 按 identifier 解析当前要运行的前端：多个版本时取 semver 最大的那个。
pub fn resolve_active(data_dir: &Path, identifier: &str) -> Result<Frontend, String> {
    validate_identifier(identifier)?;
    let base = data_dir.join(STORE_DIR).join(identifier);
    let mut versions: Vec<(semver::Version, PathBuf)> = Vec::new();
    let entries = fs::read_dir(&base).map_err(|error| {
        format!(
            "前端 {identifier} 未安装或已被删除（{}）：{error}",
            base.display()
        )
    })?;
    for entry in entries {
        let entry = entry.map_err(|error| error.to_string())?;
        let dir = entry.path();
        if !dir.is_dir() {
            continue;
        }
        let name = entry.file_name().to_string_lossy().to_string();
        if let Ok(version) = semver::Version::parse(&name) {
            versions.push((version, dir));
        }
    }
    let (_, dir) = versions
        .into_iter()
        .max_by(|a, b| a.0.cmp(&b.0))
        .ok_or_else(|| format!("前端 {identifier} 没有可用的版本目录"))?;
    let mut frontend = resolve_dir(&dir)?;
    frontend.mode = "package";
    Ok(frontend)
}

/// 安装一个 `.mcuglf` / `.zip` 包。
pub fn install(data_dir: &Path, package: &Path) -> Result<InstalledFrontend, PackageError> {
    let bytes = fs::read(package).map_err(|error| {
        PackageError::Io(format!("读取前端包 {} 失败：{error}", package.display()))
    })?;
    let entries = parse_zip(&bytes).map_err(PackageError::Invalid)?;

    let store = data_dir.join(STORE_DIR);
    fs::create_dir_all(&store)
        .map_err(|error| PackageError::Io(format!("创建前端目录失败：{error}")))?;
    let staging = store.join(format!(".staging-{}", unique_suffix()));
    if staging.exists() {
        let _ = fs::remove_dir_all(&staging);
    }
    fs::create_dir_all(&staging)
        .map_err(|error| PackageError::Io(format!("创建临时目录失败：{error}")))?;

    let result = (|| -> Result<InstalledFrontend, PackageError> {
        extract(&entries, &bytes, &staging)?;
        let frontend = resolve_dir(&staging).map_err(PackageError::Invalid)?;
        let identifier = frontend.manifest.identifier.clone().ok_or_else(|| {
            PackageError::Invalid(
                "frontend.json 缺少 identifier（反向域名，例如 dev.example.my-frontend）".into(),
            )
        })?;
        validate_identifier(&identifier).map_err(PackageError::Invalid)?;
        let frontend_version = frontend.manifest.version.trim().to_string();
        semver::Version::parse(&frontend_version).map_err(|error| {
            PackageError::Invalid(format!("frontend.json 的 version 不是合法 semver：{error}"))
        })?;

        // 显式指定的图标必须真的存在；默认 icon.png 缺失则回退内置占位图标。
        if let Some(icon) = &frontend.manifest.icon {
            if icon_file(&staging, &frontend.manifest).is_none() {
                return Err(PackageError::Invalid(format!(
                    "frontend.json 指定的图标 {icon} 不存在、不可读或超过 512 KiB"
                )));
            }
        }

        let target = store.join(&identifier).join(&frontend_version);
        if target.exists() {
            return Err(PackageError::Conflict(format!(
                "前端 {identifier} {frontend_version} 已安装；请先在设置里移除旧版本"
            )));
        }
        if let Some(parent) = target.parent() {
            fs::create_dir_all(parent)
                .map_err(|error| PackageError::Io(format!("创建前端目录失败：{error}")))?;
        }
        fs::rename(&staging, &target)
            .map_err(|error| PackageError::Io(format!("安装前端失败：{error}")))?;

        let packages = store.join(PACKAGES_DIR);
        fs::create_dir_all(&packages)
            .map_err(|error| PackageError::Io(format!("创建包存档目录失败：{error}")))?;
        fs::copy(package, packages.join(format!("{identifier}-{frontend_version}.mcuglf")))
            .map_err(|error| PackageError::Io(format!("归档前端包失败：{error}")))?;

        let icon = icon_file(&target, &frontend.manifest);
        let mut installed = summary(identifier, frontend);
        installed.dir = target;
        installed.icon = icon;
        Ok(installed)
    })();

    if result.is_err() {
        let _ = fs::remove_dir_all(&staging);
    }
    result
}

/// 删除已安装的前端。`version` 为空时删除该 identifier 的全部版本。
/// 返回是否真的删掉了东西（用于区分 NOT_FOUND）。
pub fn remove(
    data_dir: &Path,
    identifier: &str,
    version: Option<&str>,
) -> Result<bool, PackageError> {
    validate_identifier(identifier).map_err(PackageError::Invalid)?;
    let base = data_dir.join(STORE_DIR).join(identifier);
    if !base.is_dir() {
        return Ok(false);
    }
    let removed = match version {
        Some(version) => {
            let dir = base.join(version);
            let existed = dir.is_dir();
            if existed {
                fs::remove_dir_all(&dir)
                    .map_err(|error| PackageError::Io(format!("删除前端失败：{error}")))?;
            }
            let package = data_dir
                .join(STORE_DIR)
                .join(PACKAGES_DIR)
                .join(format!("{identifier}-{version}.mcuglf"));
            if package.is_file() {
                let _ = fs::remove_file(&package);
            }
            existed
        }
        None => {
            fs::remove_dir_all(&base)
                .map_err(|error| PackageError::Io(format!("删除前端失败：{error}")))?;
            true
        }
    };
    // identifier 目录空了就一起删掉，免得设置页出现空壳条目。
    if let Ok(mut entries) = fs::read_dir(&base) {
        if entries.next().is_none() {
            let _ = fs::remove_dir(&base);
        }
    }
    Ok(removed)
}

/// `MCUGL_FRONTEND` 指向 `.mcuglf` 文件时的开发模式：解压到 `frontends/.dev/`。
pub fn resolve_dev_package(data_dir: &Path, package: &Path) -> Result<Frontend, String> {
    if package.is_dir() {
        return resolve_dir(package);
    }
    let bytes = fs::read(package)
        .map_err(|error| format!("读取前端包 {} 失败：{error}", package.display()))?;
    let entries = parse_zip(&bytes)?;
    let store = data_dir.join(STORE_DIR).join(DEV_DIR);
    fs::create_dir_all(&store).map_err(|error| format!("创建临时前端目录失败：{error}"))?;
    let staging = store.join(format!(".staging-{}", unique_suffix()));
    if staging.exists() {
        let _ = fs::remove_dir_all(&staging);
    }
    fs::create_dir_all(&staging).map_err(|error| format!("创建临时目录失败：{error}"))?;

    let result = (|| -> Result<Frontend, String> {
        extract(&entries, &bytes, &staging).map_err(|error| error.message().to_string())?;
        let frontend = resolve_dir(&staging)?;
        let identifier = frontend
            .manifest
            .identifier
            .clone()
            .ok_or("frontend.json 缺少 identifier")?;
        validate_identifier(&identifier)?;
        let target = store.join(format!("{identifier}-{}", frontend.manifest.version));
        if target.exists() {
            fs::remove_dir_all(&target).map_err(|error| format!("清理旧临时前端失败：{error}"))?;
        }
        fs::rename(&staging, &target).map_err(|error| format!("安装临时前端失败：{error}"))?;
        let mut frontend = resolve_dir(&target)?;
        frontend.mode = "package";
        Ok(frontend)
    })();

    if result.is_err() {
        let _ = fs::remove_dir_all(&staging);
    }
    result
}

/// 校验包清单里的 identifier：反向域名风格，只留 ASCII 字母数字与 `. - _`。
pub fn validate_identifier(value: &str) -> Result<(), String> {
    let value = value.trim();
    if value.len() < 3 || value.len() > 128 {
        return Err("identifier 长度必须在 3–128 个字符之间".into());
    }
    if !value
        .chars()
        .all(|ch| ch.is_ascii_alphanumeric() || ch == '.' || ch == '-' || ch == '_')
    {
        return Err("identifier 只能包含 ASCII 字母、数字、`.`、`-`、`_`".into());
    }
    if value.starts_with('.') || value.ends_with('.') || value.contains("..") {
        return Err("identifier 不能以点开头/结尾，也不能有连续的点".into());
    }
    const RESERVED: &[&str] = &[
        "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8",
        "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    ];
    for segment in value.split('.') {
        if segment.is_empty() {
            return Err("identifier 不能有空段".into());
        }
        if RESERVED.contains(&segment.to_ascii_lowercase().as_str()) {
            return Err(format!("identifier 的段 `{segment}` 是 Windows 保留名"));
        }
    }
    Ok(())
}

fn summary(identifier: String, frontend: Frontend) -> InstalledFrontend {
    let icon = icon_file(&frontend.dir, &frontend.manifest);
    InstalledFrontend {
        identifier,
        name: frontend.manifest.name.clone(),
        version: frontend.manifest.version.clone(),
        kernel_api: frontend.manifest.kernel_api.clone(),
        entry: frontend.entry.clone(),
        author: frontend.manifest.author.clone(),
        description: frontend.manifest.description.clone(),
        homepage: frontend.manifest.homepage.clone(),
        icon,
        dir: frontend.dir.clone(),
    }
}

/// 解析前端图标：清单里的 `icon` 优先，否则用同目录的 `icon.png`。
/// 不存在、越界、空文件或超过 512 KiB 都返回 `None`（宿主显示占位图标）。
fn icon_file(dir: &Path, manifest: &FrontendManifest) -> Option<PathBuf> {
    let relative = manifest.icon.as_deref().unwrap_or(DEFAULT_ICON);
    if relative.is_empty() || relative.len() > 256 || !is_safe_icon_relative(relative) {
        return None;
    }
    let root = dir.canonicalize().ok()?;
    let resolved = dir.join(relative).canonicalize().ok()?;
    if !resolved.starts_with(&root) || !resolved.is_file() {
        return None;
    }
    let size = fs::metadata(&resolved).ok()?.len();
    if size == 0 || size > MAX_ICON_BYTES {
        return None;
    }
    Some(resolved)
}

fn is_safe_icon_relative(path: &str) -> bool {
    !path.starts_with('/')
        && !path.ends_with('/')
        && !path.contains('\\')
        && !path.contains(':')
        && path
            .split('/')
            .all(|segment| !segment.is_empty() && segment != "." && segment != "..")
}

/// 把图标读成 `data:` URL；设置页直接塞进 `<img src>`。
pub fn icon_data_url(path: &Path) -> Result<Option<String>, String> {
    let mime = match path
        .extension()
        .and_then(|value| value.to_str())
        .map(|value| value.to_ascii_lowercase())
        .as_deref()
    {
        Some("png") => "image/png",
        Some("jpg" | "jpeg") => "image/jpeg",
        Some("webp") => "image/webp",
        Some("gif") => "image/gif",
        Some("svg") => "image/svg+xml",
        // 默认图标名是 icon.png，但清单可以指向别的扩展名；不认识的格式不显示。
        _ => return Ok(None),
    };
    let bytes = match fs::read(path) {
        Ok(bytes) => bytes,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
        Err(error) => {
            return Err(format!("读取前端图标 {} 失败：{error}", path.display()));
        }
    };
    if bytes.is_empty() || bytes.len() as u64 > MAX_ICON_BYTES {
        return Ok(None);
    }
    Ok(Some(format!("data:{mime};base64,{}", base64(&bytes))))
}

/// 极简 base64（标准字母表 + padding）。图标最大 512 KiB，不需要流式实现。
fn base64(bytes: &[u8]) -> String {
    const ALPHABET: &[u8; 64] =
        b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    let mut out = String::with_capacity(bytes.len().div_ceil(3) * 4);
    for chunk in bytes.chunks(3) {
        let b0 = chunk[0] as u32;
        let b1 = chunk.get(1).copied().unwrap_or(0) as u32;
        let b2 = chunk.get(2).copied().unwrap_or(0) as u32;
        let value = (b0 << 16) | (b1 << 8) | b2;
        out.push(ALPHABET[(value >> 18) as usize & 0x3F] as char);
        out.push(ALPHABET[(value >> 12) as usize & 0x3F] as char);
        out.push(if chunk.len() > 1 {
            ALPHABET[(value >> 6) as usize & 0x3F] as char
        } else {
            '='
        });
        out.push(if chunk.len() > 2 {
            ALPHABET[value as usize & 0x3F] as char
        } else {
            '='
        });
    }
    out
}

fn unique_suffix() -> String {
    let nanos = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|duration| duration.as_nanos())
        .unwrap_or_default();
    format!("{}-{nanos}", std::process::id())
}

// ---------------------------------------------------------------------------
// 最小 zip 读取器
// ---------------------------------------------------------------------------

struct ZipEntry {
    name: String,
    method: u16,
    crc32: u32,
    compressed_size: usize,
    uncompressed_size: usize,
    data_offset: usize,
    is_dir: bool,
}

fn u16_at(data: &[u8], at: usize) -> Result<u16, String> {
    data.get(at..at + 2)
        .map(|bytes| u16::from_le_bytes([bytes[0], bytes[1]]))
        .ok_or_else(|| "前端包已损坏（越界读取）".to_string())
}

fn u32_at(data: &[u8], at: usize) -> Result<u32, String> {
    data.get(at..at + 4)
        .map(|bytes| u32::from_le_bytes([bytes[0], bytes[1], bytes[2], bytes[3]]))
        .ok_or_else(|| "前端包已损坏（越界读取）".to_string())
}

fn find_eocd(data: &[u8]) -> Result<usize, String> {
    if data.len() < 22 {
        return Err("不是有效的 zip 包（文件过小）".into());
    }
    let start = data.len().saturating_sub(65_557);
    for offset in (start..=data.len() - 22).rev() {
        if u32_at(data, offset)? == EOCD_SIG {
            return Ok(offset);
        }
    }
    Err("不是有效的 zip 包（找不到目录结尾标记）".into())
}

fn parse_zip(data: &[u8]) -> Result<Vec<ZipEntry>, String> {
    let eocd = find_eocd(data)?;
    let count = u16_at(data, eocd + 10)?;
    let cd_size = u32_at(data, eocd + 12)? as usize;
    let cd_offset = u32_at(data, eocd + 16)? as usize;
    if count == u16::MAX || cd_offset == u32::MAX as usize || cd_size == u32::MAX as usize {
        return Err("暂不支持 ZIP64 格式（条目过多或包超过 4 GiB）".into());
    }
    if count as usize > MAX_ENTRIES {
        return Err(format!("前端包条目过多（{count} > {MAX_ENTRIES}）"));
    }
    if cd_offset
        .checked_add(cd_size)
        .map_or(true, |end| end > data.len())
    {
        return Err("前端包已损坏（目录区越界）".into());
    }

    let mut entries = Vec::with_capacity(count as usize);
    let mut names = HashSet::new();
    let mut total_bytes: u64 = 0;
    let mut cursor = cd_offset;
    for _ in 0..count as usize {
        if u32_at(data, cursor)? != CENTRAL_SIG {
            return Err("前端包已损坏（目录条目签名不对）".into());
        }
        let flags = u16_at(data, cursor + 8)?;
        let method = u16_at(data, cursor + 10)?;
        let crc = u32_at(data, cursor + 16)?;
        let compressed_size = u32_at(data, cursor + 20)? as usize;
        let uncompressed_size = u32_at(data, cursor + 24)? as usize;
        let name_len = u16_at(data, cursor + 28)? as usize;
        let extra_len = u16_at(data, cursor + 30)? as usize;
        let comment_len = u16_at(data, cursor + 32)? as usize;
        let external = u32_at(data, cursor + 38)?;
        let local_offset = u32_at(data, cursor + 42)? as usize;

        let name_start = cursor + 46;
        let name_end = name_start
            .checked_add(name_len)
            .ok_or_else(|| "前端包已损坏（文件名越界）".to_string())?;
        let name_bytes = data
            .get(name_start..name_end)
            .ok_or_else(|| "前端包已损坏（文件名越界）".to_string())?;
        let name = String::from_utf8(name_bytes.to_vec())
            .map_err(|_| "前端包内文件名不是 UTF-8（请用 UTF-8 打包）".to_string())?;
        validate_zip_name(&name)?;
        if !names.insert(name.clone()) {
            return Err(format!("前端包内有重复条目：{name}"));
        }
        if flags & 0x1 != 0 {
            return Err(format!("不支持加密条目：{name}"));
        }
        let unix_mode = (external >> 16) & 0o170000;
        if unix_mode == 0o120000 {
            return Err(format!("不支持符号链接条目：{name}"));
        }

        let is_dir = name.ends_with('/');
        if !is_dir {
            if method != 0 && method != 8 {
                return Err(format!("不支持压缩方式 {method} 的条目：{name}"));
            }
            total_bytes = total_bytes
                .checked_add(uncompressed_size as u64)
                .ok_or_else(|| "前端包已损坏（大小溢出）".to_string())?;
            if total_bytes > MAX_TOTAL_BYTES {
                return Err("前端包解压后超过 256 MiB 上限".into());
            }
            if uncompressed_size as u64 > MAX_ENTRY_BYTES {
                return Err(format!("包内文件过大：{name}"));
            }
        }

        if u32_at(data, local_offset)? != LOCAL_SIG {
            return Err(format!("前端包已损坏（本地头签名不对）：{name}"));
        }
        let local_name_len = u16_at(data, local_offset + 26)? as usize;
        let local_extra_len = u16_at(data, local_offset + 28)? as usize;
        let data_offset = local_offset + 30 + local_name_len + local_extra_len;
        if data_offset
            .checked_add(compressed_size)
            .map_or(true, |end| end > data.len())
        {
            return Err(format!("前端包已损坏（文件数据越界）：{name}"));
        }
        let local_name = data
            .get(local_offset + 30..local_offset + 30 + local_name_len)
            .ok_or_else(|| "前端包已损坏（本地文件名越界）".to_string())?;
        if local_name != name_bytes {
            return Err(format!("前端包已损坏（本地头与目录文件名不一致）：{name}"));
        }

        entries.push(ZipEntry {
            name,
            method,
            crc32: crc,
            compressed_size,
            uncompressed_size,
            data_offset,
            is_dir,
        });
        cursor = name_end
            .checked_add(extra_len)
            .and_then(|value| value.checked_add(comment_len))
            .ok_or_else(|| "前端包已损坏（目录条目越界）".to_string())?;
    }
    Ok(entries)
}

/// 只接受相对、目录内的条目名；Windows 上会被静默改名的结尾空格/点也拒绝。
fn validate_zip_name(name: &str) -> Result<(), String> {
    if name.is_empty() || name.len() > 256 {
        return Err("包内有空文件名或过长文件名".into());
    }
    if name.starts_with('/') || name.contains('\\') || name.contains(':') {
        return Err(format!("包内路径必须是相对路径：{name}"));
    }
    if name.chars().any(char::is_control) {
        return Err(format!("包内路径含控制字符：{name}"));
    }
    let trimmed = name.trim_end_matches('/');
    if trimmed.is_empty() {
        return Err("包内有空路径".into());
    }
    for segment in trimmed.split('/') {
        if segment.is_empty() || segment == "." || segment == ".." {
            return Err(format!("包内路径不能包含 `..` 或空段：{name}"));
        }
        if segment.ends_with(' ') || segment.ends_with('.') {
            return Err(format!("包内路径不能以空格或点结尾：{name}"));
        }
    }
    Ok(())
}

fn extract(entries: &[ZipEntry], archive: &[u8], target: &Path) -> Result<(), PackageError> {
    for entry in entries {
        let relative = entry.name.trim_end_matches('/');
        let mut destination = target.to_path_buf();
        for segment in relative.split('/') {
            destination.push(segment);
        }
        if entry.is_dir {
            fs::create_dir_all(&destination)
                .map_err(|error| PackageError::Io(format!("创建目录失败：{error}")))?;
            continue;
        }
        let bytes = read_entry(archive, entry)?;
        if let Some(parent) = destination.parent() {
            fs::create_dir_all(parent)
                .map_err(|error| PackageError::Io(format!("创建目录失败：{error}")))?;
        }
        fs::write(&destination, &bytes)
            .map_err(|error| PackageError::Io(format!("写入文件失败（{}）：{error}", destination.display())))?;
    }
    Ok(())
}

fn read_entry(archive: &[u8], entry: &ZipEntry) -> Result<Vec<u8>, PackageError> {
    let slice = archive
        .get(entry.data_offset..entry.data_offset + entry.compressed_size)
        .ok_or_else(|| PackageError::Invalid(format!("包内文件数据越界：{}", entry.name)))?;
    let bytes = match entry.method {
        0 => slice.to_vec(),
        8 => {
            let mut decoder = DeflateDecoder::new(slice).take(MAX_ENTRY_BYTES + 1);
            let mut output = Vec::new();
            decoder.read_to_end(&mut output).map_err(|error| {
                PackageError::Invalid(format!("解压失败（{}）：{error}", entry.name))
            })?;
            output
        }
        other => {
            return Err(PackageError::Invalid(format!(
                "不支持压缩方式 {other}：{}",
                entry.name
            )))
        }
    };
    if bytes.len() != entry.uncompressed_size {
        return Err(PackageError::Invalid(format!(
            "解压后大小不一致：{}（{} ≠ {}）",
            entry.name,
            bytes.len(),
            entry.uncompressed_size
        )));
    }
    if crc32(&bytes) != entry.crc32 {
        return Err(PackageError::Invalid(format!(
            "CRC32 校验失败：{}（包已损坏）",
            entry.name
        )));
    }
    Ok(bytes)
}

fn crc32(data: &[u8]) -> u32 {
    let mut table = [0u32; 256];
    for (index, slot) in table.iter_mut().enumerate() {
        let mut value = index as u32;
        for _ in 0..8 {
            value = if value & 1 == 1 {
                0xEDB8_8320 ^ (value >> 1)
            } else {
                value >> 1
            };
        }
        *slot = value;
    }
    let mut crc = 0xFFFF_FFFFu32;
    for byte in data {
        crc = table[((crc ^ *byte as u32) & 0xFF) as usize] ^ (crc >> 8);
    }
    crc ^ 0xFFFF_FFFF
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    fn put_u16(out: &mut Vec<u8>, value: u16) {
        out.extend_from_slice(&value.to_le_bytes());
    }

    fn put_u32(out: &mut Vec<u8>, value: u32) {
        out.extend_from_slice(&value.to_le_bytes());
    }

    /// 测试用的最小 zip 写入器：`deflate` 为真时用 raw deflate，否则 store。
    fn build_zip(files: &[(&str, &[u8], bool)]) -> Vec<u8> {
        let mut out = Vec::new();
        let mut central = Vec::new();
        for (name, content, deflate) in files {
            let compressed = if *deflate {
                let mut encoder =
                    flate2::write::DeflateEncoder::new(Vec::new(), flate2::Compression::default());
                encoder.write_all(content).unwrap();
                encoder.finish().unwrap()
            } else {
                content.to_vec()
            };
            let method: u16 = if *deflate { 8 } else { 0 };
            let crc = crc32(content);
            let offset = out.len() as u32;
            put_u32(&mut out, LOCAL_SIG);
            put_u16(&mut out, 20);
            put_u16(&mut out, 0);
            put_u16(&mut out, method);
            put_u16(&mut out, 0);
            put_u16(&mut out, 0);
            put_u32(&mut out, crc);
            put_u32(&mut out, compressed.len() as u32);
            put_u32(&mut out, content.len() as u32);
            put_u16(&mut out, name.len() as u16);
            put_u16(&mut out, 0);
            out.extend_from_slice(name.as_bytes());
            out.extend_from_slice(&compressed);

            put_u32(&mut central, CENTRAL_SIG);
            put_u16(&mut central, 20);
            put_u16(&mut central, 20);
            put_u16(&mut central, 0);
            put_u16(&mut central, method);
            put_u16(&mut central, 0);
            put_u16(&mut central, 0);
            put_u32(&mut central, crc);
            put_u32(&mut central, compressed.len() as u32);
            put_u32(&mut central, content.len() as u32);
            put_u16(&mut central, name.len() as u16);
            put_u16(&mut central, 0);
            put_u16(&mut central, 0);
            put_u16(&mut central, 0);
            put_u16(&mut central, 0);
            put_u32(&mut central, 0);
            put_u32(&mut central, offset);
            central.extend_from_slice(name.as_bytes());
        }
        let cd_offset = out.len() as u32;
        let cd_size = central.len() as u32;
        out.extend_from_slice(&central);
        put_u32(&mut out, EOCD_SIG);
        put_u16(&mut out, 0);
        put_u16(&mut out, 0);
        put_u16(&mut out, files.len() as u16);
        put_u16(&mut out, files.len() as u16);
        put_u32(&mut out, cd_size);
        put_u32(&mut out, cd_offset);
        put_u16(&mut out, 0);
        out
    }

    fn manifest(identifier: &str, version: &str, kernel_api: &str) -> String {
        format!(
            r#"{{"identifier":"{identifier}","name":"测试前端","version":"{version}","kernelApi":"{kernel_api}","entry":"index.html","author":"测试"}}"#
        )
    }

    fn package(files: &[(&str, &[u8], bool)]) -> Vec<u8> {
        build_zip(files)
    }

    #[test]
    fn installs_store_and_deflate_entries() {
        let data_dir = tempfile::tempdir().unwrap();
        let payload = package(&[
            ("frontend.json", manifest("dev.example.demo", "1.0.0", "^1.0.0").as_bytes(), true),
            ("index.html", b"<h1>hi</h1>".as_slice(), false),
            ("assets/app.js", b"console.log(1)".as_slice(), true),
        ]);
        let path = data_dir.path().join("demo.mcuglf");
        fs::write(&path, &payload).unwrap();

        let installed = install(data_dir.path(), &path).unwrap();
        assert_eq!(installed.identifier, "dev.example.demo");
        assert_eq!(installed.version, "1.0.0");
        assert_eq!(installed.author.as_deref(), Some("测试"));
        assert!(installed.dir.join("index.html").is_file());
        assert_eq!(
            fs::read_to_string(installed.dir.join("assets/app.js")).unwrap(),
            "console.log(1)"
        );
        assert!(
            data_dir
                .path()
                .join("frontends/packages/dev.example.demo-1.0.0.mcuglf")
                .is_file()
        );

        let listed = list(data_dir.path()).unwrap();
        assert_eq!(listed.len(), 1);
        assert_eq!(listed[0].name, "测试前端");

        let active = resolve_active(data_dir.path(), "dev.example.demo").unwrap();
        assert_eq!(active.mode, "package");
        assert_eq!(active.entry, "index.html");
    }

    #[test]
    fn rejects_path_traversal_entries() {
        let data_dir = tempfile::tempdir().unwrap();
        let payload = package(&[
            ("frontend.json", manifest("dev.example.demo", "1.0.0", "^1.0.0").as_bytes(), false),
            ("../evil.txt", b"x".as_slice(), false),
        ]);
        let path = data_dir.path().join("bad.mcuglf");
        fs::write(&path, &payload).unwrap();

        let error = install(data_dir.path(), &path).unwrap_err();
        assert!(matches!(error, PackageError::Invalid(_)), "{error:?}");
        assert!(error.message().contains(".."), "{}", error.message());
    }

    #[test]
    fn rejects_incompatible_kernel_api() {
        let data_dir = tempfile::tempdir().unwrap();
        let payload = package(&[
            (
                "frontend.json",
                manifest("dev.example.demo", "1.0.0", "^2.0.0").as_bytes(),
                false,
            ),
            ("index.html", b"x".as_slice(), false),
        ]);
        let path = data_dir.path().join("future.mcuglf");
        fs::write(&path, &payload).unwrap();

        let error = install(data_dir.path(), &path).unwrap_err();
        assert!(error.message().contains("内核 API"), "{}", error.message());
    }

    #[test]
    fn refuses_to_overwrite_an_installed_version() {
        let data_dir = tempfile::tempdir().unwrap();
        let payload = package(&[
            ("frontend.json", manifest("dev.example.demo", "1.0.0", "^1.0.0").as_bytes(), false),
            ("index.html", b"x".as_slice(), false),
        ]);
        let path = data_dir.path().join("demo.mcuglf");
        fs::write(&path, &payload).unwrap();

        install(data_dir.path(), &path).unwrap();
        let error = install(data_dir.path(), &path).unwrap_err();
        assert!(matches!(error, PackageError::Conflict(_)), "{error:?}");
    }

    #[test]
    fn removes_installed_files_and_archived_package() {
        let data_dir = tempfile::tempdir().unwrap();
        let payload = package(&[
            ("frontend.json", manifest("dev.example.demo", "1.0.0", "^1.0.0").as_bytes(), false),
            ("index.html", b"x".as_slice(), false),
        ]);
        let path = data_dir.path().join("demo.mcuglf");
        fs::write(&path, &payload).unwrap();
        install(data_dir.path(), &path).unwrap();

        assert!(remove(data_dir.path(), "dev.example.demo", None).unwrap());
        assert!(list(data_dir.path()).unwrap().is_empty());
        assert!(!data_dir.path().join("frontends/dev.example.demo").exists());
        assert!(!remove(data_dir.path(), "dev.example.demo", None).unwrap());
    }

    #[test]
    fn picks_the_newest_semver_of_an_identifier() {
        let data_dir = tempfile::tempdir().unwrap();
        for version in ["1.0.0", "1.2.0", "1.10.0"] {
            let payload = package(&[
                (
                    "frontend.json",
                    manifest("dev.example.demo", version, "^1.0.0").as_bytes(),
                    false,
                ),
                ("index.html", b"x".as_slice(), false),
            ]);
            let path = data_dir.path().join(format!("demo-{version}.mcuglf"));
            fs::write(&path, &payload).unwrap();
            install(data_dir.path(), &path).unwrap();
        }
        assert_eq!(list(data_dir.path()).unwrap().len(), 3);
        assert_eq!(
            resolve_active(data_dir.path(), "dev.example.demo")
                .unwrap()
                .manifest
                .version,
            "1.10.0"
        );
    }

    #[test]
    fn validates_identifiers() {
        assert!(validate_identifier("dev.example.my-frontend").is_ok());
        assert!(validate_identifier("ab").is_err());
        assert!(validate_identifier("dev/example").is_err());
        assert!(validate_identifier("../evil").is_err());
        assert!(validate_identifier("dev.con").is_err());
    }

    fn manifest_with_icon(icon: &str) -> String {
        format!(
            r#"{{"identifier":"dev.example.demo","name":"测试前端","version":"1.0.0","kernelApi":"^1.0.0","entry":"index.html","icon":"{icon}"}}"#
        )
    }

    #[test]
    fn reads_the_default_icon_as_a_data_url() {
        let data_dir = tempfile::tempdir().unwrap();
        let payload = package(&[
            ("frontend.json", manifest("dev.example.demo", "1.0.0", "^1.0.0").as_bytes(), false),
            ("index.html", b"x".as_slice(), false),
            ("icon.png", b"abc".as_slice(), false),
        ]);
        let path = data_dir.path().join("demo.mcuglf");
        fs::write(&path, &payload).unwrap();

        let installed = install(data_dir.path(), &path).unwrap();
        let icon = installed.icon.clone().expect("icon.png 应被识别");
        assert!(icon.ends_with("icon.png"), "{}", icon.display());
        assert_eq!(
            icon_data_url(&icon).unwrap().as_deref(),
            Some("data:image/png;base64,YWJj")
        );

        let listed = list(data_dir.path()).unwrap();
        assert!(listed[0].icon.is_some());
    }

    #[test]
    fn missing_icon_falls_back_to_none() {
        let data_dir = tempfile::tempdir().unwrap();
        let payload = package(&[
            ("frontend.json", manifest("dev.example.demo", "1.0.0", "^1.0.0").as_bytes(), false),
            ("index.html", b"x".as_slice(), false),
        ]);
        let path = data_dir.path().join("demo.mcuglf");
        fs::write(&path, &payload).unwrap();

        let installed = install(data_dir.path(), &path).unwrap();
        assert!(installed.icon.is_none());
        assert_eq!(icon_data_url(&installed.dir.join("icon.png")).unwrap(), None);
        let resolved = resolve_dir(&installed.dir).unwrap();
        assert!(icon_file(&resolved.dir, &resolved.manifest).is_none());
    }

    #[test]
    fn rejects_a_manifest_icon_that_does_not_exist() {
        let data_dir = tempfile::tempdir().unwrap();
        let payload = package(&[
            ("frontend.json", manifest_with_icon("missing.png").as_bytes(), false),
            ("index.html", b"x".as_slice(), false),
        ]);
        let path = data_dir.path().join("demo.mcuglf");
        fs::write(&path, &payload).unwrap();

        let error = install(data_dir.path(), &path).unwrap_err();
        assert!(matches!(error, PackageError::Invalid(_)), "{error:?}");
        assert!(error.message().contains("图标"), "{}", error.message());
    }
}
