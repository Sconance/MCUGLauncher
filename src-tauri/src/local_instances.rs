use std::{collections::BTreeMap, fs, io, path::Path};

use serde::Deserialize;

use crate::model::{InstanceCatalogInfo, InstanceInfo, VersionChoice, VersionGroupInfo};

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct VersionMetadata {
    id: String,
    inherits_from: Option<String>,
    #[serde(default)]
    libraries: Vec<Library>,
}

#[derive(Deserialize)]
struct Library {
    name: String,
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
            "net.minecraftforge" if library.name.starts_with("net.minecraftforge:forge:") => {
                Some("Forge")
            }
            "net.neoforged" if library.name.starts_with("net.neoforged:neoforge:") => {
                Some("NeoForge")
            }
            "optifine" => Some("OptiFine"),
            _ => None,
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
        let minecraft = metadata.inherits_from.unwrap_or(metadata.id);
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
}
