//! 共享的加载器安装、校验和发布流程。

use std::{
    fs, io,
    path::{Path, PathBuf},
    process::{Command, Stdio},
    thread,
    time::{Duration, Instant},
};

use reqwest::blocking::Client;
use serde::Deserialize;
use sha1::{Digest, Sha1};

use super::{
    file_download::{FileIntegrity, download_file, verify_file},
    java_runtime::select_java,
    vanilla_download::download_vanilla,
};

const INSTALLER_TIMEOUT: Duration = Duration::from_secs(15 * 60);

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct VersionProfile {
    id: String,
    inherits_from: String,
    libraries: Vec<Library>,
}

#[derive(Deserialize)]
struct Library {
    name: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct BaseVersion {
    java_version: Option<JavaVersion>,
    downloads: BaseDownloads,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct JavaVersion {
    major_version: u32,
}

#[derive(Deserialize)]
struct BaseDownloads {
    client: ClientArtifact,
}

#[derive(Deserialize)]
struct ClientArtifact {
    sha1: String,
    size: u64,
}

pub(super) struct Installer<'a> {
    pub id: &'a str,
    pub url: &'a str,
    pub flag: &'a str,
    pub identifies: fn(&str) -> bool,
}

pub(super) fn install(
    client: &Client,
    root: &Path,
    minecraft: &str,
    configured_java: &str,
    managed_java: &Path,
    spec: Installer<'_>,
    mut report: impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
) -> io::Result<()> {
    let versions_dir = root.join("versions");
    let profile_id = spec.id;
    let destination = versions_dir.join(profile_id);
    ensure_absent(&destination)?;
    fs::create_dir_all(&versions_dir)?;
    let staging = tempfile::Builder::new()
        .prefix(".mcug-loader-")
        .tempdir_in(&versions_dir)?;
    let work = staging.path();
    let installer = work.join("installer.jar");
    report("installerDownload", None)?;
    fetch_maven_file(client, spec.url, &installer, None, None)?;
    let profile = read_installer_profile(&installer)?;
    validate_profile(&profile, profile_id, minecraft, spec.identifies)?;

    let base = ensure_base(client, root, minecraft, &mut report)?;
    report("preparingBase", None)?;
    let metadata_name = format!("{minecraft}.json");
    let jar_name = format!("{minecraft}.jar");
    let metadata_bytes = fs::read(base.join(&metadata_name))?;
    let metadata: BaseVersion = serde_json::from_slice(&metadata_bytes).map_err(io::Error::from)?;
    verify_file(
        &base.join(&jar_name),
        &FileIntegrity {
            sha1: &metadata.downloads.client.sha1,
            size: Some(metadata.downloads.client.size),
        },
    )
    .map_err(|error| invalid(format!("原版客户端文件不可用：{error}")))?;
    let required_java = metadata
        .java_version
        .map_or(17, |java| java.major_version)
        .max(17);
    let java = select_java(configured_java, required_java, managed_java)?;

    let launcher = work.join("game");
    let staged_base = launcher.join("versions").join(minecraft);
    fs::create_dir_all(&staged_base)?;
    fs::write(staged_base.join(metadata_name), metadata_bytes)?;
    fs::copy(base.join(&jar_name), staged_base.join(&jar_name))?;
    fs::write(
        launcher.join("launcher_profiles.json"),
        br#"{"profiles":{}}"#,
    )?;
    run_installer(
        &java.executable,
        &installer,
        &launcher,
        spec.flag,
        &mut report,
    )?;

    let installed = launcher.join("versions").join(profile_id);
    let installed_json = installed.join(format!("{profile_id}.json"));
    let profile: VersionProfile =
        serde_json::from_reader(fs::File::open(installed_json)?).map_err(io::Error::from)?;
    validate_profile(&profile, profile_id, minecraft, spec.identifies)?;

    report("publishingLibraries", None)?;
    publish_libraries(
        &launcher.join("libraries"),
        &root.join("libraries"),
        &mut report,
    )?;
    report("committing", None)?;
    ensure_absent(&destination)?;
    fs::rename(installed, destination)
}

fn read_installer_profile(installer: &Path) -> io::Result<VersionProfile> {
    let mut archive = zip::ZipArchive::new(fs::File::open(installer)?).map_err(io::Error::other)?;
    let manifest = archive.by_name("version.json").map_err(io::Error::other)?;
    serde_json::from_reader(manifest).map_err(io::Error::from)
}

fn validate_profile(
    profile: &VersionProfile,
    id: &str,
    minecraft: &str,
    identifies: fn(&str) -> bool,
) -> io::Result<()> {
    if profile.id != id
        || profile.inherits_from != minecraft
        || !profile
            .libraries
            .iter()
            .any(|library| identifies(&library.name))
    {
        return Err(invalid("加载器安装器的版本信息与请求不一致。"));
    }
    Ok(())
}

fn run_installer(
    java: &Path,
    installer: &Path,
    launcher: &Path,
    flag: &str,
    report: &mut impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
) -> io::Result<()> {
    report("runningInstaller", None)?;
    let log_path = installer.with_extension("output.log");
    let log = fs::File::create(&log_path)?;
    let mut command = Command::new(java);
    command
        .current_dir(launcher)
        .args(["-Djava.awt.headless=true", "-jar"])
        .arg(installer)
        .arg(flag)
        .arg(launcher)
        .stdin(Stdio::null())
        .stdout(Stdio::from(log.try_clone()?))
        .stderr(Stdio::from(log));
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000);
    }
    let mut child = command.spawn()?;
    let deadline = Instant::now() + INSTALLER_TIMEOUT;
    let result = (|| {
        loop {
            report("runningInstaller", None)?;
            if let Some(status) = child.try_wait()? {
                if status.success() {
                    return Ok(());
                }
                let log = fs::read_to_string(&log_path).unwrap_or_default();
                let tail = log.lines().rev().take(6).collect::<Vec<_>>();
                let tail = tail.into_iter().rev().collect::<Vec<_>>().join("\n");
                return Err(io::Error::other(format!(
                    "加载器安装器退出（{status}）：{tail}"
                )));
            }
            if Instant::now() >= deadline {
                return Err(io::Error::new(
                    io::ErrorKind::TimedOut,
                    "加载器安装器运行超时。",
                ));
            }
            thread::sleep(Duration::from_millis(200));
        }
    })();
    if result.is_err() {
        let _ = child.kill();
        let _ = child.wait();
    }
    result
}

pub(super) fn publish_libraries(
    source: &Path,
    destination: &Path,
    report: &mut impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
) -> io::Result<()> {
    let mut pending = vec![(PathBuf::from(source), PathBuf::from(destination))];
    while let Some((source, destination)) = pending.pop() {
        // Reject substituted output directories before publishing shared files.
        match fs::create_dir(&destination) {
            Ok(()) => {}
            Err(failure)
                if failure.kind() == io::ErrorKind::AlreadyExists
                    && fs::symlink_metadata(&destination)?.is_dir() => {}
            Err(failure) => return Err(failure),
        }
        for entry in fs::read_dir(source)? {
            let entry = entry?;
            let output = destination.join(entry.file_name());
            let kind = entry.file_type()?;
            if kind.is_dir() {
                pending.push((entry.path(), output));
            } else if kind.is_file() {
                report("publishingLibraries", None)?;
                // The staging directory and the game directory are on the same filesystem.
                match fs::hard_link(entry.path(), &output) {
                    Ok(()) => {}
                    Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
                        if !fs::symlink_metadata(&output)?.is_file()
                            || file_hash(&entry.path())? != file_hash(&output)?
                        {
                            return Err(io::Error::new(
                                io::ErrorKind::AlreadyExists,
                                format!("加载器支持库与已存在的文件冲突：{}", output.display()),
                            ));
                        }
                    }
                    Err(error) => return Err(error),
                }
            } else {
                return Err(invalid("加载器支持库包含非普通文件。"));
            }
        }
    }
    Ok(())
}

fn file_hash(path: &Path) -> io::Result<[u8; 20]> {
    let mut hash = Sha1::new();
    io::copy(&mut fs::File::open(path)?, &mut hash)?;
    Ok(hash.finalize().into())
}

pub(super) fn ensure_absent(path: &Path) -> io::Result<()> {
    match fs::symlink_metadata(path) {
        Ok(_) => Err(io::Error::new(
            io::ErrorKind::AlreadyExists,
            "目标版本目录已存在。",
        )),
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(error),
    }
}

fn invalid(message: impl Into<String>) -> io::Error {
    io::Error::new(io::ErrorKind::InvalidData, message.into())
}

pub(super) fn ensure_base(
    client: &Client,
    root: &Path,
    minecraft: &str,
    report: &mut impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
) -> io::Result<PathBuf> {
    let base = root.join("versions").join(minecraft);
    let base_missing = match fs::symlink_metadata(&base) {
        Ok(_) => false,
        Err(error) if error.kind() == io::ErrorKind::NotFound => true,
        Err(error) => return Err(error),
    };
    if base_missing {
        let result = download_vanilla(client, root, minecraft, |phase, progress| {
            report(
                if phase == "committing" {
                    "preparingBase"
                } else {
                    phase
                },
                progress,
            )
        });
        if let Err(error) = result {
            if error.kind() != io::ErrorKind::AlreadyExists {
                return Err(error);
            }
        }
    }
    Ok(base)
}

pub(super) fn fetch_maven_file(
    client: &Client,
    url: &str,
    output: &Path,
    checksum: Option<&str>,
    size: Option<u64>,
) -> io::Result<String> {
    let sha1 = match checksum {
        Some(checksum) => checksum.to_owned(),
        None => client
            .get(format!("{url}.sha1"))
            .send()
            .and_then(reqwest::blocking::Response::error_for_status)
            .and_then(reqwest::blocking::Response::text)
            .map_err(io::Error::other)?
            .split_whitespace()
            .next()
            .unwrap_or_default()
            .to_owned(),
    };
    download_file(client, url, output, &FileIntegrity { sha1: &sha1, size })?;
    Ok(sha1)
}
