//! 探测和选择本机 Java。探测命令应在后台线程运行。

use std::{
    collections::HashSet,
    env, fs, io,
    path::{Path, PathBuf},
    process::Command,
};

#[derive(Debug)]
pub struct JavaRuntime {
    pub executable: PathBuf,
    pub major: u32,
    pub arch: String,
    pub vendor: String,
    pub os_version: String,
}

pub fn detect_java(executable: &Path) -> io::Result<JavaRuntime> {
    let executable = executable.canonicalize()?;
    let mut command = Command::new(&executable);
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000);
    }
    let output = command
        .args(["-XshowSettings:properties", "-version"])
        .output()?;
    if !output.status.success() {
        return Err(io::Error::other(format!(
            "Java 检测失败：{}",
            executable.display()
        )));
    }
    let stdout = String::from_utf8_lossy(&output.stdout);
    let stderr = String::from_utf8_lossy(&output.stderr);
    let property = |key: &str| {
        stderr
            .lines()
            .chain(stdout.lines())
            .filter_map(|line| line.trim().split_once(" = "))
            .find_map(|(name, value)| (name == key).then_some(value))
            .unwrap_or_default()
            .to_owned()
    };
    let version = property("java.version");
    let major = java_major(&version)
        .ok_or_else(|| io::Error::new(io::ErrorKind::InvalidData, "无法识别 Java 主版本。"))?;
    Ok(JavaRuntime {
        executable,
        major,
        arch: property("os.arch"),
        vendor: property("java.vendor"),
        os_version: property("os.version"),
    })
}

pub(super) fn java_major(version: &str) -> Option<u32> {
    let number = version.strip_prefix("1.").unwrap_or(version);
    let end = number
        .find(|ch: char| !ch.is_ascii_digit())
        .unwrap_or(number.len());
    number[..end].parse().ok()
}

pub(super) fn managed_java_executable(home: &Path) -> PathBuf {
    if cfg!(target_os = "macos") {
        home.join("jre.bundle/Contents/Home/bin/java")
    } else {
        home.join(if cfg!(windows) {
            "bin/java.exe"
        } else {
            "bin/java"
        })
    }
}

pub fn scan_java_runtimes(configured: &str, managed_dir: &Path) -> Vec<JavaRuntime> {
    let name = if cfg!(windows) { "java.exe" } else { "java" };
    let mut candidates = Vec::new();
    if !configured.trim().is_empty() && configured != "auto" {
        candidates.push(PathBuf::from(configured));
    }
    if let Some(home) = env::var_os("JAVA_HOME") {
        candidates.push(PathBuf::from(home).join("bin").join(name));
    }
    if let Some(path) = env::var_os("PATH") {
        candidates.extend(env::split_paths(&path).map(|directory| directory.join(name)));
    }
    for entry in fs::read_dir(managed_dir).into_iter().flatten().flatten() {
        if entry
            .file_name()
            .to_str()
            .is_some_and(|name| name.starts_with("java-"))
        {
            candidates.push(managed_java_executable(&entry.path()));
        }
    }
    let mut roots = Vec::new();
    #[cfg(target_os = "linux")]
    roots.push(PathBuf::from("/usr/lib/jvm"));
    #[cfg(target_os = "macos")]
    roots.push(PathBuf::from("/Library/Java/JavaVirtualMachines"));
    if let Some(home) = env::home_dir() {
        roots.push(home.join(".sdkman/candidates/java"));
        #[cfg(target_os = "macos")]
        roots.push(home.join("Library/Java/JavaVirtualMachines"));
    }
    #[cfg(windows)]
    for program_files in [
        env::var_os("ProgramFiles"),
        env::var_os("ProgramFiles(x86)"),
    ]
    .into_iter()
    .flatten()
    {
        let base = PathBuf::from(program_files);
        roots.extend(
            ["Java", "Eclipse Adoptium", "Microsoft", "Zulu"].map(|vendor| base.join(vendor)),
        );
    }
    for directory in roots {
        for entry in fs::read_dir(directory).into_iter().flatten().flatten() {
            let home = entry.path();
            candidates.push(home.join("bin").join(name));
            #[cfg(target_os = "macos")]
            candidates.push(home.join("Contents/Home/bin").join(name));
        }
    }
    let mut seen = HashSet::new();
    let mut runtimes: Vec<_> = candidates
        .into_iter()
        .filter_map(|path| path.canonicalize().ok())
        .filter(|path| path.is_file() && seen.insert(path.clone()))
        .filter_map(|path| detect_java(&path).ok())
        .collect();
    runtimes.sort_by_key(|runtime| runtime.major);
    runtimes
}

pub fn select_java(
    configured: &str,
    required_major: u32,
    managed_dir: &Path,
) -> io::Result<JavaRuntime> {
    if !configured.trim().is_empty() && configured != "auto" {
        let selected = detect_java(Path::new(configured)).map_err(|error| {
            io::Error::new(error.kind(), format!("无法使用已配置的 Java：{error}"))
        })?;
        if selected.major < required_major {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                format!(
                    "该版本至少需要 Java {required_major}，已配置的版本是 Java {}。",
                    selected.major,
                ),
            ));
        }
        return Ok(selected);
    }
    scan_java_runtimes("", managed_dir)
        .into_iter()
        .find(|runtime| runtime.major >= required_major)
        .ok_or_else(|| {
            io::Error::new(
                io::ErrorKind::NotFound,
                format!("没有找到 Java {required_major} 或更高版本。"),
            )
        })
}
