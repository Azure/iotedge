use std::env::consts::{ARCH, OS};

use failure::{self, Context, ResultExt};
use os_info::Bitness;
use serde_json::Value;

use crate::check::{checker::Checker, Check, CheckResult};
use crate::error::{DetermineModuleVersionReason, ErrorKind};
use crate::{DockerImageInfo, LatestVersions};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct LatestRuntimeModules {
    pub actual_edge_agent_version: DockerImageInfo,
    pub expected_edge_agent_version: DockerImageInfo,
    pub actual_edge_hub_version: DockerImageInfo,
    pub expected_edge_hub_version: DockerImageInfo,
    pub latest_versions: crate::LatestVersions,
}

enum PlatformType {
    LinuxAmd64,
    LinuxArm32v7,
    LinuxArm64v8,
}

impl PlatformType {
    fn get() -> Result<PlatformType, failure::Error> {
        let bitness = os_info::get().bitness();
        if ARCH == "x86_64" && OS == "linux" {
            Ok(PlatformType::LinuxAmd64)
        } else if ARCH == "arm" && OS == "linux" && bitness == Bitness::X32 {
            Ok(PlatformType::LinuxArm32v7)
        } else if ARCH == "arm" && OS == "linux" && bitness == Bitness::X64 {
            Ok(PlatformType::LinuxArm64v8)
        } else {
            Err(failure::Error::from(ErrorKind::UnknownPlatform {
                os: OS.to_string(),
                arch: ARCH.to_string(),
                bitness: bitness.to_string(),
            }))
        }
    }
}

impl Checker for LatestRuntimeModules {
    fn id(&self) -> &'static str {
        "latest-runtime-modules"
    }

    fn description(&self) -> &'static str {
        "edgeAgent and edgeHub runtime modules are up-to-date"
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

        // Determine actual running versions of edgeAgent and edgeHub
        self.actual_edge_agent_version =
            match LatestRuntimeModules::get_module_image_info(docker_host_arg, "edgeAgent") {
                Ok(v) => v,
                Err(e) => return CheckResult::Failed(e),
            };
        self.actual_edge_hub_version =
            match LatestRuntimeModules::get_module_image_info(docker_host_arg, "edgeHub") {
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
            let platform_res = PlatformType::get();

            // Set expected edgeAgent version if not provided as cmd line arg
            if check.expected_aziot_edge_agent_image_id.is_none() {
                match platform_res {
                    Ok(ref platform) => {
                        self.expected_edge_agent_version = match platform {
                            PlatformType::LinuxAmd64 => {
                                self.latest_versions.aziot_edge_agent.linux_amd64.clone()
                            }
                            PlatformType::LinuxArm32v7 => {
                                self.latest_versions.aziot_edge_agent.linux_arm32v7.clone()
                            }
                            PlatformType::LinuxArm64v8 => {
                                self.latest_versions.aziot_edge_agent.linux_arm64v8.clone()
                            }
                        }
                    }
                    Err(e) => return CheckResult::Failed(e),
                }
            }

            // Set expected edgeHub version if not provided as cmd line arg
            if check.expected_aziot_edge_hub_image_id.is_none() {
                match platform_res {
                    Ok(ref platform) => {
                        self.expected_edge_hub_version = match platform {
                            PlatformType::LinuxAmd64 => {
                                self.latest_versions.aziot_edge_hub.linux_amd64.clone()
                            }
                            PlatformType::LinuxArm32v7 => {
                                self.latest_versions.aziot_edge_hub.linux_arm32v7.clone()
                            }
                            PlatformType::LinuxArm64v8 => {
                                self.latest_versions.aziot_edge_hub.linux_arm64v8.clone()
                            }
                        }
                    }
                    Err(e) => return CheckResult::Failed(e),
                }
            }
        }

        if self.actual_edge_agent_version.image_id != self.expected_edge_agent_version.image_id {
            return CheckResult::Warning(
            failure::Error::from(Context::new(format!(
                "Running an old version of edgeAgent.\n\
                \tDeployed image: {}.\n\
                \tLatest image:   {}\n\
                Please see https://aka.ms/iotedge-update-runtime#update-the-runtime-containers for update instructions.", 
                self.actual_edge_agent_version, self.expected_edge_agent_version) ))
                );
        }
        if self.actual_edge_hub_version.image_id != self.expected_edge_hub_version.image_id {
            return CheckResult::Warning(
            failure::Error::from(Context::new(format!(
                "Running an old version of edgeHub.\n\
                \tDeployed image: {}.\n\
                \tLatest image:   {}\n\
                Please see https://aka.ms/iotedge-update-runtime#update-the-runtime-containers for update instructions.", 
                self.actual_edge_hub_version, self.expected_edge_hub_version) ))
        );
        }

        CheckResult::Ok
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl LatestRuntimeModules {
    fn get_module_image_info(
        docker_host_arg: &str,
        module_name: &str,
    ) -> Result<DockerImageInfo, failure::Error> {
        let stdout = super::docker(docker_host_arg, vec!["inspect", module_name])
            .map_err(|(_, err)| err)
            .context(format!("'docker inspect {}' failed", module_name))?;

        // Convert output text to json
        let output = String::from_utf8(stdout).context(ErrorKind::DetermineModuleVersion(
            DetermineModuleVersionReason::StdoutToStringConversionError(module_name.to_owned()),
        ))?;
        let output_json: Value =
            serde_json::from_str(&output).context(ErrorKind::DetermineModuleVersion(
                DetermineModuleVersionReason::JsonDeserializationError(module_name.to_owned()),
            ))?;

        // Retrieve and return docker image info
        let repo_and_tag = output_json[0]["Config"]["Image"]
            .as_str()
            .map(std::borrow::ToOwned::to_owned)
            .ok_or_else(|| {
                ErrorKind::DetermineModuleVersion(
                    DetermineModuleVersionReason::ConfigImageKeyNotFound(module_name.to_owned()),
                )
            })?;
        let repo_and_tag: Vec<String> = repo_and_tag
            .split(':')
            .map(std::borrow::ToOwned::to_owned)
            .collect();
        Ok(DockerImageInfo {
            repository: repo_and_tag[0].clone(),
            image_tag: repo_and_tag[1].clone(),
            image_id: output_json[0]["Image"]
                .as_str()
                .map(std::borrow::ToOwned::to_owned)
                .ok_or_else(|| {
                    ErrorKind::DetermineModuleVersion(
                        DetermineModuleVersionReason::ImageKeyNotFound(module_name.to_owned()),
                    )
                })?,
        })
    }
}

#[cfg(all(target_os="linux", target_arch="x86_64"))]
#[cfg(test)]
mod tests {
    use super::super::docker;
    use super::*;

    struct TestHelper {
        docker_host_arg: String,
        containers_to_create: Vec<Container>,
        _edged_package_name: Option<String>,
    }

    impl TestHelper {
        fn new(
            docker_host_arg: String,
            containers_to_create: Vec<Container>,
            _edged_package_name: Option<String>,
        ) -> TestHelper {
            for c in &containers_to_create {
                docker(
                    docker_host_arg.as_str(),
                    vec![
                        "run",
                        "--name",
                        c.container_name.as_str(),
                        c.image_name.as_str(),
                    ],
                )
                .map_err(|(_, err)| err)
                .context(format!("Failed to run {} container", c.container_name))
                .expect("docker run expected to succeed");
            }

            TestHelper {
                docker_host_arg,
                containers_to_create,
                _edged_package_name,
            }
        }

        fn run_get_module_image_info_test(
            &self,
            module_name: &str,
            expected_image_info: &DockerImageInfo,
        ) {
            let image_info_result = LatestRuntimeModules::get_module_image_info(
                self.docker_host_arg.as_str(),
                module_name,
            );
            assert!(image_info_result.is_ok());

            assert_eq!(image_info_result.unwrap(), *expected_image_info);
        }
    }

    impl Drop for TestHelper {
        fn drop(&mut self) {
            for c in &self.containers_to_create {
                docker(
                    self.docker_host_arg.as_str(),
                    vec!["stop", c.container_name.as_str()],
                )
                .map_err(|(_, err)| err)
                .context(format!("Failed to stop {} container", c.container_name))
                .expect("docker stop expected to succeed");

                docker(
                    self.docker_host_arg.as_str(),
                    vec!["rm", c.container_name.as_str()],
                )
                .map_err(|(_, err)| err)
                .context(format!("Failed to remove {} container", c.container_name))
                .expect("docker rm expected to succeed");
            }
        }
    }

    struct Container {
        image_name: String,
        container_name: String,
    }

    #[test]
    fn test_get_module_image_info() {
        let module_name = "alpine".to_owned();
        let image_to_pull = "alpine:3.13.5".to_owned();
        let expected_image_info = DockerImageInfo {
            image_tag: "3.13.5".to_owned(),
            repository: "alpine".to_owned(),
            image_id: "sha256:6dbb9cc54074106d46d4ccb330f2a40a682d49dda5f4844962b7dce9fe44aaec"
                .to_owned(),
        };
        let helper = TestHelper::new(
            "unix:///var/run/docker.sock".to_owned(),
            vec![Container {
                image_name: image_to_pull.clone(),
                container_name: module_name.clone(),
            }],
            None,
        );
        helper.run_get_module_image_info_test(module_name.as_str(), &expected_image_info)
    }

    // #[test]
    // fn latest_version_check_passes() {
    //     unimplemented!();
    // }

    // #[test]
    // fn latest_edged_not_installed() {
    //     unimplemented!();
    // }

    // #[test]
    // fn latest_edge_agent_not_running() {
    //     unimplemented!();
    // }

    // #[test]
    // fn latest_edge_hub_not_running() {
    //     unimplemented!();
    // }

    // #[test]
    // fn latest_version_check_fails_if_edge_hub_missing() {
    //     unimplemented!();
    // }
}
