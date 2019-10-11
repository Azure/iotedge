pub mod certificates_quickstart;
pub mod connect_management_uri;
pub mod container_connect_iothub;
pub mod container_engine_dns;
pub mod container_engine_installed;
pub mod container_engine_ipv6;
pub mod container_engine_is_moby;
pub mod container_engine_logrotate;
pub mod container_local_time;
pub mod host_connect_dps_endpoint;
pub mod host_connect_iothub;
pub mod host_local_time;
pub mod hostname;
pub mod identity_certificate_expiry;
pub mod iotedged_version;
pub mod storage_mounted_from_host;
pub mod well_formed_config;
pub mod well_formed_connection_string;
pub mod windows_host_version;

pub use self::certificates_quickstart::CertificatesQuickstart;
pub use self::connect_management_uri::ConnectManagementUri;
pub use self::container_connect_iothub::get_host_container_iothub_tests;
pub use self::container_engine_dns::ContainerEngineDns;
pub use self::container_engine_installed::ContainerEngineInstalled;
pub use self::container_engine_ipv6::ContainerEngineIPv6;
pub use self::container_engine_is_moby::ContainerEngineIsMoby;
pub use self::container_engine_logrotate::ContainerEngineLogrotate;
pub use self::container_local_time::ContainerLocalTime;
pub use self::host_connect_dps_endpoint::HostConnectDpsEndpoint;
pub use self::host_connect_iothub::get_host_connect_iothub_tests;
pub use self::host_local_time::HostLocalTime;
pub use self::hostname::Hostname;
pub use self::identity_certificate_expiry::IdentityCertificateExpiry;
pub use self::iotedged_version::IotedgedVersion;
pub use self::storage_mounted_from_host::{EdgeAgentStorageMounted, EdgeHubStorageMounted};
pub use self::well_formed_config::WellFormedConfig;
pub use self::well_formed_connection_string::WellFormedConnectionString;
pub use self::windows_host_version::WindowsHostVersion;

use std::ffi::OsStr;
use std::process::Command;

use failure::{self, Context, Fail};

pub(crate) fn docker<I>(
    docker_host_arg: &str,
    args: I,
) -> Result<Vec<u8>, (Option<String>, failure::Error)>
where
    I: IntoIterator,
    <I as IntoIterator>::Item: AsRef<OsStr>,
{
    let mut process = Command::new("docker");
    process.arg("-H");
    process.arg(docker_host_arg);

    process.args(args);

    let output = process.output().map_err(|err| {
        (
            None,
            err.context(format!("could not run {:?}", process)).into(),
        )
    })?;

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&*output.stderr).into_owned();
        let err = Context::new(format!(
            "docker returned {}, stderr = {}",
            output.status, stderr,
        ))
        .into();
        return Err((Some(stderr), err));
    }

    Ok(output.stdout)
}
