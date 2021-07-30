use std::env::consts::{ARCH, OS};

use failure::{self, Context, ResultExt};
use os_info::Bitness;
use serde_json::Value;

use crate::check::{checker::Checker, Check, CheckResult};
use crate::error::{DetermineEdgeVersionReason, ErrorKind};
use crate::{DockerImageInfo, LatestVersions};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct LatestRuntimeModules {
    pub actual_edge_agent_version: DockerImageInfo,
    pub expected_edge_agent_version: DockerImageInfo,
    pub actual_edge_hub_version: DockerImageInfo,
    pub expected_edge_hub_version: DockerImageInfo,
    pub latest_versions: crate::LatestVersions,
}

impl Checker for LatestRuntimeModules {
    fn id(&self) -> &'static str {
        "aziot-edge-version"
    }

    fn description(&self) -> &'static str {
        "aziot-edge package is up-to-date"
    }

    fn execute(
        &mut self,
        check: &mut Check,
        tokio_runtime: &mut tokio::runtime::Runtime,
    ) -> CheckResult {
        let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
            docker_host_arg
        } else {
            return CheckResult::Skipped;
        };

        // Determine actual running versions of iotedge daemon, agent, and hub
        self.actual_edge_agent_version.image_id =
            match LatestRuntimeModules::actual_module_image_id(docker_host_arg, "edgeAgent") {
                Ok(v) => v,
                Err(e) => return CheckResult::Failed(e),
            };
        // TODO: Consider ignoring errors caused by EdgeHub not currently running
        self.actual_edge_hub_version.image_id =
            match LatestRuntimeModules::actual_module_image_id(docker_host_arg, "edgeHub") {
                Ok(v) => v,
                Err(e) => return CheckResult::Failed(e),
            };

        if check.expected_aziot_edge_agent_image_id.is_none()
            || check.expected_aziot_edge_hub_image_id.is_none()
        {
            if check.parent_hostname.is_some() {
                // This is a nested Edge device so it may not be able to access aka.ms or github.com.
                // In the best case the request would be blocked immediately, but in the worst case it may take a long time to time out.
                // The user didn't provide the `expected_aziot_edged_version` param on the CLI, so we just ignore this check.
                return CheckResult::Ignored;
            }

            self.latest_versions = match LatestVersions::get_latest_versions(
                tokio_runtime,
                "https://aka.ms/AAdb3gt",
            ) {
                Ok(lv) => lv,
                Err(e) => match e.kind() {
                    ErrorKind::FetchLatestVersions(_) => return CheckResult::Warning(e.into()),
                    _ => return CheckResult::Failed(e.into()),
                },
            };

            // Determine OS and Arch
            let bitness = os_info::get().bitness();
            // TODO: Consider using an enum for os_arch instead of a string slice
            let os_arch: &str = if ARCH == "x86_64" && OS == "linux" {
                "linux-amd64"
            } else if ARCH == "arm" && OS == "linux" && bitness == Bitness::X32 {
                "linux-arm32v7"
            } else if ARCH == "arm" && OS == "linux" && bitness == Bitness::X64 {
                "linux-arm64v8"
            } else {
                return CheckResult::Failed(failure::Error::from(ErrorKind::UnknownPlatform {
                    os: OS.to_string(),
                    arch: ARCH.to_string(),
                    bitness: bitness.to_string(),
                }));
            };

            // Set expected edgeAgent version if not provided as cmd line arg
            if check.expected_aziot_edge_agent_image_id.is_none() {
                self.expected_edge_agent_version = match os_arch {
                    "linux-amd64" => self.latest_versions.aziot_edge_agent.linux_amd64.clone(),
                    "linux-arm32v7" => self.latest_versions.aziot_edge_agent.linux_arm32v7.clone(),
                    "linux-arm64v8" => self.latest_versions.aziot_edge_agent.linux_arm64v8.clone(),
                    _ => {
                        return CheckResult::Failed(failure::Error::from(
                            ErrorKind::UnknownPlatform {
                                os: OS.to_string(),
                                arch: ARCH.to_string(),
                                bitness: bitness.to_string(),
                            },
                        ))
                    }
                };
            }

            // Set expected edgeHub version if not provided as cmd line arg
            if check.expected_aziot_edge_hub_image_id.is_none() {
                self.expected_edge_hub_version = match os_arch {
                    "linux-amd64" => self.latest_versions.aziot_edge_hub.linux_amd64.clone(),
                    "linux-arm32v7" => self.latest_versions.aziot_edge_hub.linux_arm32v7.clone(),
                    "linux-arm64v8" => self.latest_versions.aziot_edge_hub.linux_arm64v8.clone(),
                    _ => {
                        return CheckResult::Failed(failure::Error::from(
                            ErrorKind::UnknownPlatform {
                                os: OS.to_string(),
                                arch: ARCH.to_string(),
                                bitness: bitness.to_string(),
                            },
                        ))
                    }
                };
            }
        }

        if self.actual_edge_agent_version.image_id != self.expected_edge_agent_version.image_id {
            return CheckResult::Warning(
            failure::Error::from(Context::new(format!(
                "Running edgeAgent module has image ID {} but {} is the image ID of the latest released version.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                self.actual_edge_agent_version.image_id, self.expected_edge_agent_version.image_id,
           )))
        );
        }
        if self.actual_edge_hub_version.image_id != self.expected_edge_hub_version.image_id {
            return CheckResult::Warning(
            failure::Error::from(Context::new(format!(
                "Running edgeHub module has image ID {} but {} is the image ID of the latest released version.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                self.actual_edge_hub_version.image_id, self.expected_edge_hub_version.image_id,
           )))
        );
        }

        CheckResult::Ok
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl LatestRuntimeModules {
    fn actual_module_image_id(
        docker_host_arg: &str,
        module_name: &str,
    ) -> Result<String, failure::Error> {
        let stdout = super::docker(docker_host_arg, vec!["inspect", module_name])
            .map_err(|(_, err)| err)
            .context(format!("'docker inspect {}' failed", module_name))?;

        // Convert output text to json
        let output = String::from_utf8(stdout).context(ErrorKind::DetermineEdgeVersion(
            DetermineEdgeVersionReason::StdoutToStringConversionError(module_name.to_owned()),
        ))?;
        let output_json: Value =
            serde_json::from_str(&output).context(ErrorKind::DetermineEdgeVersion(
                DetermineEdgeVersionReason::JsonDeserializationError(module_name.to_owned()),
            ))?;

        // Retrieve and return image ID
        Ok(output_json[0]["Image"]
            .as_str()
            .map(std::borrow::ToOwned::to_owned)
            .ok_or_else(|| {
                ErrorKind::DetermineEdgeVersion(DetermineEdgeVersionReason::ImageKeyNotFound(
                    module_name.to_owned(),
                ))
            })?)
    }
}
