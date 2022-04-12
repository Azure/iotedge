//! Note: Keep in sync with Microsoft.Azure.Devices.Edge.Agent.Service.Program.GetStoragePath and Microsoft.Azure.Devices.Edge.Hub.Service.DependencyManager.GetStoragePath

use std::path::{Path, PathBuf};

use anyhow::Context;
use regex::Regex;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct EdgeAgentStorageMounted {
    storage_directory: Option<PathBuf>,
    container_directories: Option<Vec<PathBuf>>,
}

#[async_trait::async_trait]
impl Checker for EdgeAgentStorageMounted {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "edge-agent-storage-mounted-from-host",
            description: "production readiness: Edge Agent's storage directory is persisted on the host filesystem",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        storage_mounted_from_host(
            check,
            "edgeAgent",
            "edgeAgent",
            &mut self.storage_directory,
            &mut self.container_directories,
        )
        .await
        .unwrap_or_else(CheckResult::Failed)
    }
}

#[derive(Default, serde_derive::Serialize)]
pub struct EdgeHubStorageMounted {
    storage_directory: Option<PathBuf>,
    container_directories: Option<Vec<PathBuf>>,
}

#[async_trait::async_trait]
impl Checker for EdgeHubStorageMounted {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "edge-hub-storage-mounted-from-host",
            description: "production readiness: Edge Hub's storage directory is persisted on the host filesystem",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        storage_mounted_from_host(
            check,
            "edgeHub",
            "edgeHub",
            &mut self.storage_directory,
            &mut self.container_directories,
        )
        .await
        .unwrap_or_else(CheckResult::Failed)
    }
}

async fn storage_mounted_from_host<'a>(
    check: &'a mut Check,
    container_name: &'static str,
    storage_directory_name: &'static str,
    storage_directory_out: &'a mut Option<PathBuf>,
    container_directories_out: &'a mut Option<Vec<PathBuf>>,
) -> anyhow::Result<CheckResult> {
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

    let inspect_result = inspect_container(docker_host_arg, container_name).await?;

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
        // Hard-code the value here rather than using the tempfile crate. It needs to match .Net Core's implementation,
        // and needs to be in the context of the container user instead of the host running `iotedge check`.
        .unwrap_or("/tmp");

    let storage_directory = Path::new(&*temp_dir).join(storage_directory_name);
    *storage_directory_out = Some(storage_directory.clone());

    let mounted_directories = inspect_result
        .mounts()
        .into_iter()
        .flatten()
        .filter_map(|mount| mount.destination().map(PathBuf::from));

    let volume_directories = inspect_result
        .config()
        .and_then(docker::models::ContainerConfig::volumes)
        .map(std::collections::HashMap::keys)
        .into_iter()
        .flatten()
        .map(PathBuf::from);

    let container_directories: Vec<PathBuf> =
        mounted_directories.chain(volume_directories).collect();
    *container_directories_out = Some(container_directories.clone());

    if !container_directories
        .into_iter()
        .any(|container_directory| storage_directory.starts_with(container_directory))
    {
        return Ok(CheckResult::Warning(anyhow::Error::msg(format!(
            "The {} module is not configured to persist its {} directory on the host filesystem.\n\
                 Data might be lost if the module is deleted or updated.\n\
                 Please see https://aka.ms/iotedge-storage-host for best practices.",
            container_name,
            storage_directory.display(),
        ))));
    }

    Ok(CheckResult::Ok)
}

async fn inspect_container(
    docker_host_arg: &str,
    name: &str,
) -> anyhow::Result<docker::models::InlineResponse200> {
    Ok(super::docker(docker_host_arg, &["inspect", name])
        .await
        .map_err(|(_, err)| err)
        .and_then(|output| {
            let (inspect_result,): (docker::models::InlineResponse200,) =
                serde_json::from_slice(&output)
                    .context("Could not parse result of docker inspect")?;
            Ok(inspect_result)
        })
        .with_context(|| format!("Could not check current state of {} container", name))?)
}
