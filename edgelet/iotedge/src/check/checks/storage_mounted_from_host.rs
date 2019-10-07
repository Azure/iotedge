use std::path::Path;

use failure::{self, Context, ResultExt};
use regex::Regex;

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub struct EdgeAgentStorageMounted {}
impl Checker for EdgeAgentStorageMounted {
    fn id(&self) -> &'static str {
        "edge-agent-storage-mounted-from-host"
    }
    fn description(&self) -> &'static str {
        // Note: Keep in sync with Microsoft.Azure.Devices.Edge.Agent.Service.Program.GetStoragePath
        "production readiness: Edge Agent's storage directory is persisted on the host filesystem"
    }
    fn result(&mut self, check: &mut Check) -> CheckResult {
        storage_mounted_from_host(check, "edgeAgent", "edgeAgent")
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

#[derive(Default, serde_derive::Serialize)]
pub struct EdgeHubStorageMounted {}
impl Checker for EdgeHubStorageMounted {
    fn id(&self) -> &'static str {
        "edge-hub-storage-mounted-from-host"
    }
    fn description(&self) -> &'static str {
        // Note: Keep in sync with Microsoft.Azure.Devices.Edge.Hub.Service.DependencyManager.GetStoragePath
        "production readiness: Edge Hub's storage directory is persisted on the host filesystem"
    }
    fn result(&mut self, check: &mut Check) -> CheckResult {
        storage_mounted_from_host(check, "edgeHub", "edgeHub").unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

fn storage_mounted_from_host(
    check: &mut Check,
    container_name: &'static str,
    storage_directory_name: &'static str,
) -> Result<CheckResult, failure::Error> {
    lazy_static::lazy_static! {
        static ref STORAGE_FOLDER_ENV_VAR_KEY_REGEX: Regex =
            Regex::new("(?i)^storagefolder=(.*)")
            .expect("This hard-coded regex is expected to be valid.");
    }

    let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
        docker_host_arg
    } else {
        return Ok(CheckResult::Skipped);
    };

    let inspect_result = inspect_container(docker_host_arg, container_name)?;

    let temp_dir = inspect_result
        .config()
        .and_then(docker::models::ContainerConfig::env)
        .into_iter()
        .flatten()
        .find_map(|env| {
            STORAGE_FOLDER_ENV_VAR_KEY_REGEX
                .captures(env)
                .and_then(|capture| capture.get(1))
                .map(|match_| match_.as_str())
        })
        .unwrap_or(
            // Hard-code the value here rather than using the tempfile crate. It needs to match .Net Core's implementation,
            // and needs to be in the context of the container user instead of the host running `iotedge check`.
            if cfg!(windows) {
                r"C:\Windows\Temp"
            } else {
                "/tmp"
            },
        );

    let storage_directory = Path::new(&*temp_dir).join(storage_directory_name);

    let mounted_directories = inspect_result
        .mounts()
        .into_iter()
        .flatten()
        .filter_map(|mount| mount.destination().map(Path::new));

    let volume_directories = inspect_result
        .config()
        .and_then(docker::models::ContainerConfig::volumes)
        .map(std::collections::HashMap::keys)
        .into_iter()
        .flatten()
        .map(Path::new);

    if !mounted_directories
        .chain(volume_directories)
        .any(|container_directory| container_directory == storage_directory)
    {
        return Ok(CheckResult::Warning(
            Context::new(format!(
                "The {} module is not configured to persist its {} directory on the host filesystem.\n\
                 Data might be lost if the module is deleted or updated.\n\
                 Please see https://aka.ms/iotedge-storage-host for best practices.",
                container_name,
                storage_directory.display(),
            )).into(),
        ));
    }

    Ok(CheckResult::Ok)
}

fn inspect_container(
    docker_host_arg: &str,
    name: &str,
) -> Result<docker::models::InlineResponse200, failure::Error> {
    Ok(
        super::container_engine_installed::docker(docker_host_arg, &["inspect", name])
            .map_err(|(_, err)| err)
            .and_then(|output| {
                let (inspect_result,): (docker::models::InlineResponse200,) =
                    serde_json::from_slice(&output)
                        .context("Could not parse result of docker inspect")?;
                Ok(inspect_result)
            })
            .with_context(|_| format!("Could not check current state of {} container", name))?,
    )
}
