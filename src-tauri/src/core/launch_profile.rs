//! 将本地 Minecraft 版本继承链解析成通用的启动配置。

use std::{collections::HashSet, fs::File, io, path::Path};

use serde::Deserialize;

use super::vanilla_download::{Library, Rule, safe_component};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct ProfileMetadata {
    id: String,
    #[serde(rename = "type")]
    kind: Option<String>,
    main_class: Option<String>,
    inherits_from: Option<String>,
    jar: Option<String>,
    java_version: Option<JavaVersion>,
    asset_index: Option<AssetIndex>,
    assets: Option<String>,
    #[serde(default)]
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
pub(super) struct AssetIndex {
    pub(super) id: String,
}

#[derive(Deserialize)]
pub(super) struct Arguments {
    #[serde(default)]
    pub(super) jvm: Vec<Argument>,
    #[serde(default)]
    pub(super) game: Vec<Argument>,
}

#[derive(Deserialize)]
#[serde(untagged)]
pub(super) enum Argument {
    Text(String),
    Conditional {
        rules: Vec<Rule>,
        value: ArgumentValue,
    },
}

#[derive(Deserialize)]
#[serde(untagged)]
pub(super) enum ArgumentValue {
    Text(String),
    List(Vec<String>),
}

pub(crate) struct VersionProfile {
    pub(super) id: String,
    pub(super) kind: String,
    pub(super) main_class: String,
    pub(super) jar: String,
    pub(super) inherits: bool,
    required_java: u32,
    pub(super) asset_index: Option<AssetIndex>,
    pub(super) assets: Option<String>,
    pub(super) libraries: Vec<Library>,
    pub(super) arguments: Option<Arguments>,
    pub(super) minecraft_arguments: Option<String>,
}

impl VersionProfile {
    pub(crate) fn resolve(versions_dir: &Path, selected: &str) -> io::Result<Self> {
        let mut chain = Vec::new();
        let mut seen = HashSet::new();
        let mut current = selected.to_owned();
        loop {
            if !safe_component(&current) {
                return Err(invalid("版本或父版本 ID 无效。"));
            }
            if chain.len() >= 32 || !seen.insert(current.clone()) {
                return Err(invalid("版本继承链包含循环或超过长度上限。"));
            }
            let path = versions_dir.join(&current).join(format!("{current}.json"));
            let file = File::open(&path).map_err(|error| {
                io::Error::new(
                    error.kind(),
                    format!("无法读取版本 {}：{error}", path.display()),
                )
            })?;
            let metadata: ProfileMetadata = serde_json::from_reader(file)
                .map_err(|error| invalid(format!("无法解析版本 {}：{error}", path.display())))?;
            if !safe_component(&metadata.id)
                || metadata
                    .jar
                    .as_deref()
                    .is_some_and(|jar| !safe_component(jar))
            {
                return Err(invalid("版本 ID 或客户端 JAR ID 无效。"));
            }
            let parent = metadata.inherits_from.clone();
            chain.push((current, metadata));
            let Some(parent) = parent else { break };
            current = parent;
        }

        let (base_dir, base) = chain.pop().expect("the selected profile was read");
        let mut resolved = Self::new(base_dir, base);
        while let Some((_, child)) = chain.pop() {
            resolved.apply(child);
        }
        if resolved.main_class.trim().is_empty() {
            return Err(invalid("版本缺少 Java 主类。"));
        }
        Ok(resolved)
    }

    pub(crate) fn required_java(&self) -> u32 {
        self.required_java
    }

    fn new(base_dir: String, metadata: ProfileMetadata) -> Self {
        Self {
            id: metadata.id,
            kind: metadata.kind.unwrap_or_else(|| "release".into()),
            main_class: metadata.main_class.unwrap_or_default(),
            jar: metadata.jar.unwrap_or(base_dir),
            inherits: false,
            required_java: metadata
                .java_version
                .map_or(8, |version| version.major_version),
            asset_index: metadata.asset_index,
            assets: metadata.assets,
            libraries: metadata.libraries,
            arguments: metadata.arguments,
            minecraft_arguments: metadata.minecraft_arguments,
        }
    }

    fn apply(&mut self, metadata: ProfileMetadata) {
        self.inherits = true;
        self.id = metadata.id;
        if let Some(kind) = metadata.kind {
            self.kind = kind;
        }
        if let Some(main_class) = metadata.main_class {
            self.main_class = main_class;
        }
        if let Some(jar) = metadata.jar {
            self.jar = jar;
        }
        if let Some(java) = metadata.java_version {
            self.required_java = self.required_java.max(java.major_version);
        }
        if metadata.asset_index.is_some() {
            self.asset_index = metadata.asset_index;
        }
        if metadata.assets.is_some() {
            self.assets = metadata.assets;
        }
        self.libraries.extend(metadata.libraries);
        if let Some(arguments) = metadata.arguments {
            if let Some(existing) = &mut self.arguments {
                existing.jvm.extend(arguments.jvm);
                existing.game.extend(arguments.game);
            } else {
                self.arguments = Some(arguments);
            }
        }
        if metadata.minecraft_arguments.is_some() {
            self.minecraft_arguments = metadata.minecraft_arguments;
        }
    }
}

fn invalid(message: impl Into<String>) -> io::Error {
    io::Error::new(io::ErrorKind::InvalidData, message.into())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;

    fn write_profile(root: &Path, version: &str, data: serde_json::Value) {
        let directory = root.join(version);
        fs::create_dir_all(&directory).unwrap();
        fs::write(directory.join(format!("{version}.json")), data.to_string()).unwrap();
    }

    #[test]
    fn resolves_multiple_layers_and_preserves_parent_launch_data() {
        let dir = tempfile::tempdir().unwrap();
        let root = dir.path();
        write_profile(
            root,
            "base",
            serde_json::json!({
                "id": "base", "type": "release", "mainClass": "GameMain",
                "assetIndex": {"id": "assets-42"}, "javaVersion": {"majorVersion": 21},
                "arguments": {"game": ["--username", "${auth_player_name}"], "jvm": ["-cp", "${classpath}"]}
            }),
        );
        write_profile(
            root,
            "loader",
            serde_json::json!({
                "id": "loader", "inheritsFrom": "base", "mainClass": "LoaderMain",
                "arguments": {"jvm": ["-Dloader=true"]}
            }),
        );
        write_profile(
            root,
            "instance",
            serde_json::json!({
                "id": "instance", "inheritsFrom": "loader", "javaVersion": {"majorVersion": 25},
                "arguments": {"game": ["--loader"]}
            }),
        );
        let profile = VersionProfile::resolve(root, "instance").unwrap();
        assert_eq!(profile.id, "instance");
        assert_eq!(profile.main_class, "LoaderMain");
        assert_eq!(profile.jar, "base");
        assert_eq!(profile.required_java(), 25);
        assert_eq!(profile.asset_index.unwrap().id, "assets-42");
        let arguments = profile.arguments.unwrap();
        assert!(matches!(&arguments.jvm[2], Argument::Text(text) if text == "-Dloader=true"));
        assert!(matches!(&arguments.game[2], Argument::Text(text) if text == "--loader"));
    }

    #[test]
    fn rejects_cycles_and_invalid_parent_ids() {
        let dir = tempfile::tempdir().unwrap();
        let root = dir.path();
        write_profile(
            root,
            "a",
            serde_json::json!({"id": "a", "inheritsFrom": "b"}),
        );
        write_profile(
            root,
            "b",
            serde_json::json!({"id": "b", "inheritsFrom": "a"}),
        );
        assert!(VersionProfile::resolve(root, "a").is_err());
        write_profile(
            root,
            "b",
            serde_json::json!({"id": "b", "inheritsFrom": "../outside"}),
        );
        assert!(VersionProfile::resolve(root, "a").is_err());
    }
}
