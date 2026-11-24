//! 原版安装时按需获取版本描述。请在后台线程调用。

use std::{fs, io, path::Path};

use reqwest::blocking::Client;
use serde_json::Value;

use super::{
    file_download::{FileIntegrity, download_file},
    game_sources::GameVersion,
};

/// 下载并解析选中版本的 JSON。调用方提供本次安装的暂存文件路径。
pub fn download_version_metadata(
    client: &Client,
    version: &GameVersion,
    destination: &Path,
) -> io::Result<Value> {
    let integrity = FileIntegrity {
        size: None,
        sha1: &version.sha1,
    };
    download_file(client, &version.url, destination, &integrity)?;

    let metadata: Value = serde_json::from_slice(&fs::read(destination)?)
        .map_err(|error| io::Error::new(io::ErrorKind::InvalidData, error))?;
    if metadata.get("id").and_then(Value::as_str) != Some(version.id.as_str()) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "版本 JSON 的 ID 与清单不一致。",
        ));
    }
    Ok(metadata)
}
