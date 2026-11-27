//! 从 Mojang 安装启动器管理的 Java。调用方应在后台线程执行。

use std::{
    collections::{BTreeMap, HashMap},
    fs, io,
    path::{Component, Path},
    thread,
};

use reqwest::blocking::Client;
use serde::Deserialize;

use super::{
    file_download::{FileIntegrity, download_file},
    java_runtime::{JavaRuntime, detect_java, java_major, managed_java_executable},
    vanilla_download::safe_component,
};

const RUNTIME_INDEX: &str = "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

#[derive(Deserialize)]
struct RuntimeRelease {
    manifest: Download,
    version: RuntimeVersion,
}

#[derive(Deserialize)]
struct RuntimeVersion {
    name: String,
    released: String,
}

#[derive(Deserialize)]
struct Download {
    url: String,
    size: u64,
    sha1: String,
}

impl Download {
    fn save(&self, client: &Client, path: &Path) -> io::Result<()> {
        download_file(
            client,
            &self.url,
            path,
            &FileIntegrity {
                size: Some(self.size),
                sha1: &self.sha1,
            },
        )
    }
}

#[derive(Deserialize)]
struct RuntimeManifest {
    files: BTreeMap<String, RuntimeEntry>,
}

#[derive(Deserialize)]
#[serde(tag = "type", rename_all = "lowercase")]
enum RuntimeEntry {
    Directory,
    File {
        downloads: RuntimeDownloads,
        executable: bool,
    },
    Link {
        target: String,
    },
}

#[derive(Deserialize)]
struct RuntimeDownloads {
    raw: Download,
}

/// 下载指定主版本的 Java，校验完成后安装到 `managed_dir/java-<major>`。
pub fn install_java(client: &Client, managed_dir: &Path, major: u32) -> io::Result<JavaRuntime> {
    let platform = match (std::env::consts::OS, std::env::consts::ARCH) {
        ("linux", "x86_64") => "linux",
        ("linux", "x86") => "linux-i386",
        ("macos", "x86_64") => "mac-os",
        ("macos", "aarch64") => "mac-os-arm64",
        ("windows", "x86_64") => "windows-x64",
        ("windows", "x86") => "windows-x86",
        ("windows", "aarch64") => "windows-arm64",
        _ => {
            return Err(io::Error::new(
                io::ErrorKind::Unsupported,
                "Mojang 未提供当前平台的 Java 运行时。",
            ));
        }
    };
    if major == 0 {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Java 主版本必须大于零。",
        ));
    }
    let destination = managed_dir.join(format!("java-{major}"));
    if destination.try_exists()? {
        return check_installed(&destination, major);
    }
    let index: HashMap<String, HashMap<String, Vec<RuntimeRelease>>> = client
        .get(RUNTIME_INDEX)
        .send()
        .and_then(reqwest::blocking::Response::error_for_status)
        .and_then(reqwest::blocking::Response::json)
        .map_err(io::Error::other)?;
    let release = index
        .get(platform)
        .into_iter()
        .flat_map(|components| components.iter())
        .filter(|(name, _)| !name.ends_with("-snapshot"))
        .flat_map(|(_, releases)| releases)
        .filter(|release| java_major(&release.version.name) == Some(major))
        .max_by_key(|release| (&release.version.released, &release.manifest.sha1))
        .ok_or_else(|| {
            io::Error::new(
                io::ErrorKind::NotFound,
                format!("Mojang 未提供适合此平台的 Java {major}。"),
            )
        })?;

    fs::create_dir_all(managed_dir)?;
    let staging = tempfile::Builder::new()
        .prefix(".mcug-java-")
        .tempdir_in(managed_dir)?;
    let manifest_path = staging.path().join("manifest.json");
    release.manifest.save(client, &manifest_path)?;
    let manifest: RuntimeManifest = serde_json::from_reader(fs::File::open(manifest_path)?)
        .map_err(|error| io::Error::new(io::ErrorKind::InvalidData, error))?;
    let payload = staging.path().join("runtime");
    fs::create_dir(&payload)?;
    install_files(client, &payload, manifest.files)?;
    check_installed(&payload, major)?;
    match fs::rename(&payload, &destination) {
        Ok(()) => check_installed(&destination, major),
        Err(error) if destination.try_exists()? => {
            check_installed(&destination, major).map_err(|_| error)
        }
        Err(error) => Err(error),
    }
}

fn install_files(
    client: &Client,
    payload: &Path,
    files: BTreeMap<String, RuntimeEntry>,
) -> io::Result<()> {
    let mut downloads = Vec::new();
    let mut links = Vec::new();
    for (relative, entry) in files {
        if !relative.split('/').all(safe_component) {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "Java 文件清单包含无效路径。",
            ));
        }
        let path = payload.join(relative);
        match entry {
            RuntimeEntry::Directory => fs::create_dir_all(path)?,
            RuntimeEntry::File {
                downloads: sources,
                executable,
            } => {
                downloads.push((path, sources.raw, executable));
            }
            RuntimeEntry::Link { target } => {
                validate_link(payload, &path, &target)?;
                links.push((path, target));
            }
        }
    }
    for batch in downloads.chunks(8) {
        thread::scope(|scope| {
            let jobs: Vec<_> = batch
                .iter()
                .map(|(path, download, executable)| {
                    scope.spawn(move || {
                        download.save(client, path)?;
                        #[cfg(unix)]
                        if *executable {
                            use std::os::unix::fs::PermissionsExt;
                            fs::set_permissions(path, fs::Permissions::from_mode(0o755))?;
                        }
                        #[cfg(not(unix))]
                        let _ = executable;
                        Ok(())
                    })
                })
                .collect();
            jobs.into_iter().try_for_each(|job| {
                job.join()
                    .unwrap_or_else(|_| Err(io::Error::other("Java 下载线程异常退出。")))
            })
        })?;
    }
    for (path, target) in links {
        create_link(Path::new(&target), &path)?;
    }
    Ok(())
}

fn validate_link(root: &Path, path: &Path, target: &str) -> io::Result<()> {
    let invalid = || {
        io::Error::new(
            io::ErrorKind::InvalidData,
            "Java 文件清单中的链接指向运行时目录之外。",
        )
    };
    let mut depth = path
        .strip_prefix(root)
        .map_err(|_| invalid())?
        .components()
        .count()
        - 1;
    if target.is_empty() || target.contains('\\') || target.contains(':') {
        return Err(invalid());
    }
    for part in Path::new(target).components() {
        match part {
            Component::Normal(name) if name.to_str().is_some_and(safe_component) => depth += 1,
            Component::CurDir => {}
            Component::ParentDir if depth > 0 => depth -= 1,
            _ => return Err(invalid()),
        }
    }
    Ok(())
}

#[cfg(unix)]
fn create_link(target: &Path, link: &Path) -> io::Result<()> {
    std::os::unix::fs::symlink(target, link)
}

#[cfg(windows)]
fn create_link(target: &Path, link: &Path) -> io::Result<()> {
    let resolved = link.parent().unwrap().join(target);
    if resolved.is_dir() {
        std::os::windows::fs::symlink_dir(target, link)
    } else {
        std::os::windows::fs::symlink_file(target, link)
    }
}

fn check_installed(home: &Path, major: u32) -> io::Result<JavaRuntime> {
    let runtime = detect_java(&managed_java_executable(home))?;
    if runtime.major != major {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "安装后的 Java 主版本与请求不符。",
        ));
    }
    Ok(runtime)
}

#[cfg(test)]
mod tests {
    use super::validate_link;
    use std::path::Path;

    #[test]
    fn runtime_links_stay_within_installation() {
        let root = Path::new("runtime");
        let link = root.join("lib/link");
        assert!(validate_link(root, &link, "../bin/java").is_ok());
        for target in ["../../outside", "/outside", "C:/outside", "..\\..\\outside"] {
            assert!(validate_link(root, &link, target).is_err());
        }
    }
}
