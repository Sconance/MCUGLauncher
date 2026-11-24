//! 通用文件下载、校验与落盘。请在后台线程调用。

use std::{
    fs::{self, File},
    io::{self, Read},
    path::Path,
    thread,
    time::Duration,
};

use reqwest::blocking::Client;
use sha1::{Digest, Sha1};
use tempfile::NamedTempFile;

/// 来自下载清单的文件大小和 SHA-1。
pub struct FileIntegrity<'a> {
    pub size: Option<u64>,
    pub sha1: &'a str,
}

pub fn verify_file(path: &Path, integrity: &FileIntegrity<'_>) -> io::Result<()> {
    let mut file = File::open(path)?;
    if let Some(size) = integrity.size {
        let actual_size = file.metadata()?.len();
        if actual_size != size {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                format!("文件大小不符，预期 {size} 字节，实际 {actual_size} 字节。"),
            ));
        }
    }

    let mut hash = Sha1::new();
    io::copy(&mut file, &mut hash)?;
    if !integrity
        .sha1
        .eq_ignore_ascii_case(&format!("{:x}", hash.finalize()))
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "文件 SHA-1 校验失败。",
        ));
    }
    Ok(())
}

/// 复用通过校验的目标文件，否则下载到同目录临时文件，校验通过后再替换。
pub fn download_file(
    client: &Client,
    url: &str,
    destination: &Path,
    integrity: &FileIntegrity<'_>,
) -> io::Result<()> {
    if integrity.sha1.len() != 40 || !integrity.sha1.bytes().all(|byte| byte.is_ascii_hexdigit()) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "无效的 SHA-1 校验值。",
        ));
    }
    match verify_file(destination, integrity) {
        Ok(()) => return Ok(()),
        Err(error)
            if matches!(
                error.kind(),
                io::ErrorKind::NotFound | io::ErrorKind::InvalidData
            ) => {}
        Err(error) => return Err(error),
    }

    let directory = destination
        .parent()
        .filter(|path| !path.as_os_str().is_empty())
        .unwrap_or_else(|| Path::new("."));
    fs::create_dir_all(directory)?;

    let mut delays = [250, 1000].into_iter();
    loop {
        match download_once(client, url, directory, destination, integrity) {
            Err(error) if download_can_retry(&error) => {
                let Some(delay) = delays.next() else {
                    return Err(error);
                };
                thread::sleep(Duration::from_millis(delay));
            }
            result => return result,
        }
    }
}

fn download_can_retry(error: &io::Error) -> bool {
    let mut cause = error.get_ref().map(|cause| cause as &dyn std::error::Error);
    while let Some(inner) = cause {
        if let Some(request) = inner.downcast_ref::<reqwest::Error>() {
            return request.is_timeout()
                || request.is_connect()
                || request.is_request()
                || request.is_body()
                || request.status().is_some_and(|status| {
                    status.is_server_error() || matches!(status.as_u16(), 408 | 429)
                });
        }
        cause = inner.source();
    }
    false
}

fn download_once(
    client: &Client,
    url: &str,
    directory: &Path,
    destination: &Path,
    integrity: &FileIntegrity<'_>,
) -> io::Result<()> {
    let mut response = client
        .get(url)
        .send()
        .and_then(reqwest::blocking::Response::error_for_status)
        .map_err(io::Error::other)?;
    let mut temporary = NamedTempFile::new_in(directory)?;
    if let Some(size) = integrity.size {
        io::copy(
            &mut response.take(size.saturating_add(1)),
            temporary.as_file_mut(),
        )?;
    } else {
        io::copy(&mut response, temporary.as_file_mut())?;
    }
    verify_file(temporary.path(), integrity)?;
    temporary
        .persist(destination)
        .map_err(|error| error.error)?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{io::Write, net::TcpListener, thread, time::Duration};

    #[test]
    fn replaces_existing_file_only_after_verification() {
        let directory = tempfile::tempdir().unwrap();
        let destination = directory.path().join("client.jar");
        fs::write(&destination, b"old").unwrap();
        let integrity = FileIntegrity {
            size: Some(3),
            sha1: "a9993e364706816aba3e25717850c26c9cd0d89d",
        };
        let listener = TcpListener::bind("127.0.0.1:0").unwrap();
        let url = format!("http://{}/", listener.local_addr().unwrap());
        let server = thread::spawn(move || {
            for (status, body) in [
                ("200 OK", b"bad"),
                ("503 Service Unavailable", b"err"),
                ("200 OK", b"abc"),
            ] {
                let (mut stream, _) = listener.accept().unwrap();
                stream
                    .set_read_timeout(Some(Duration::from_secs(5)))
                    .unwrap();
                let mut request = Vec::new();
                while !request.ends_with(b"\r\n\r\n") {
                    let mut byte = [0];
                    stream.read_exact(&mut byte).unwrap();
                    request.extend(byte);
                }
                write!(
                    stream,
                    "HTTP/1.1 {status}\r\nContent-Length: 3\r\nConnection: close\r\n\r\n"
                )
                .unwrap();
                stream.write_all(body).unwrap();
            }
        });
        let client = Client::builder()
            .no_proxy()
            .timeout(Duration::from_secs(5))
            .build()
            .unwrap();

        let error = download_file(&client, &url, &destination, &integrity).unwrap_err();
        assert_eq!(error.kind(), io::ErrorKind::InvalidData);
        assert_eq!(fs::read(&destination).unwrap(), b"old");
        assert_eq!(fs::read_dir(directory.path()).unwrap().count(), 1);

        download_file(&client, &url, &destination, &integrity).unwrap();
        assert_eq!(fs::read(&destination).unwrap(), b"abc");
        server.join().unwrap();
        download_file(&client, "unused", &destination, &integrity).unwrap();
    }
}
