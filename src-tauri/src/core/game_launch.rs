//! 从本地原版实例生成 Java 命令，准备原生库并启动子进程。

use std::{
    collections::HashMap,
    env,
    ffi::OsString,
    fs::{self, File},
    io::{self, Read},
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
};

use regex::Regex;
use serde::Deserialize;
use tempfile::TempDir;

use super::{
    java_runtime::JavaRuntime,
    vanilla_download::{Library, Rule, library_path, safe_component},
};

pub struct PlayerProfile {
    pub name: String,
    pub uuid: String,
    pub access_token: String,
    pub user_type: String,
}

pub fn offline_profile(name: &str) -> io::Result<PlayerProfile> {
    if !(1..=16).contains(&name.len())
        || !name
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || byte == b'_')
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "离线名称须为 1–16 位英文字母、数字或下划线。",
        ));
    }
    let mut uuid = md5::compute(format!("OfflinePlayer:{name}")).0;
    uuid[6] = (uuid[6] & 0x0f) | 0x30;
    uuid[8] = (uuid[8] & 0x3f) | 0x80;
    Ok(PlayerProfile {
        name: name.into(),
        uuid: uuid.iter().map(|byte| format!("{byte:02x}")).collect(),
        access_token: "0".into(),
        user_type: "legacy".into(),
    })
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VersionProfile {
    id: String,
    #[serde(rename = "type")]
    kind: String,
    main_class: String,
    inherits_from: Option<String>,
    java_version: Option<JavaVersion>,
    asset_index: Option<AssetIndex>,
    assets: Option<String>,
    libraries: Vec<Library>,
    arguments: Option<Arguments>,
    minecraft_arguments: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct JavaVersion {
    major_version: u32,
}

#[derive(Deserialize)]
struct AssetIndex {
    id: String,
}

#[derive(Deserialize)]
struct Arguments {
    jvm: Vec<Argument>,
    game: Vec<Argument>,
}

#[derive(Deserialize)]
#[serde(untagged)]
enum Argument {
    Text(String),
    Conditional {
        rules: Vec<Rule>,
        value: ArgumentValue,
    },
}

#[derive(Deserialize)]
#[serde(untagged)]
enum ArgumentValue {
    Text(String),
    List(Vec<String>),
}

#[derive(Deserialize)]
struct LegacyIndex {
    #[serde(default, rename = "virtual")]
    virtual_assets: bool,
    #[serde(default)]
    map_to_resources: bool,
    objects: HashMap<String, LegacyAsset>,
}

#[derive(Deserialize)]
struct LegacyAsset {
    hash: String,
    size: u64,
}

impl VersionProfile {
    pub fn read(path: &Path) -> io::Result<Self> {
        let profile: Self = read_json(path)?;
        if profile.inherits_from.is_some() {
            return Err(io::Error::new(
                io::ErrorKind::Unsupported,
                "暂未接入继承版本和加载器启动。",
            ));
        }
        if !safe_component(&profile.id) || profile.main_class.is_empty() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "版本缺少有效 ID 或 Java 主类。",
            ));
        }
        Ok(profile)
    }

    pub fn required_java(&self) -> u32 {
        self.java_version
            .as_ref()
            .map_or(8, |version| version.major_version)
    }
}

pub struct LaunchOptions<'a> {
    pub root: &'a Path,
    pub version_dir: &'a Path,
    pub instance_name: &'a str,
    pub java: &'a JavaRuntime,
    pub player: &'a PlayerProfile,
    pub ram_mb: u32,
    pub width: u32,
    pub height: u32,
}

pub struct PreparedLaunch {
    command: Command,
    natives: TempDir,
}

/// 从游戏日志识别渲染线程或原生图形库已开始初始化。
pub(crate) struct LaunchLog {
    reader: File,
    tail: Vec<u8>,
}

impl LaunchLog {
    pub(crate) fn open(path: &Path) -> io::Result<Self> {
        Ok(Self {
            reader: File::open(path)?,
            tail: Vec::new(),
        })
    }

    pub(crate) fn ready(&mut self) -> io::Result<bool> {
        let mut buffer = [0; 16 * 1024];
        let read = self.reader.read(&mut buffer)?;
        self.tail.extend_from_slice(&buffer[..read]);
        let text = String::from_utf8_lossy(&self.tail).to_ascii_lowercase();
        let ready = ["[render thread/", "lwjgl version", "lwjgl openal"]
            .iter()
            .any(|marker| text.contains(marker));
        let keep = self.tail.len().saturating_sub(32);
        self.tail.drain(..keep);
        Ok(ready)
    }
}

impl PreparedLaunch {
    pub fn spawn(mut self, log_path: &Path) -> io::Result<(Child, TempDir)> {
        let log = fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(log_path)?;
        let stderr = log.try_clone()?;
        let child = self
            .command
            .stdin(Stdio::null())
            .stdout(Stdio::from(log))
            .stderr(Stdio::from(stderr))
            .spawn()?;
        Ok((child, self.natives))
    }
}

pub fn prepare_launch(
    version: &VersionProfile,
    options: LaunchOptions<'_>,
) -> io::Result<PreparedLaunch> {
    let platform = if cfg!(target_os = "macos") {
        "osx"
    } else {
        env::consts::OS
    };
    let natives = tempfile::Builder::new()
        .prefix(".mcug-natives-")
        .tempdir_in(options.version_dir)?;
    let assets = options.root.join("assets");
    let index_id = version
        .asset_index
        .as_ref()
        .map(|index| index.id.as_str())
        .or(version.assets.as_deref())
        .unwrap_or("legacy");
    if !safe_component(index_id) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "资源索引 ID 无效。",
        ));
    }
    let game_assets = prepare_assets(&assets, index_id, options.root)?;
    let mut jars = Vec::new();
    for library in &version.libraries {
        if !allowed(library.rules.as_deref(), platform, options.java) {
            continue;
        }
        if let Some(artifact) = &library.downloads.artifact {
            jars.push(require_file(library_path(artifact, options.root)?)?);
        }
        if let Some(classifier) = library.natives.get(platform) {
            let bits = if matches!(options.java.arch.as_str(), "x86" | "i386" | "i686" | "arm") {
                "32"
            } else {
                "64"
            };
            let classifier = classifier.replace("${arch}", bits);
            let artifact = library
                .downloads
                .classifiers
                .get(&classifier)
                .ok_or_else(|| {
                    io::Error::new(
                        io::ErrorKind::InvalidData,
                        format!("缺少原生库描述：{classifier}"),
                    )
                })?;
            extract_natives(&library_path(artifact, options.root)?, natives.path())?;
        }
    }
    jars.push(require_file(
        options
            .version_dir
            .join(format!("{}.jar", options.instance_name)),
    )?);
    let classpath = env::join_paths(jars)
        .map_err(|error| io::Error::new(io::ErrorKind::InvalidInput, error))?;
    let mut fields = HashMap::<&str, OsString>::from([
        ("auth_player_name", options.player.name.clone().into()),
        ("auth_uuid", options.player.uuid.clone().into()),
        (
            "auth_access_token",
            options.player.access_token.clone().into(),
        ),
        ("auth_session", options.player.access_token.clone().into()),
        ("auth_xuid", "".into()),
        ("clientid", "".into()),
        ("user_type", options.player.user_type.clone().into()),
        ("user_properties", "{}".into()),
        ("profile_properties", "{}".into()),
        ("version_name", version.id.clone().into()),
        ("version_type", version.kind.clone().into()),
        ("game_directory", options.root.as_os_str().into()),
        ("assets_root", assets.as_os_str().into()),
        ("game_assets", game_assets.into_os_string()),
        ("assets_index_name", index_id.into()),
        ("natives_directory", natives.path().as_os_str().into()),
        (
            "library_directory",
            options.root.join("libraries").into_os_string(),
        ),
        ("classpath", classpath),
        (
            "classpath_separator",
            if cfg!(windows) { ";" } else { ":" }.into(),
        ),
        ("launcher_name", "MCUGLauncher".into()),
        ("launcher_version", env!("CARGO_PKG_VERSION").into()),
        ("resolution_width", options.width.to_string().into()),
        ("resolution_height", options.height.to_string().into()),
    ]);
    let mut command = Command::new(&options.java.executable);
    command
        .current_dir(options.root)
        .arg(format!("-Xmx{}M", options.ram_mb));
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000);
    }
    if let Some(args) = &version.arguments {
        command
            .args(resolve_arguments(
                &args.jvm,
                platform,
                options.java,
                &fields,
            )?)
            .arg(&version.main_class)
            .args(resolve_arguments(
                &args.game,
                platform,
                options.java,
                &fields,
            )?);
    } else {
        let mut native_path = OsString::from("-Djava.library.path=");
        native_path.push(natives.path());
        command
            .arg(native_path)
            .arg("-cp")
            .arg(fields.remove("classpath").unwrap())
            .arg(&version.main_class);
        let args = version
            .minecraft_arguments
            .as_deref()
            .ok_or_else(|| io::Error::new(io::ErrorKind::InvalidData, "版本缺少游戏启动参数。"))?;
        for arg in args.split_whitespace() {
            command.arg(replace_fields(arg, &fields)?);
        }
    }
    Ok(PreparedLaunch { command, natives })
}

fn allowed(rules: Option<&[Rule]>, platform: &str, java: &JavaRuntime) -> bool {
    rules.is_none_or(|rules| {
        rules
            .iter()
            .rev()
            .find(|rule| {
                rule.features
                    .iter()
                    .all(|(name, enabled)| (name == "has_custom_resolution") == *enabled)
                    && rule.os.as_ref().is_none_or(|os| {
                        os.name.as_deref().is_none_or(|name| name == platform)
                            && os.arch.as_deref().is_none_or(|arch| {
                                arch == java.arch
                                    || matches!(
                                        (arch, java.arch.as_str()),
                                        ("arm64", "aarch64")
                                            | ("aarch64", "arm64")
                                            | ("x86_64", "amd64")
                                            | ("amd64", "x86_64")
                                    )
                            })
                            && os.version.as_deref().is_none_or(|pattern| {
                                Regex::new(pattern)
                                    .is_ok_and(|regex| regex.is_match(&java.os_version))
                            })
                    })
            })
            .is_some_and(|rule| rule.action == "allow")
    })
}

fn resolve_arguments(
    args: &[Argument],
    platform: &str,
    java: &JavaRuntime,
    fields: &HashMap<&str, OsString>,
) -> io::Result<Vec<OsString>> {
    let mut output = Vec::new();
    for arg in args {
        match arg {
            Argument::Text(text) => output.push(replace_fields(text, fields)?),
            Argument::Conditional { rules, value } if allowed(Some(rules), platform, java) => {
                match value {
                    ArgumentValue::Text(text) => output.push(replace_fields(text, fields)?),
                    ArgumentValue::List(list) => {
                        for text in list {
                            output.push(replace_fields(text, fields)?);
                        }
                    }
                }
            }
            _ => {}
        }
    }
    Ok(output)
}

fn replace_fields(mut template: &str, fields: &HashMap<&str, OsString>) -> io::Result<OsString> {
    let mut result = OsString::new();
    while let Some((prefix, placeholder)) = template.split_once("${") {
        result.push(prefix);
        let (key, rest) = placeholder.split_once('}').ok_or_else(|| {
            io::Error::new(
                io::ErrorKind::InvalidData,
                "Java 参数中的占位符缺少结束符。",
            )
        })?;
        let value = fields.get(key).ok_or_else(|| {
            io::Error::new(
                io::ErrorKind::Unsupported,
                format!("暂不支持启动参数 ${{{key}}}。"),
            )
        })?;
        result.push(value);
        template = rest;
    }
    result.push(template);
    Ok(result)
}

fn require_file(path: PathBuf) -> io::Result<PathBuf> {
    if path.is_file() {
        return Ok(path);
    }
    Err(io::Error::new(
        io::ErrorKind::NotFound,
        format!("缺少游戏文件：{}。请检查实例安装。", path.display()),
    ))
}

fn read_json<T: serde::de::DeserializeOwned>(path: &Path) -> io::Result<T> {
    serde_json::from_reader(File::open(path)?).map_err(|error| {
        io::Error::new(
            io::ErrorKind::InvalidData,
            format!("{}：{error}", path.display()),
        )
    })
}

fn prepare_assets(assets: &Path, id: &str, root: &Path) -> io::Result<PathBuf> {
    let index: LegacyIndex = read_json(&assets.join("indexes").join(format!("{id}.json")))?;
    let virtual_dir = assets.join("virtual").join(id);
    let resources = root.join("resources");
    for (name, asset) in index.objects {
        if !name.split('/').all(safe_component)
            || asset.hash.len() != 40
            || !asset.hash.bytes().all(|byte| byte.is_ascii_hexdigit())
        {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "资源索引包含无效的路径或哈希。",
            ));
        }
        let source = assets
            .join("objects")
            .join(&asset.hash[..2])
            .join(&asset.hash);
        if !source
            .metadata()
            .is_ok_and(|info| info.is_file() && info.len() == asset.size)
        {
            return Err(io::Error::new(
                io::ErrorKind::NotFound,
                format!("资源文件 {name} 缺失或不完整，请检查实例安装。",),
            ));
        }
        for destination in [
            index.virtual_assets.then_some(&virtual_dir),
            index.map_to_resources.then_some(&resources),
        ]
        .into_iter()
        .flatten()
        {
            let target = destination.join(&name);
            if target
                .metadata()
                .is_ok_and(|info| info.is_file() && info.len() == asset.size)
            {
                continue;
            }
            fs::create_dir_all(target.parent().unwrap())?;
            fs::copy(&source, target)?;
        }
    }
    Ok(if index.virtual_assets {
        virtual_dir
    } else if index.map_to_resources {
        resources
    } else {
        assets.to_owned()
    })
}

fn extract_natives(path: &Path, destination: &Path) -> io::Result<()> {
    let mut archive = zip::ZipArchive::new(File::open(path)?)?;
    for index in 0..archive.len() {
        let mut entry = archive.by_index(index)?;
        let Some(path) = entry.enclosed_name() else {
            continue;
        };
        let Some(name) = path.file_name().and_then(|name| name.to_str()) else {
            continue;
        };
        if !entry.is_file()
            || !safe_component(name)
            || ![".dll", ".so", ".dylib", ".jnilib"]
                .iter()
                .any(|suffix| name.ends_with(suffix))
        {
            continue;
        }
        const MAX_NATIVE_BYTES: u64 = 128 * 1024 * 1024;
        if entry.size() > MAX_NATIVE_BYTES {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "原生库文件超过大小限制。",
            ));
        }
        let expected = entry.size();
        let mut file = File::create(destination.join(name))?;
        if io::copy(&mut entry.by_ref().take(expected + 1), &mut file)? != expected {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "原生库解压大小与索引不符。",
            ));
        }
    }
    Ok(())
}
