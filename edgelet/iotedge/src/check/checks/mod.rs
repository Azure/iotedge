mod aziot_edged_version;
mod check_agent_image;
mod check_sockets;
mod check_users;
mod connect_management_uri;
mod container_connect_upstream;
mod container_engine_dns;
mod container_engine_installed;
mod container_engine_ipv6;
mod container_engine_logrotate;
mod container_local_time;
mod container_resolve_parent_hostname;
mod parent_hostname;
mod proxy_settings;
mod storage_mounted_from_host;
mod up_to_date_config;
mod well_formed_config;

pub(crate) use self::aziot_edged_version::AziotEdgedVersion;
pub(crate) use self::check_agent_image::CheckAgentImage;
use self::check_sockets::CheckSockets;
pub(crate) use self::check_users::CheckUsers;
pub(crate) use self::connect_management_uri::ConnectManagementUri;
pub(crate) use self::container_connect_upstream::get_host_container_upstream_tests;
pub(crate) use self::container_engine_dns::ContainerEngineDns;
pub(crate) use self::container_engine_installed::ContainerEngineInstalled;
pub(crate) use self::container_engine_ipv6::ContainerEngineIPv6;
pub(crate) use self::container_engine_logrotate::ContainerEngineLogrotate;
pub(crate) use self::container_local_time::ContainerLocalTime;
pub(crate) use self::container_resolve_parent_hostname::ContainerResolveParentHostname;
pub(crate) use self::parent_hostname::ParentHostname;
pub(crate) use self::proxy_settings::ProxySettings;
pub(crate) use self::storage_mounted_from_host::{EdgeAgentStorageMounted, EdgeHubStorageMounted};
pub(crate) use self::up_to_date_config::UpToDateConfig;
pub(crate) use self::well_formed_config::WellFormedConfig;

use std::ffi::OsStr;

use failure::{self, Context, Fail};

use super::Checker;

pub(crate) async fn docker<I>(
    docker_host_arg: &str,
    args: I,
) -> Result<Vec<u8>, (Option<String>, failure::Error)>
where
    I: IntoIterator,
    <I as IntoIterator>::Item: AsRef<OsStr>,
{
    let mut process = tokio::process::Command::new("docker");
    process.arg("-H");
    process.arg(docker_host_arg);

    process.args(args);

    let output = process.output().await.map_err(|err| {
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

// built-in checks, as opposed to those that are deferred to `aziot check`
pub(crate) fn built_in_checks() -> [(&'static str, Vec<Box<dyn Checker>>); 3] {
    /* Note: keep ordering consistent. Later tests may depend on earlier tests. */
    [
        (
            "Installation Checks",
            vec![
                Box::new(CheckUsers::default()),
                Box::new(CheckSockets::default()),
            ],
        ),
        (
            "Configuration checks",
            vec![
                Box::new(WellFormedConfig::default()),
                Box::new(UpToDateConfig::default()),
                Box::new(ContainerEngineInstalled::default()),
                Box::new(ParentHostname::default()),
                Box::new(ContainerResolveParentHostname::default()),
                Box::new(ConnectManagementUri::default()),
                Box::new(AziotEdgedVersion::default()),
                Box::new(ContainerLocalTime::default()),
                Box::new(ContainerEngineDns::default()),
                Box::new(ContainerEngineIPv6::default()),
                Box::new(ContainerEngineLogrotate::default()),
                Box::new(EdgeAgentStorageMounted::default()),
                Box::new(EdgeHubStorageMounted::default()),
                Box::new(CheckAgentImage::default()),
                Box::new(ProxySettings::default()),
            ],
        ),
        ("Connectivity checks", {
            let mut tests: Vec<Box<dyn Checker>> = Vec::new();
            tests.extend(get_host_container_upstream_tests());
            tests
        }),
    ]
}
