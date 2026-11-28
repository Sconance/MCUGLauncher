use std::{collections::BTreeMap, fs, io, path::Path};

use serde::Deserialize;
use serde_json::Value;

use crate::core::neoforge::is_loader_library as is_neoforge_library;
use crate::model::{InstanceCatalogInfo, InstanceInfo, VersionChoice, VersionGroupInfo};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct VersionMetadata {
    id: String,
    inherits_from: Option<String>,
    client_version: Option<String>,
    jar: Option<String>,
    #[serde(default)]
    patches: Vec<VersionPatch>,
    #[serde(default)]
    arguments: Option<VersionArguments>,
    #[serde(default)]
    libraries: Vec<Library>,
}

#[derive(Deserialize)]
struct VersionPatch {
    id: String,
    version: Option<String>,
}

#[derive(Deserialize)]
struct VersionArguments {
    #[serde(default)]
    game: Vec<Value>,
}

#[derive(Deserialize)]
struct Library {
    name: String,
}

fn non_empty(value: Option<&str>) -> Option<&str> {
    value.map(str::trim).filter(|s| !s.is_empty())
}

fn looks_like_mc_version(value: &str) -> bool {
    let bytes = value.as_bytes();
    if bytes.is_empty() || !bytes[0].is_ascii_digit() {
        return false;
    }
    // 标准版本号（如 1.20.1、1.21、26.1）或快照（如 24w14a）
    let has_dot = value.contains('.');
    let is_snapshot = bytes.len() >= 5 && bytes[2] == b'w';
    (has_dot || is_snapshot)
        && value
            .chars()
            .all(|ch| ch.is_ascii_alphanumeric() || matches!(ch, '.' | '-' | '_'))
}

fn version_from_libraries(libraries: &[Library]) -> Option<String> {
    for library in libraries {
        let mut parts = library.name.split(':');
        let (Some(group), Some(artifact), Some(version)) =
            (parts.next(), parts.next(), parts.next())
        else {
            continue;
        };
        match (group, artifact) {
            ("net.fabricmc", "intermediary")
            | ("org.quiltmc", "hashed")
            | ("net.minecraft", "client") => {
                if let Some(ver) = non_empty(Some(version)) {
                    return Some(ver.to_string());
                }
            }
            ("net.minecraftforge", "forge") | ("net.minecraftforge", "fmlloader") => {
                if let Some(mc_ver) = version.split('-').next().and_then(|s| non_empty(Some(s))) {
                    if looks_like_mc_version(mc_ver) {
                        return Some(mc_ver.to_string());
                    }
                }
            }
            _ => {}
        }
    }
    None
}

fn version_from_game_args(arguments: Option<&VersionArguments>) -> Option<String> {
    let args = &arguments?.game;
    let mut iter = args.iter();
    while let Some(item) = iter.next() {
        if item.as_str() == Some("--fml.mcVersion") {
            if let Some(next) = iter.next().and_then(Value::as_str) {
                if let Some(ver) = non_empty(Some(next)) {
                    return Some(ver.to_string());
                }
            }
        }
    }
    None
}

fn leading_mc_version(id: &str) -> Option<&str> {
    let head = id
        .split(|ch: char| ch == '-' || ch == ' ' || ch == '_')
        .next()?;
    looks_like_mc_version(head).then_some(head)
}

fn resolve_minecraft_version(metadata: &VersionMetadata) -> String {
    if let Some(ver) = non_empty(metadata.inherits_from.as_deref()) {
        return ver.to_string();
    }
    if let Some(ver) = non_empty(metadata.client_version.as_deref()) {
        return ver.to_string();
    }
    if let Some(ver) = metadata.patches.iter().find_map(|patch| {
        matches!(patch.id.as_str(), "game" | "net.minecraft")
            .then(|| non_empty(patch.version.as_deref()))
            .flatten()
    }) {
        return ver.to_string();
    }
    if let Some(ver) = version_from_libraries(&metadata.libraries) {
        return ver;
    }
    if let Some(ver) = version_from_game_args(metadata.arguments.as_ref()) {
        return ver;
    }
    if let Some(jar) = non_empty(metadata.jar.as_deref()) {
        if jar != metadata.id && looks_like_mc_version(jar) {
            return jar.to_string();
        }
    }
    if let Some(ver) = leading_mc_version(&metadata.id) {
        return ver.to_string();
    }
    metadata.id.clone()
}

fn loader(metadata: &VersionMetadata) -> &'static str {
    metadata
        .libraries
        .iter()
        .find_map(|library| match library.name.split(':').next()? {
            "net.fabricmc" if library.name.starts_with("net.fabricmc:fabric-loader:") => {
                Some("Fabric")
            }
            "org.quiltmc" if library.name.starts_with("org.quiltmc:quilt-loader:") => Some("Quilt"),
            "net.minecraftforge" if crate::core::forge::is_loader_library(&library.name) => {
                Some("Forge")
            }
            _ if is_neoforge_library(&library.name) => Some("NeoForge"),
            "optifine" => Some("OptiFine"),
            _ => None,
        })
        .or_else(|| {
            metadata.patches.iter().find_map(|patch| match patch.id.as_str() {
                "fabric" => Some("Fabric"),
                "quilt" => Some("Quilt"),
                "forge" => Some("Forge"),
                "neoforge" => Some("NeoForge"),
                "optifine" => Some("OptiFine"),
                _ => None,
            })
        })
        .unwrap_or("Vanilla")
}

pub(crate) fn empty_catalog() -> InstanceCatalogInfo {
    InstanceCatalogInfo {
        items: Vec::new(),
        version_groups: Vec::new(),
    }
}

pub(crate) fn scan(root: &Path) -> io::Result<InstanceCatalogInfo> {
    if root.as_os_str().is_empty() {
        return Ok(empty_catalog());
    }
    let entries = match fs::read_dir(root.join("versions")) {
        Ok(entries) => entries,
        Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(empty_catalog()),
        Err(error) => return Err(error),
    };
    let canonical_root = root.canonicalize()?;
    let mut items = Vec::new();
    let mut groups = BTreeMap::<String, Vec<VersionChoice>>::new();
    for entry in entries.flatten() {
        if !entry.file_type().is_ok_and(|kind| kind.is_dir()) {
            continue;
        }
        let Ok(name) = entry.file_name().into_string() else {
            continue;
        };
        let manifest = entry.path().join(format!("{name}.json"));
        let metadata = fs::read(&manifest).and_then(|bytes| {
            serde_json::from_slice::<VersionMetadata>(&bytes).map_err(io::Error::from)
        });
        let metadata = match metadata {
            Ok(metadata) if !metadata.id.is_empty() => metadata,
            Ok(_) => continue,
            Err(error) => {
                if error.kind() != io::ErrorKind::NotFound {
                    eprintln!("[mcugl] 无法读取版本文件 {}：{error}", manifest.display());
                }
                continue;
            }
        };
        let loader = loader(&metadata);
        let minecraft = resolve_minecraft_version(&metadata);
        let instance_path = canonical_root.join("versions").join(&name);
        let Some(instance_path) = instance_path.to_str() else {
            continue;
        };
        groups
            .entry(minecraft.clone())
            .or_default()
            .push(VersionChoice {
                name: name.clone(),
                meta: loader.into(),
            });
        items.push(InstanceInfo {
            id: format!("local:{instance_path}"),
            display_name: name.clone(),
            name,
            minecraft,
            loader: loader.into(),
            mods: 0,
            updates: 0,
            is_placeholder: false,
        });
    }
    items.sort_by(|left, right| left.name.cmp(&right.name));
    let version_groups = groups
        .into_iter()
        .map(|(label, mut versions)| {
            versions.sort_by(|left, right| left.name.cmp(&right.name));
            VersionGroupInfo {
                label,
                count: versions.len() as u64,
                versions,
            }
        })
        .collect();
    Ok(InstanceCatalogInfo {
        items,
        version_groups,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn scans_each_root_independently_and_skips_bad_metadata() {
        let temp = tempfile::tempdir().unwrap();
        let first = temp.path().join("first");
        let second = temp.path().join("second");
        for root in [&first, &second] {
            let version = root.join("versions/example");
            fs::create_dir_all(&version).unwrap();
            fs::write(
                version.join("example.json"),
                r#"{"id":"example","inheritsFrom":"1.21.1","libraries":[{"name":"net.fabricmc:fabric-loader:0.16.9"}]}"#,
            )
            .unwrap();
        }
        let bad = first.join("versions/invalid");
        fs::create_dir_all(&bad).unwrap();
        fs::write(bad.join("invalid.json"), "{ invalid").unwrap();

        let first_catalog = scan(&first).unwrap();
        let second_catalog = scan(&second).unwrap();
        assert_eq!(first_catalog.items.len(), 1);
        assert_eq!(second_catalog.items.len(), 1);
        assert_eq!(first_catalog.items[0].minecraft, "1.21.1");
        assert_eq!(first_catalog.items[0].loader, "Fabric");
        assert_ne!(first_catalog.items[0].id, second_catalog.items[0].id);
        assert_eq!(first_catalog.items[0].id, scan(&first).unwrap().items[0].id);
    }

    #[test]
    fn resolves_minecraft_version_from_merged_manifests() {
        let temp = tempfile::tempdir().unwrap();
        let versions = temp.path().join("versions");

        let cases = [
            (
                "Fabulously Optimized-26.3",
                r#"{"id":"Fabulously Optimized-26.3","patches":[{"id":"game","version":"1.21.4"},{"id":"fabric","version":"0.16.10"}],"libraries":[{"name":"net.fabricmc:fabric-loader:0.16.10"}]}"#,
                "1.21.4",
                "Fabric",
            ),
            (
                "FabricPack",
                r#"{"id":"FabricPack","libraries":[{"name":"net.fabricmc:fabric-loader:0.16.10"},{"name":"net.fabricmc:intermediary:1.21.1"}]}"#,
                "1.21.1",
                "Fabric",
            ),
            (
                "1.10.2-Forge_12.18.3.2511-OptiFine_I5",
                r#"{"id":"1.10.2-Forge_12.18.3.2511-OptiFine_I5","libraries":[{"name":"net.minecraftforge:forge:1.10.2-12.18.3.2511"}]}"#,
                "1.10.2",
                "Forge",
            ),
            (
                "PCL-Instance",
                r#"{"id":"PCL-Instance","clientVersion":"1.20.1","libraries":[{"name":"net.neoforged.fancymodloader:loader:2.0.0"}]}"#,
                "1.20.1",
                "NeoForge",
            ),
        ];

        for (dir_name, content, _, _) in cases {
            let dir = versions.join(dir_name);
            fs::create_dir_all(&dir).unwrap();
            fs::write(dir.join(format!("{dir_name}.json")), content).unwrap();
        }

        let catalog = scan(temp.path()).unwrap();
        for (dir_name, _, expected_mc, expected_loader) in cases {
            let item = catalog
                .items
                .iter()
                .find(|entry| entry.name == dir_name)
                .unwrap_or_else(|| panic!("missing {dir_name}"));
            assert_eq!(item.minecraft, expected_mc, "for {dir_name}");
            assert_eq!(item.loader, expected_loader, "for {dir_name}");
        }
    }
}
