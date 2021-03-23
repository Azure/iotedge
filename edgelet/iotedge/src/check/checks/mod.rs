mod aziot_edged_version;
mod check_agent_image;
mod connect_management_uri;
mod container_connect_upstream;
mod container_engine_dns;
mod container_engine_installed;
mod container_engine_ipv6;
mod container_engine_is_moby;
mod container_engine_logrotate;
mod container_local_time;
mod container_resolve_parent_hostname;
mod hostname;
mod parent_hostname;
mod storage_mounted_from_host;
mod up_to_date_config;
mod well_formed_config;

pub(crate) use self::aziot_edged_version::AziotEdgedVersion;
pub(crate) use self::check_agent_image::CheckAgentImage;
pub(crate) use self::connect_management_uri::ConnectManagementUri;
pub(crate) use self::container_connect_upstream::get_host_container_upstream_tests;
pub(crate) use self::container_engine_dns::ContainerEngineDns;
pub(crate) use self::container_engine_installed::ContainerEngineInstalled;
pub(crate) use self::container_engine_ipv6::ContainerEngineIPv6;
pub(crate) use self::container_engine_is_moby::ContainerEngineIsMoby;
pub(crate) use self::container_engine_logrotate::ContainerEngineLogrotate;
pub(crate) use self::container_local_time::ContainerLocalTime;
pub(crate) use self::container_resolve_parent_hostname::ContainerResolveParentHostname;
pub(crate) use self::hostname::Hostname;
pub(crate) use self::parent_hostname::ParentHostname;
pub(crate) use self::storage_mounted_from_host::{EdgeAgentStorageMounted, EdgeHubStorageMounted};
pub(crate) use self::up_to_date_config::UpToDateConfig;
pub(crate) use self::well_formed_config::WellFormedConfig;

use std::ffi::OsStr;
use std::process::Command;

use failure::{self, Context, Fail};

use super::Checker;

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

// built-in checks, as opposed to those that are deferred to `aziot check`
pub(crate) fn built_in_checks() -> [(&'static str, Vec<Box<dyn Checker>>); 2] {
    /* Note: keep ordering consistent. Later tests may depend on earlier tests. */
    [
        (
            "Configuration checks",
            vec![
                Box::new(WellFormedConfig::default()),
                Box::new(UpToDateConfig::default()),
                Box::new(ContainerEngineInstalled::default()),
                Box::new(Hostname::default()),
                Box::new(ParentHostname::default()),
                Box::new(ContainerResolveParentHostname::default()),
                Box::new(ConnectManagementUri::default()),
                Box::new(AziotEdgedVersion::default()),
                Box::new(ContainerLocalTime::default()),
                Box::new(ContainerEngineDns::default()),
                Box::new(ContainerEngineIPv6::default()),
                Box::new(ContainerEngineIsMoby::default()),
                Box::new(ContainerEngineLogrotate::default()),
                Box::new(EdgeAgentStorageMounted::default()),
                Box::new(EdgeHubStorageMounted::default()),
                Box::new(CheckAgentImage::default()),
            ],
        ),
        ("Connectivity checks", {
            let mut tests: Vec<Box<dyn Checker>> = Vec::new();
            tests.extend(get_host_container_upstream_tests());
            tests
        }),
    ]
}
