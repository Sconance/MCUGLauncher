//! Fabric 和 Quilt 的官方 Meta API 与继承式客户端配置。

use std::{fs, io, path::Path, time::Duration};

use reqwest::{Url, blocking::Client};
use serde::Deserialize;
use serde_json::{Value, json};

use super::{
    loader_install::{ensure_absent, ensure_base, fetch_maven_file, publish_libraries},
    vanilla_download::safe_component,
};

#[derive(Clone, Copy)]
pub(crate) enum MetaLoader {
    Fabric,
    Quilt,
}

#[derive(Deserialize)]
struct VersionEntry {
    loader: LoaderEntry,
}

#[derive(Deserialize)]
struct LoaderEntry {
    version: String,
}

impl MetaLoader {
    pub(crate) fn name(self) -> &'static str {
        match self {
            Self::Fabric => "Fabric",
            Self::Quilt => "Quilt",
        }
    }

    fn api(self) -> &'static str {
        match self {
            Self::Fabric => "https://meta.fabricmc.net/v2/versions/loader",
            Self::Quilt => "https://meta.quiltmc.org/v3/versions/loader",
        }
    }

    fn marker(self) -> &'static str {
        match self {
            Self::Fabric => "net.fabricmc:fabric-loader:",
            Self::Quilt => "org.quiltmc:quilt-loader:",
        }
    }

    pub(crate) fn profile_id(self, game: &str, version: &str) -> String {
        format!(
            "{}-loader-{version}-{game}",
            self.name().to_ascii_lowercase()
        )
    }

    pub(crate) fn list_versions(self, client: &Client, game: &str) -> io::Result<Vec<String>> {
        let response = client
            .get(api_url(self.api(), &[game]))
            .timeout(Duration::from_secs(12))
            .send()
            .map_err(io::Error::other)?;
        if response.status() == reqwest::StatusCode::NOT_FOUND {
            return Ok(Vec::new());
        }
        let entries: Vec<VersionEntry> = response
            .error_for_status()
            .and_then(reqwest::blocking::Response::json)
            .map_err(io::Error::other)?;
        Ok(entries
            .into_iter()
            .map(|entry| entry.loader.version)
            .filter(|version| safe_component(version))
            .collect())
    }

    pub(crate) fn install(
        self,
        client: &Client,
        root: &Path,
        game: &str,
        version: &str,
        mut report: impl FnMut(&str, Option<(u64, u64)>) -> io::Result<()>,
    ) -> io::Result<()> {
        if !safe_component(game) || !safe_component(version) {
            return Err(invalid("游戏或加载器版本无效。"));
        }
        let versions = root.join("versions");
        let id = self.profile_id(game, version);
        let destination = versions.join(&id);
        ensure_absent(&destination)?;
        report("loaderProfile", None)?;
        let mut profile: Value = client
            .get(api_url(self.api(), &[game, version, "profile", "json"]))
            .send()
            .and_then(reqwest::blocking::Response::error_for_status)
            .and_then(reqwest::blocking::Response::json)
            .map_err(io::Error::other)?;
        if profile["id"].as_str() != Some(&id)
            || profile["inheritsFrom"].as_str() != Some(game)
            || profile["mainClass"].as_str().is_none_or(str::is_empty)
        {
            return Err(invalid("加载器配置与请求的版本不一致。"));
        }
        let libraries = profile["libraries"]
            .as_array_mut()
            .ok_or_else(|| invalid("加载器缺少库清单。"))?;
        if !libraries.iter().any(|library| {
            library["name"]
                .as_str()
                .is_some_and(|name| name.starts_with(self.marker()))
        }) {
            return Err(invalid("加载器配置缺少客户端运行库。"));
        }

        ensure_base(client, root, game, &mut report)?;
        fs::create_dir_all(&versions)?;
        let staging = tempfile::Builder::new()
            .prefix(".mcug-loader-")
            .tempdir_in(&versions)?;
        let staged_libraries = staging.path().join("libraries");
        fs::create_dir(&staged_libraries)?;
        let count = libraries.len() as u64;
        for (position, library) in libraries.iter_mut().enumerate() {
            report("downloading", Some((position as u64, count)))?;
            let coordinate = library["name"]
                .as_str()
                .ok_or_else(|| invalid("加载器库缺少 Maven 坐标。"))?;
            let relative =
                maven_path(coordinate).ok_or_else(|| invalid("加载器库的 Maven 坐标无效。"))?;
            let base = library["url"]
                .as_str()
                .ok_or_else(|| invalid("加载器库缺少下载地址。"))?;
            let url = library_url(base, &relative)?;
            let size = library["size"].as_u64();
            let checksum = library["sha1"].as_str();
            let output = staged_libraries.join(&relative);
            let sha1 = fetch_maven_file(client, url.as_str(), &output, checksum, size)?;
            // Normalize the Meta API's Maven libraries to the common launch profile format.
            library["downloads"] = json!({"artifact": {
                "path": relative, "url": url.as_str(), "sha1": sha1, "size": fs::metadata(output)?.len()
            }});
        }

        let staged_profile = staging.path().join("profile");
        fs::create_dir(&staged_profile)?;
        let file = fs::File::create(staged_profile.join(format!("{id}.json")))?;
        serde_json::to_writer(file, &profile).map_err(io::Error::other)?;
        publish_libraries(&staged_libraries, &root.join("libraries"), &mut report)?;
        report("committing", None)?;
        ensure_absent(&destination)?;
        fs::rename(staged_profile, destination)
    }
}

fn api_url(base: &str, parts: &[&str]) -> Url {
    let mut url = Url::parse(base).expect("fixed Meta API URL");
    url.path_segments_mut()
        .expect("hierarchical URL")
        .extend(parts.iter().copied());
    url
}

fn maven_path(coordinate: &str) -> Option<String> {
    let fields: Vec<_> = coordinate.split(':').collect();
    let (group, artifact, version, classifier) = match fields.as_slice() {
        [group, artifact, version] => (*group, *artifact, *version, ""),
        [group, artifact, version, classifier] => (*group, *artifact, *version, *classifier),
        _ => return None,
    };
    if !group.split('.').all(safe_component)
        || !safe_component(artifact)
        || !safe_component(version)
        || (!classifier.is_empty() && !safe_component(classifier))
    {
        return None;
    }
    let suffix = if classifier.is_empty() {
        String::new()
    } else {
        format!("-{classifier}")
    };
    Some(format!(
        "{}/{artifact}/{version}/{artifact}-{version}{suffix}.jar",
        group.replace('.', "/")
    ))
}

fn library_url(base: &str, relative: &str) -> io::Result<Url> {
    let base = Url::parse(base).map_err(io::Error::other)?;
    if base.scheme() != "https"
        || !matches!(
            base.host_str(),
            Some(
                "maven.fabricmc.net"
                    | "maven.quiltmc.org"
                    | "libraries.minecraft.net"
                    | "repo.maven.apache.org"
            )
        )
    {
        return Err(invalid("加载器库地址不在支持的 HTTPS Maven 仓库。"));
    }
    let mut url = base;
    url.path_segments_mut()
        .map_err(|_| invalid("无效的 Maven 仓库路径。"))?
        .pop_if_empty()
        .extend(relative.split('/'));
    Ok(url)
}

fn invalid(message: &str) -> io::Error {
    io::Error::new(io::ErrorKind::InvalidData, message)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn coordinates_preserve_classifier_and_reject_invalid_paths() {
        assert_eq!(
            maven_path("net.fabricmc:intermediary:1.21.4:v2").as_deref(),
            Some("net/fabricmc/intermediary/1.21.4/intermediary-1.21.4-v2.jar")
        );
        assert!(maven_path("net..fabricmc:intermediary:1.21.4").is_none());
        assert!(maven_path("net.fabricmc:intermediary:../bad").is_none());
    }
}
