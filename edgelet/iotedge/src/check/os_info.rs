/// A subset of the fields from /etc/os-release.
///
/// Examples:
///
/// ```ignore
///  OS                  | id                  | version_id
/// ---------------------+---------------------+------------
///  CentOS 7            | centos              | 7
///  Debian 9            | debian              | 9
///  openSUSE Tumbleweed | opensuse-tumbleweed | 20190325
///  Ubuntu 18.04        | ubuntu              | 18.04
///  Windows 10          | windows             | 10.0.17763
/// ```
///
/// Ref: <https://www.freedesktop.org/software/systemd/man/os-release.html>
#[derive(Clone, Debug, serde_derive::Serialize)]
pub(super) struct OsInfo {
    id: Option<String>,
    version_id: Option<String>,
    bitness: usize,
}

impl OsInfo {
    #[cfg(windows)]
    pub(super) fn new() -> Self {
        use winapi::shared::ntdef::NTSTATUS;
        use winapi::shared::ntstatus::STATUS_SUCCESS;
        use winapi::um::winnt::{LPOSVERSIONINFOW, OSVERSIONINFOW};

        extern "system" {
            // Can't use GetVersion(Ex) since it reports version N if the caller doesn't have a manifest indicating that it supports Windows N + 1.
            // Rust binaries don't have a manifest by default, so GetVersion(Ex) always reports Windows 8.
            pub(super) fn RtlGetVersion(lpVersionInformation: LPOSVERSIONINFOW) -> NTSTATUS;
        }

        let mut result = OsInfo {
            id: Some("windows".to_owned()),
            version_id: None,
            bitness: std::mem::size_of::<usize>() * 8,
        };

        unsafe {
            let mut os_version_info: OSVERSIONINFOW = std::mem::zeroed();
            os_version_info.dwOSVersionInfoSize = std::mem::size_of_val(&os_version_info) as _;

            if RtlGetVersion(&mut os_version_info) == STATUS_SUCCESS {
                let csd_version = os_version_info
                    .szCSDVersion
                    .iter()
                    .position(|&c| c == 0)
                    .map(|len| {
                        std::os::windows::ffi::OsStringExt::from_wide(
                            &os_version_info.szCSDVersion[..len],
                        )
                    })
                    .and_then(|csd_version: std::ffi::OsString| csd_version.into_string().ok())
                    .unwrap_or_else(String::new);

                result.version_id = Some(format!(
                    "{}.{}.{} {}",
                    os_version_info.dwMajorVersion,
                    os_version_info.dwMinorVersion,
                    os_version_info.dwBuildNumber,
                    csd_version,
                ));
            }
        }

        result
    }

    #[cfg(unix)]
    pub(super) fn new() -> Self {
        use std::fs::File;
        use std::io::{BufRead, BufReader};

        let mut result = OsInfo {
            id: None,
            version_id: None,
            bitness: std::mem::size_of::<usize>() * 8,
        };

        if let Ok(os_release) = File::open("/etc/os-release") {
            let mut os_release = BufReader::new(os_release);

            let mut line = String::new();
            loop {
                match os_release.read_line(&mut line) {
                    Ok(0) | Err(_) => break,
                    Ok(_) => {
                        if let Some((key, value)) = parse_os_release_line(&line) {
                            if key == "ID" {
                                result.id = Some(value.to_owned());
                            } else if key == "VERSION_ID" {
                                result.version_id = Some(value.to_owned());
                            }
                        }

                        line.clear();
                    }
                }
            }
        }

        result
    }
}

#[cfg(unix)]
fn parse_os_release_line(line: &str) -> Option<(&str, &str)> {
    let line = line.trim();

    let mut parts = line.split('=');

    let key = parts
        .next()
        .expect("split line will have at least one part");

    let value = parts.next()?;

    // The value is essentially a shell string, so it can be quoted in single or double quotes, and can have escaped sequences using backslash.
    // For simplicitly, just trim the quotes instead of implementing a full shell string grammar.
    let value = if (value.starts_with('\'') && value.ends_with('\''))
        || (value.starts_with('"') && value.ends_with('"'))
    {
        &value[1..(value.len() - 1)]
    } else {
        value
    };

    Some((key, value))
}
