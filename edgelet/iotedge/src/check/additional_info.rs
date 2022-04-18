use std::env::consts::ARCH;
use std::str;

#[cfg(unix)]
use byte_unit::{Byte, ByteUnit};
#[cfg(unix)]
use sysinfo::{DiskExt, SystemExt};

/// Additional info for the JSON output of `iotedge check`
#[derive(Clone, Debug, serde::Serialize)]
pub(super) struct AdditionalInfo {
    pub(super) docker_version: Option<String>,
    pub(super) aziot_edged_version: Option<String>,
    now: chrono::DateTime<chrono::Utc>,
    os: OsInfo,
    system_info: SystemInfo,
}

impl AdditionalInfo {
    pub(super) fn new() -> Self {
        AdditionalInfo {
            docker_version: None,
            aziot_edged_version: None,
            now: chrono::Utc::now(),
            os: OsInfo::new(),
            system_info: SystemInfo::new(),
        }
    }
}

/// A subset of the fields from /etc/os-release.
///
/// Examples:
///
/// ```ignore
///  OS                  | id                  | version_id
/// ---------------------+---------------------+------------
///  CentOS 7            | centos              | 7
///  RedHat EL 8         | rhel                | 8.5
///  Debian 9            | debian              | 9
///  openSUSE Tumbleweed | opensuse-tumbleweed | 20190325
///  Ubuntu 18.04        | ubuntu              | 18.04
/// ```
///
/// Ref: <https://www.freedesktop.org/software/systemd/man/os-release.html>
#[derive(Clone, Debug, serde::Serialize)]
pub(super) struct OsInfo {
    id: Option<String>,
    version_id: Option<String>,
    arch: &'static str,
    bitness: usize,
}

impl OsInfo {
    #[cfg(unix)]
    pub(super) fn new() -> Self {
        use std::fs::File;
        use std::io::{BufRead, BufReader};

        let mut result = OsInfo {
            id: None,
            version_id: None,
            arch: ARCH,
            // Technically wrong if someone runs an arm32 build on arm64,
            // but we have dedicated arm64 builds so hopefully they don't.
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
#[derive(Clone, Debug, Default, serde::Serialize)]
struct SystemInfo {
    used_ram: String,
    total_ram: String,
    used_swap: String,
    total_swap: String,

    disks: Vec<DiskInfo>,
}

impl SystemInfo {
    fn new() -> Self {
        #[cfg(unix)]
        {
            let mut system = sysinfo::System::new();
            system.refresh_all();
            SystemInfo {
                total_ram: pretty_kbyte(system.total_memory()),
                used_ram: pretty_kbyte(system.used_memory()),
                total_swap: pretty_kbyte(system.total_swap()),
                used_swap: pretty_kbyte(system.used_swap()),

                disks: system.disks().iter().map(DiskInfo::new).collect(),
            }
        }

        #[cfg(not(unix))]
        return SystemInfo::default();
    }
}

#[derive(Clone, Debug, Default, serde::Serialize)]
struct DiskInfo {
    name: String,
    percent_free: String,
    available_space: String,
    total_space: String,
    file_system: String,
    file_type: String,
}

#[cfg(unix)]
impl DiskInfo {
    fn new<T>(disk: &T) -> Self
    where
        T: DiskExt,
    {
        let available_space = disk.available_space();
        let total_space = disk.total_space();
        #[allow(clippy::cast_precision_loss)]
        let percent_free = format!(
            "{:.1}%",
            available_space as f64 / total_space as f64 * 100.0
        );

        DiskInfo {
            name: disk.name().to_string_lossy().into_owned(),
            percent_free,
            available_space: Byte::from_bytes(u128::from(available_space))
                .get_appropriate_unit(true)
                .format(2),
            total_space: Byte::from_bytes(u128::from(total_space))
                .get_appropriate_unit(true)
                .format(2),
            file_system: String::from_utf8_lossy(disk.file_system()).into_owned(),
            file_type: format!("{:?}", disk.type_()),
        }
    }
}

#[cfg(unix)]
fn pretty_kbyte(bytes: u64) -> String {
    #[allow(clippy::cast_precision_loss)]
    match Byte::from_unit(bytes as f64, ByteUnit::KiB) {
        Ok(b) => b.get_appropriate_unit(true).format(2),
        Err(err) => format!("could not parse bytes value: {:?}", err),
    }
}
