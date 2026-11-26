//! 读取和安装 Modrinth 客户端整合包中的文件。

use std::{
    collections::{BTreeMap, HashSet},
    fs::{self, File},
    io::{self, Read, Seek, SeekFrom},
    path::{Path, PathBuf},
    time::Duration,
};

use reqwest::{Url, blocking::Client, redirect};
use serde::Deserialize;
use sha2::{Digest, Sha256, Sha512};
use zip::ZipArchive;

use super::{
    file_download::{FileIntegrity, stage_file},
    vanilla_download::safe_component,
};

const INDEX: &str = "modrinth.index.json";
const MAX_INDEX_BYTES: u64 = 8 * 1024 * 1024;
const MAX_OVERRIDES_BYTES: u64 = 16 * 1024 * 1024 * 1024;

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub(crate) struct Manifest {
    format_version: u32,
    game: String,
    pub(crate) name: String,
    pub(crate) dependencies: BTreeMap<String, String>,
    files: Vec<PackFile>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct PackFile {
    path: String,
    downloads: Vec<String>,
    file_size: u64,
    hashes: Hashes,
    env: Option<Environment>,
}

#[derive(Deserialize)]
struct Hashes {
    sha1: String,
    sha512: String,
}

#[derive(Deserialize)]
struct Environment {
    client: ClientSupport,
}

#[derive(Deserialize)]
#[serde(rename_all = "lowercase")]
enum ClientSupport {
    Required,
    Optional,
    Unsupported,
}

pub(crate) struct Archive {
    zip: ZipArchive<File>,
    overrides: Vec<(u8, usize, PathBuf)>,
    pub(crate) manifest: Manifest,
    pub(crate) fingerprint: [u8; 32],
}

impl Archive {
    pub(crate) fn open(path: &Path) -> io::Result<Self> {
        if !path.metadata()?.is_file() {
            return Err(invalid("整合包路径必须指向普通文件。"));
        }
        let mut file = File::open(path)?;
        if !file.metadata()?.is_file() {
            return Err(invalid("整合包路径必须指向普通文件。"));
        }
        let mut hash = Sha256::new();
        io::copy(&mut file, &mut hash)?;
        file.seek(SeekFrom::Start(0))?;
        let mut zip = ZipArchive::new(file).map_err(invalid)?;
        let mut manifest: Manifest = {
            let index = zip.by_name(INDEX).map_err(invalid)?;
            if index.size() > MAX_INDEX_BYTES {
                return Err(invalid("整合包清单超过大小限制。"));
            }
            serde_json::from_reader(index.take(MAX_INDEX_BYTES + 1)).map_err(invalid)?
        };
        if manifest.format_version != 1 || manifest.game != "minecraft" {
            return Err(invalid(
                "仅支持 Minecraft 的 Modrinth formatVersion 1 整合包。",
            ));
        }
        manifest.files.retain(|file| {
            file.env
                .as_ref()
                .is_none_or(|env| matches!(env.client, ClientSupport::Required))
        });
        let mut paths = HashSet::new();
        for file in &manifest.files {
            pack_path(&file.path)?;
            if !paths.insert(file.path.to_ascii_lowercase()) {
                return Err(invalid(format!("重复的下载路径：{}", file.path)));
            }
            if file.downloads.is_empty()
                || !file
                    .downloads
                    .iter()
                    .any(|url| allowed_download(url).is_some())
                || !valid_hash(&file.hashes.sha1, 40)
                || !valid_hash(&file.hashes.sha512, 128)
            {
                return Err(invalid(format!("缺少有效的下载地址或哈希：{}", file.path)));
            }
        }

        let mut paths = HashSet::new();
        let mut overrides = Vec::new();
        let mut total_bytes = 0u64;
        for index in 0..zip.len() {
            let entry = zip.by_index(index).map_err(invalid)?;
            let Some((layer, relative)) = entry
                .name()
                .strip_prefix("overrides/")
                .map(|path| (0, path))
                .or_else(|| {
                    entry
                        .name()
                        .strip_prefix("client-overrides/")
                        .map(|path| (1, path))
                })
            else {
                continue;
            };
            if entry.is_dir() {
                continue;
            }
            if !entry.is_file() {
                return Err(invalid("覆盖目录只允许普通文件。"));
            }
            let path = pack_path(relative)?;
            if !paths.insert((layer, relative.to_ascii_lowercase())) {
                return Err(invalid(format!("重复的覆盖文件：{relative}")));
            }
            total_bytes = total_bytes.saturating_add(entry.size());
            if total_bytes > MAX_OVERRIDES_BYTES {
                return Err(invalid("覆盖文件解压后超过大小限制。"));
            }
            overrides.push((layer, index, path));
        }
        overrides.sort_unstable_by_key(|&(layer, _, _)| layer);
        Ok(Self {
            zip,
            overrides,
            manifest,
            fingerprint: hash.finalize().into(),
        })
    }

    /// 安装到调用方独占的暂存目录。普通覆盖先应用，客户端覆盖后应用。
    pub(crate) fn install_files(
        &mut self,
        root: &Path,
        report: &impl Fn(&str, Option<(u64, u64)>) -> io::Result<()>,
    ) -> io::Result<()> {
        let client = Client::builder()
            .timeout(Duration::from_secs(90))
            .user_agent(concat!("MCUGLauncher/", env!("CARGO_PKG_VERSION")))
            .redirect(redirect::Policy::custom(|attempt| {
                if attempt.previous().len() <= 5 && allowed_url(attempt.url(), true) {
                    attempt.follow()
                } else {
                    attempt.error("整合包文件重定向不在允许范围内")
                }
            }))
            .build()
            .map_err(io::Error::other)?;
        let total = self.manifest.files.len() as u64;
        for (index, file) in self.manifest.files.iter().enumerate() {
            report("downloadingPack", Some((index as u64, total)))?;
            file.download(&client, root)?;
        }
        self.extract_overrides(root, report)
    }

    fn extract_overrides(
        &mut self,
        root: &Path,
        report: &impl Fn(&str, Option<(u64, u64)>) -> io::Result<()>,
    ) -> io::Result<()> {
        let total = self.overrides.len() as u64;
        for (position, (_, index, relative)) in self.overrides.iter().enumerate() {
            report("extractingPack", Some((position as u64, total)))?;
            let entry = self.zip.by_index(*index).map_err(invalid)?;
            let size = entry.size();
            let destination = root.join(relative);
            fs::create_dir_all(destination.parent().expect("pack file has parent"))?;
            let mut file = File::create(destination)?;
            if io::copy(&mut entry.take(size + 1), &mut file)? != size {
                return Err(invalid("覆盖文件大小与 ZIP 清单不一致。"));
            }
        }
        Ok(())
    }
}

impl PackFile {
    fn download(&self, client: &Client, root: &Path) -> io::Result<()> {
        let destination = root.join(pack_path(&self.path)?);
        let integrity = FileIntegrity {
            size: Some(self.file_size),
            sha1: &self.hashes.sha1,
        };
        let mut last_error = invalid("没有可用的整合包文件下载地址。");
        for url in self
            .downloads
            .iter()
            .filter_map(|url| allowed_download(url))
        {
            let result = (|| {
                let mut staged = stage_file(
                    client,
                    url.as_str(),
                    destination.parent().expect("pack file has parent"),
                    &integrity,
                )?;
                let mut hash = Sha512::new();
                staged.as_file_mut().rewind()?;
                io::copy(staged.as_file_mut(), &mut hash)?;
                if !self
                    .hashes
                    .sha512
                    .eq_ignore_ascii_case(&format!("{:x}", hash.finalize()))
                {
                    return Err(invalid("整合包文件 SHA-512 校验失败。"));
                }
                staged
                    .persist_noclobber(&destination)
                    .map_err(|error| error.error)?;
                Ok(())
            })();
            match result {
                Ok(()) => return Ok(()),
                Err(error) => last_error = error,
            }
        }
        Err(last_error)
    }
}

fn pack_path(path: &str) -> io::Result<PathBuf> {
    let mut components = path.split('/');
    let first = components.next().unwrap_or_default();
    if matches!(
        first.to_ascii_lowercase().as_str(),
        "versions" | "libraries" | "assets"
    ) || !safe_component(first)
        || !components.all(safe_component)
    {
        return Err(invalid(format!("无效的整合包文件路径：{path}")));
    }
    Ok(path.split('/').collect())
}

fn valid_hash(hash: &str, length: usize) -> bool {
    hash.len() == length && hash.bytes().all(|byte| byte.is_ascii_hexdigit())
}

fn allowed_download(value: &str) -> Option<Url> {
    Url::parse(value).ok().filter(|url| allowed_url(url, false))
}

fn allowed_url(url: &Url, redirect: bool) -> bool {
    url.scheme() == "https"
        && url.port().is_none()
        && url.username().is_empty()
        && url.password().is_none()
        && (matches!(
            url.host_str(),
            Some("cdn.modrinth.com" | "github.com" | "raw.githubusercontent.com" | "gitlab.com")
        ) || redirect
            && matches!(
                url.host_str(),
                Some("objects.githubusercontent.com" | "release-assets.githubusercontent.com")
            ))
}

fn invalid(message: impl std::fmt::Display) -> io::Error {
    io::Error::new(io::ErrorKind::InvalidData, message.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use zip::{ZipWriter, write::SimpleFileOptions};

    #[test]
    fn rejects_unsafe_paths_and_urls() {
        for path in [
            "/mods/a",
            "../outside",
            "mods/../a",
            "C:/a",
            "mods\\a",
            "Versions/a",
        ] {
            assert!(pack_path(path).is_err(), "{path}");
        }
        for url in [
            "http://cdn.modrinth.com/a",
            "https://cdn.modrinth.com.attacker.test/a",
            "https://cdn.modrinth.com:8443/a",
            "https://127.0.0.1/a",
        ] {
            assert!(allowed_download(url).is_none(), "{url}");
        }
        assert!(allowed_download("https://cdn.modrinth.com/a").is_some());
    }

    #[test]
    fn reads_archive_and_layers_client_over_shared_overrides() {
        let temp = tempfile::tempdir().unwrap();
        let path = temp.path().join("sample.mrpack");
        let mut zip = ZipWriter::new(File::create(&path).unwrap());
        for (name, bytes) in [
            (
                INDEX,
                r#"{
                "formatVersion":1,"game":"minecraft","name":"Sample",
                "dependencies":{"minecraft":"1.21.1","fabric-loader":"0.16.9"},
                "files":[{
                    "path":"mods/skip.jar","downloads":[],"fileSize":0,
                    "hashes":{"sha1":"","sha512":""},"env":{"client":"unsupported"}
                }]
            }"#,
            ),
            ("client-overrides/config/test.txt", "client"),
            ("server-overrides/config/test.txt", "server"),
            ("overrides/config/test.txt", "shared"),
        ] {
            zip.start_file(name, SimpleFileOptions::default()).unwrap();
            zip.write_all(bytes.as_bytes()).unwrap();
        }
        zip.finish().unwrap();
        let mut pack = Archive::open(&path).unwrap();
        assert_eq!(pack.manifest.name, "Sample");
        assert!(pack.manifest.files.is_empty());
        let root = temp.path().join("game");
        pack.install_files(&root, &|_, _| Ok(())).unwrap();
        assert_eq!(
            fs::read_to_string(root.join("config/test.txt")).unwrap(),
            "client"
        );
    }
}
