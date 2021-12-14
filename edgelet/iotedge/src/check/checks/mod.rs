mod certificates_quickstart;
mod connect_management_uri;
mod container_connect_iothub;
mod container_engine_dns;
mod container_engine_installed;
mod container_engine_ipv6;
mod container_engine_is_moby;
mod container_engine_logrotate;
mod container_local_time;
mod host_connect_dps_endpoint;
mod host_connect_iothub;
mod host_local_time;
mod hostname;
mod identity_certificate_expiry;
mod iotedged_version;
#[cfg(unix)]
mod proxy_settings;
mod storage_mounted_from_host;
mod well_formed_config;
mod well_formed_connection_string;
mod windows_host_version;

pub(crate) use self::certificates_quickstart::CertificatesQuickstart;
pub(crate) use self::connect_management_uri::ConnectManagementUri;
pub(crate) use self::container_connect_iothub::get_host_container_iothub_tests;
pub(crate) use self::container_engine_dns::ContainerEngineDns;
pub(crate) use self::container_engine_installed::ContainerEngineInstalled;
pub(crate) use self::container_engine_ipv6::ContainerEngineIPv6;
#[cfg(windows)]
pub(crate) use self::container_engine_is_moby::ContainerEngineIsMoby;
pub(crate) use self::container_engine_logrotate::ContainerEngineLogrotate;
pub(crate) use self::container_local_time::ContainerLocalTime;
pub(crate) use self::host_connect_dps_endpoint::HostConnectDpsEndpoint;
pub(crate) use self::host_connect_iothub::get_host_connect_iothub_tests;
pub(crate) use self::host_local_time::HostLocalTime;
pub(crate) use self::hostname::Hostname;
pub(crate) use self::identity_certificate_expiry::IdentityCertificateExpiry;
pub(crate) use self::iotedged_version::IotedgedVersion;
#[cfg(unix)]
pub(crate) use self::proxy_settings::ProxySettings;
pub(crate) use self::storage_mounted_from_host::{EdgeAgentStorageMounted, EdgeHubStorageMounted};
pub(crate) use self::well_formed_config::WellFormedConfig;
pub(crate) use self::well_formed_connection_string::WellFormedConnectionString;
pub(crate) use self::windows_host_version::WindowsHostVersion;

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
