pub(crate) mod certificates_quickstart;
pub(crate) mod connect_management_uri;
pub(crate) mod container_connect_iothub;
pub(crate) mod container_engine_dns;
pub(crate) mod container_engine_installed;
pub(crate) mod container_engine_ipv6;
pub(crate) mod container_engine_is_moby;
pub(crate) mod container_engine_logrotate;
pub(crate) mod container_local_time;
pub(crate) mod host_connect_dps_endpoint;
pub(crate) mod host_connect_iothub;
pub(crate) mod host_local_time;
pub(crate) mod hostname;
pub(crate) mod identity_certificate_expiry;
pub(crate) mod iotedged_version;
pub(crate) mod storage_mounted_from_host;
pub(crate) mod well_formed_config;
pub(crate) mod well_formed_connection_string;
pub(crate) mod windows_host_version;

pub(crate) use self::certificates_quickstart::CertificatesQuickstart;
pub(crate) use self::connect_management_uri::ConnectManagementUri;
pub(crate) use self::container_connect_iothub::get_host_container_iothub_tests;
pub(crate) use self::container_engine_dns::ContainerEngineDns;
pub(crate) use self::container_engine_installed::ContainerEngineInstalled;
pub(crate) use self::container_engine_ipv6::ContainerEngineIPv6;
pub(crate) use self::container_engine_is_moby::ContainerEngineIsMoby;
pub(crate) use self::container_engine_logrotate::ContainerEngineLogrotate;
pub(crate) use self::container_local_time::ContainerLocalTime;
pub(crate) use self::host_connect_dps_endpoint::HostConnectDpsEndpoint;
pub(crate) use self::host_connect_iothub::get_host_connect_iothub_tests;
pub(crate) use self::host_local_time::HostLocalTime;
pub(crate) use self::hostname::Hostname;
pub(crate) use self::identity_certificate_expiry::IdentityCertificateExpiry;
pub(crate) use self::iotedged_version::IotedgedVersion;
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
