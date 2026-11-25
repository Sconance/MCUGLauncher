//! NeoForge 版本发现与官方无界面客户端安装。

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
    vanilla_download::{download_vanilla, safe_component},
};

const ORIGIN: &str = "https://maven.neoforged.net";
const INSTALLER_TIMEOUT: Duration = Duration::from_secs(15 * 60);

#[derive(Deserialize)]
struct MavenVersions {
    versions: Vec<String>,
}

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

/// Resolve modern NeoForge's Maven version convention to the corresponding game ID.
fn game_version(loader_version: &str) -> Option<String> {
    if !loader_version
        .bytes()
        .all(|byte| byte.is_ascii_alphanumeric() || b".-+".contains(&byte))
    {
        return None;
    }
    let (version, snapshot) = loader_version
        .split_once('+')
        .map_or((loader_version, None), |(v, s)| (v, Some(s)));
    let numbers = version.split('-').next()?;
    let components: Vec<_> = numbers.split('.').collect();
    let unsigned = |value: &str| value.parse::<u32>().ok();
    match components.as_slice() {
        ["0", special, build] if unsigned(build).is_some() && !special.is_empty() => {
            Some((*special).to_owned())
        }
        [major, minor, patch] | [major, minor, patch, _] => {
            let (major, minor, patch) = (unsigned(major)?, unsigned(minor)?, unsigned(patch)?);
            let mut game = match (major, minor, patch) {
                (20..=23, 0, _) if major > 20 => format!("1.{major}"),
                (20, 0..=1, _) => return None,
                (20..=23, _, _) => format!("1.{major}.{minor}"),
                (26.., _, 0) => format!("{major}.{minor}"),
                (26.., _, _) => format!("{major}.{minor}.{patch}"),
                _ => return None,
            };
            if let Some(snapshot) = snapshot {
                game.push('-');
                game.push_str(snapshot);
            }
            Some(game)
        }
        _ => None,
    }
}

pub(crate) fn compatible(minecraft: &str, loader_version: &str) -> bool {
    game_version(loader_version).as_deref() == Some(minecraft)
}

pub(crate) fn list_versions(client: &Client, minecraft: &str) -> io::Result<Vec<String>> {
    // NeoForge for 1.20.1 is a separate legacy Forge artifact.
    if matches!(minecraft, "1.20" | "1.20.1") {
        return Ok(Vec::new());
    }
    let list: MavenVersions = client
        .get(format!(
            "{ORIGIN}/api/maven/versions/releases/net/neoforged/neoforge"
        ))
        .timeout(Duration::from_secs(12))
        .send()
        .and_then(reqwest::blocking::Response::error_for_status)
        .and_then(reqwest::blocking::Response::json)
        .map_err(io::Error::other)?;
    Ok(list
        .versions
        .into_iter()
        .rev()
        .filter(|version| compatible(minecraft, version))
        .collect())
}

pub(crate) fn profile_id(loader_version: &str) -> String {
    format!("neoforge-{loader_version}")
}

pub(crate) fn is_loader_library(name: &str) -> bool {
    name.starts_with("net.neoforged:neoforge:")
        || name.starts_with("net.neoforged.fancymodloader:loader:")
}

pub(crate) fn install(
    client: &Client,
    root: &Path,
    minecraft: &str,
    loader_version: &str,
    configured_java: &str,
    managed_java: &Path,
    mut report: impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
) -> io::Result<()> {
    if !safe_component(minecraft) || !compatible(minecraft, loader_version) {
        return Err(invalid("所选 NeoForge 版本与 Minecraft 版本不兼容。"));
    }

    let versions_dir = root.join("versions");
    let profile_id = profile_id(loader_version);
    let destination = versions_dir.join(&profile_id);
    ensure_absent(&destination)?;
    fs::create_dir_all(&versions_dir)?;
    let staging = tempfile::Builder::new()
        .prefix(".mcug-neoforge-")
        .tempdir_in(&versions_dir)?;
    let work = staging.path();
    let installer = work.join("installer.jar");
    let installer_url = format!(
        "{ORIGIN}/releases/net/neoforged/neoforge/{loader_version}/{profile_id}-installer.jar"
    );

    report("installerDownload", None)?;
    let checksum = client
        .get(format!("{installer_url}.sha1"))
        .send()
        .and_then(reqwest::blocking::Response::error_for_status)
        .and_then(reqwest::blocking::Response::text)
        .map_err(io::Error::other)?;
    let sha1 = checksum.split_whitespace().next().unwrap_or_default();
    download_file(
        client,
        &installer_url,
        &installer,
        &FileIntegrity { sha1, size: None },
    )?;
    let profile = read_installer_profile(&installer)?;
    validate_profile(&profile, &profile_id, minecraft)?;

    let base = versions_dir.join(minecraft);
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
    run_installer(&java.executable, &installer, &launcher, &mut report)?;

    let installed = launcher.join("versions").join(&profile_id);
    let installed_json = installed.join(format!("{profile_id}.json"));
    let profile: VersionProfile =
        serde_json::from_reader(fs::File::open(installed_json)?).map_err(io::Error::from)?;
    validate_profile(&profile, &profile_id, minecraft)?;

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

fn validate_profile(profile: &VersionProfile, id: &str, minecraft: &str) -> io::Result<()> {
    if profile.id != id
        || profile.inherits_from != minecraft
        || !profile
            .libraries
            .iter()
            .any(|library| is_loader_library(&library.name))
    {
        return Err(invalid("NeoForge 安装器的版本信息与请求不一致。"));
    }
    Ok(())
}

fn run_installer(
    java: &Path,
    installer: &Path,
    launcher: &Path,
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
        .arg("--install-client")
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
                    "NeoForge 安装器退出（{status}）：{tail}"
                )));
            }
            if Instant::now() >= deadline {
                return Err(io::Error::new(
                    io::ErrorKind::TimedOut,
                    "NeoForge 安装器运行超时。",
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

fn publish_libraries(
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
                                format!("NeoForge 支持库与已存在的文件冲突：{}", output.display()),
                            ));
                        }
                    }
                    Err(error) => return Err(error),
                }
            } else {
                return Err(invalid("NeoForge 支持库包含非普通文件。"));
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

fn ensure_absent(path: &Path) -> io::Result<()> {
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn maps_maven_versions_to_minecraft_release_ids() {
        for (loader, game) in [
            ("20.4.251", "1.20.4"),
            ("21.0.167", "1.21"),
            ("21.1.267", "1.21.1"),
            ("26.3.0.118", "26.3"),
            ("26.1.2.130", "26.1.2"),
            ("26.1.0.0-alpha.1+snapshot-1", "26.1-snapshot-1"),
            ("0.25w14craftmine.3-beta", "25w14craftmine"),
        ] {
            assert!(compatible(game, loader), "{loader} should match {game}");
        }
        assert!(!compatible("1.21.10", "21.1.267"));
        assert!(!compatible("1.20.1", "1.20.1-47.1.99"));
        assert!(!compatible("1.21.1", "../21.1.267"));
    }
}
