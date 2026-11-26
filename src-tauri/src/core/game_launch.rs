//! 从已解析的本地实例生成 Java 命令，准备原生库并启动子进程。

use std::{
    collections::{HashMap, HashSet},
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
    launch_profile::{Argument, ArgumentValue, VersionProfile},
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

pub struct LaunchOptions<'a> {
    pub root: &'a Path,
    pub version_dir: &'a Path,
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

pub(crate) fn prepare_launch(
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
    for library in launch_libraries(version, platform, options.java) {
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
    let base_jar = require_file(
        options
            .root
            .join("versions")
            .join(&version.jar)
            .join(format!("{}.jar", version.jar)),
    )?;
    if !version.inherits
        || !version.libraries.iter().any(|library| {
            library.name.starts_with("net.minecraftforge:forge:")
                && library.name.ends_with(":client")
        })
    {
        jars.push(base_jar);
    }
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
        let mut jvm = resolve_arguments(&args.jvm, platform, options.java, &fields)?;
        if version.inherits && version.main_class == "cpw.mods.bootstraplauncher.BootstrapLauncher"
        {
            ignore_inherited_client(&mut jvm, &version.jar);
        }
        command
            .args(jvm)
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

fn launch_libraries<'a>(
    version: &'a VersionProfile,
    platform: &str,
    java: &JavaRuntime,
) -> Vec<&'a Library> {
    let allowed = version
        .libraries
        .iter()
        .filter(|library| allowed(library.rules.as_deref(), platform, java));
    if !version.inherits {
        return allowed.collect();
    }
    let mut seen = HashSet::new();
    let mut selected: Vec<_> = allowed
        .rev()
        .filter(|library| library_key(&library.name).is_none_or(|key| seen.insert(key)))
        .collect();
    selected.reverse();
    selected
}

fn library_key(name: &str) -> Option<(&str, &str, &str, &str)> {
    let (coordinate, extension) = name.split_once('@').unwrap_or((name, "jar"));
    let mut parts = coordinate.split(':');
    let (group, artifact, _version) = (parts.next()?, parts.next()?, parts.next()?);
    let classifier = parts.next().unwrap_or_default();
    parts
        .next()
        .is_none()
        .then_some((group, artifact, classifier, extension))
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

fn ignore_inherited_client(args: &mut [OsString], jar_id: &str) {
    let jar = format!("{jar_id}.jar");
    if let Some(arg) = args.iter_mut().rev().find(|arg| {
        arg.to_str()
            .is_some_and(|value| value.starts_with("-DignoreList="))
    }) {
        let ignored = arg.to_str().unwrap().trim_start_matches("-DignoreList=");
        // BootstrapLauncher loads the patched client separately. Its list compares filenames.
        if !ignored.split(',').any(|entry| entry == jar) {
            if !ignored.is_empty() {
                arg.push(",");
            }
            arg.push(jar);
        }
    }
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn bootstrap_ignore_list_contains_the_actual_client_filename_once() {
        let mut args = [
            OsString::from("-DignoreList=old-list"),
            OsString::from("-DignoreList=client-extra,neoforge-21.4.161.jar"),
        ];
        ignore_inherited_client(&mut args, "1.21.4");
        ignore_inherited_client(&mut args, "1.21.4");
        assert_eq!(args[0], "-DignoreList=old-list");
        assert_eq!(
            args[1],
            "-DignoreList=client-extra,neoforge-21.4.161.jar,1.21.4.jar"
        );
    }
}
