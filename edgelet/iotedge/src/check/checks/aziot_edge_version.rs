use std::env::consts::{ARCH, OS};
use std::process::Command;

use failure::{self, Context, Fail, ResultExt};
use futures::{Future, Stream};
use os_info::Bitness;
use regex::Regex;

use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;
use serde_json::Value;

use crate::check::{checker::Checker, Check, CheckResult};
use crate::error::{DetermineEdgeVersionReason, Error, ErrorKind, FetchLatestVersionsReason};
use crate::DockerImageInfo;

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct AziotEdgeVersion {
    pub actual_edged_version: String,
    pub expected_edged_version: String,
    pub actual_edge_agent_version: DockerImageInfo,
    pub expected_edge_agent_version: DockerImageInfo,
    pub actual_edge_hub_version: DockerImageInfo,
    pub expected_edge_hub_version: DockerImageInfo,
    pub latest_versions: crate::LatestVersions,
}

impl Checker for AziotEdgeVersion {
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
            match AziotEdgeVersion::actual_module_image_id(docker_host_arg, "edgeAgent") {
                Ok(v) => v,
                Err(e) => return CheckResult::Failed(e),
            };
        // TODO: Consider ignoring errors caused by EdgeHub not currently running
        self.actual_edge_hub_version.image_id =
            match AziotEdgeVersion::actual_module_image_id(docker_host_arg, "edgeHub") {
                Ok(v) => v,
                Err(e) => return CheckResult::Failed(e),
            };
        self.actual_edged_version = match AziotEdgeVersion::actual_edged_version(check) {
            Ok(v) => v,
            Err(e) => return CheckResult::Failed(e),
        };

        if check.expected_aziot_edged_version.is_none()
            || check.expected_aziot_edge_agent_image_id.is_none()
            || check.expected_aziot_edge_hub_image_id.is_none()
        {
            if check.parent_hostname.is_some() {
                // This is a nested Edge device so it may not be able to access aka.ms or github.com.
                // In the best case the request would be blocked immediately, but in the worst case it may take a long time to time out.
                // The user didn't provide the `expected_aziot_edged_version` param on the CLI, so we just ignore this check.
                return CheckResult::Ignored;
            }

            let check_result =
                self.init_latest_versions(tokio_runtime, "https://aka.ms/latest-aziot-edge");
            match check_result {
                CheckResult::Ok => (),
                _ => return check_result,
            }

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

            // Set expected edged version if not provided as cmd line args
            if check.expected_aziot_edged_version.is_none() {
                self.expected_edged_version = self.latest_versions.aziot_edge.clone();
            }
        }

        check.additional_info.aziot_edged_version = Some(self.actual_edged_version.clone());

        if self.actual_edged_version != self.expected_edged_version {
            return CheckResult::Warning(
            failure::Error::from(Context::new(format!(
                "Installed IoT Edge daemon has version {} but {} is the latest stable version available.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                self.actual_edged_version, self.expected_edged_version,
           )))
        );
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

impl AziotEdgeVersion {
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

    fn actual_edged_version(check: &mut Check) -> Result<String, failure::Error> {
        let mut process = Command::new(&check.aziot_edged);
        process.arg("--version");

        let output = process
            .output()
            .context("Could not spawn aziot-edged process")?;
        if !output.status.success() {
            return Err(Context::new(format!(
                "aziot-edged returned {}, stderr = {}",
                output.status,
                String::from_utf8_lossy(&*output.stderr),
            ))
            .context("Could not spawn aziot-edged process")
            .into());
        }

        let output = String::from_utf8(output.stdout)
            .context("Could not parse output of aziot-edged --version")?;

        let aziot_edged_version_regex = Regex::new(r"^aziot-edged ([^ ]+)(?: \(.*\))?$")
            .expect("This hard-coded regex is expected to be valid.");
        let captures = aziot_edged_version_regex
            .captures(output.trim())
            .ok_or_else(|| {
                Context::new(format!(
                    "output {:?} does not match expected format",
                    output,
                ))
                .context("Could not parse output of aziot-edged --version")
            })?;
        Ok(captures
            .get(1)
            .expect("unreachable: regex defines one capturing group")
            .as_str()
            .to_owned())
    }

    fn init_latest_versions(
        &mut self,
        tokio_runtime: &mut tokio::runtime::Runtime,
        latest_versions_url: &str,
    ) -> CheckResult {
        // Pull expected versions from https://aka.ms/latest-aziot-edge
        let proxy = std::env::var("HTTPS_PROXY")
            .ok()
            .or_else(|| std::env::var("https_proxy").ok())
            .map(|proxy| proxy.parse::<hyper::Uri>())
            .transpose()
            .context(ErrorKind::FetchLatestVersions(
                FetchLatestVersionsReason::CreateClient,
            ));
        let hyper_client = proxy.and_then(|proxy| {
            MaybeProxyClient::new(proxy, None, None).context(ErrorKind::FetchLatestVersions(
                FetchLatestVersionsReason::CreateClient,
            ))
        });
        let hyper_client = match hyper_client {
            Ok(hyper_client) => hyper_client,
            Err(err) => return CheckResult::Failed(err.into()),
        };

        let request = hyper::Request::get(latest_versions_url)
            .body(hyper::Body::default())
            .expect("can't fail to create request");

        let latest_versions_fut = hyper_client
            .call(request)
            .then(|response| -> Result<_, Error> {
                let response = response.context(ErrorKind::FetchLatestVersions(
                    FetchLatestVersionsReason::GetResponse,
                ))?;
                Ok(response)
            })
            .and_then(move |response| match response.status() {
                status_code if status_code.is_redirection() => {
                    let uri = response
                        .headers()
                        .get(hyper::header::LOCATION)
                        .ok_or(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::InvalidOrMissingLocationHeader,
                        ))?
                        .to_str()
                        .context(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::InvalidOrMissingLocationHeader,
                        ))?;
                    let request = hyper::Request::get(uri)
                        .body(hyper::Body::default())
                        .expect("can't fail to create request");
                    Ok(hyper_client.call(request).map_err(|err| {
                        err.context(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::GetResponse,
                        ))
                        .into()
                    }))
                }
                status_code => Err(ErrorKind::FetchLatestVersions(
                    FetchLatestVersionsReason::ResponseStatusCode(status_code),
                )
                .into()),
            })
            .flatten()
            .and_then(|response| -> Result<_, Error> {
                match response.status() {
                    hyper::StatusCode::OK => Ok(response.into_body().concat2().map_err(|err| {
                        err.context(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::GetResponse,
                        ))
                        .into()
                    })),
                    status_code => Err(ErrorKind::FetchLatestVersions(
                        FetchLatestVersionsReason::ResponseStatusCode(status_code),
                    )
                    .into()),
                }
            })
            .flatten()
            .and_then(|body| {
                Ok(
                    serde_json::from_slice(&body).context(ErrorKind::FetchLatestVersions(
                        FetchLatestVersionsReason::GetResponse,
                    ))?,
                )
            });
        let latest_versions_res: Result<crate::LatestVersions, Option<Error>> =
            tokio_runtime.block_on(latest_versions_fut).map_err(Some);
        self.latest_versions = match latest_versions_res {
            Ok(latest_versions) => latest_versions,
            Err(mut err) => match err.take() {
                Some(e) => return CheckResult::Warning(e.into()),
                None => return CheckResult::Skipped,
            },
        };

        CheckResult::Ok
    }
}

#[cfg(test)]

mod tests {
    use super::super::docker;
    use super::*;
    use httpmock::Method::GET;
    use httpmock::MockServer;

    #[test]
    fn test_init_latest_versions() {
        let server = MockServer::start();
        let _latest_versions_mock = server.mock(|when, then| {
            when.method(GET).path("/latest_versions");
            then.status(302)
                .header("Location", &server.url("/redirected_latest_versions"))
                .body("");
        });
        let _redirect_mock = server.mock(|when, then| {
            when.method(GET)
                .path("/redirected_latest_versions");
            then.status(200)
                .header("Content-Type", "text/html")
                .body("{
                    \"aziot-edge\": \"1.2.3\",
                    \"azureiotedge-agent\": {
                        \"linux-amd64\": {
                            \"image-tag\": \"1.2.3-linux-amd64\",
                            \"image-id\":  \"sha256:ff4aa7c74767e1fed2d3775a5fa2fcb506b5b2662a71dbdd48c8373d83a0e749\"
                        },
                        \"linux-arm32v7\": {
                            \"image-tag\": \"1.2.3-linux-arm32v7\",
                            \"image-id\":  \"sha256:817f78c4771d2d39955d89fb0a5949b1ad7a9e250f5604e5d03842a993af7a76\"
                        },
                        \"linux-arm64v8\": {
                            \"image-tag\": \"1.2.3-linux-arm64v8\",
                            \"image-id\":  \"sha256:49934927d721e4a16cb57e2b83270ceec886b4b824f5445e8228c8e87f0de95b\"
                        }
                    },
                    \"azureiotedge-hub\": {
                        \"linux-amd64\": {
                            \"image-tag\": \"1.2.3-linux-amd64\",
                            \"image-id\":  \"sha256:23f633ecd57a212f010392e1e944d1e067b84e67460ed5f001390b9f001944c7\"
                        },
                        \"linux-arm32v7\": {
                            \"image-tag\": \"1.2.3-linux-arm32v7\",
                            \"image-id\":  \"sha256:74d64b3d279f7a6d975a1be20d2a0afb32cd1142ef612f7831659403eaff728b\"
                        },
                        \"linux-arm64v8\": {
                            \"image-tag\": \"1.2.3-linux-arm64v8\",
                            \"image-id\":  \"sha256:2b93201104098d913f6ffcfac0c7575bb22db72448d5c6ef0b38dc8583f2c9c9\"
                        } 
                    }
                }");
        });

        let mut runtime = tokio::runtime::Runtime::new().unwrap();
        let mut check = AziotEdgeVersion::default();
        let init_result = check.init_latest_versions(&mut runtime, &server.url("/latest_versions"));

        assert!(matches!(init_result, CheckResult::Ok));
        assert_eq!(check.latest_versions.aziot_edge, "1.2.3");
        assert_eq!(
            check.latest_versions.aziot_edge_agent.linux_amd64.image_id,
            "sha256:ff4aa7c74767e1fed2d3775a5fa2fcb506b5b2662a71dbdd48c8373d83a0e749".to_owned()
        );
        assert_eq!(
            check
                .latest_versions
                .aziot_edge_agent
                .linux_arm32v7
                .image_id,
            "sha256:817f78c4771d2d39955d89fb0a5949b1ad7a9e250f5604e5d03842a993af7a76".to_owned()
        );
        assert_eq!(
            check
                .latest_versions
                .aziot_edge_agent
                .linux_arm64v8
                .image_id,
            "sha256:49934927d721e4a16cb57e2b83270ceec886b4b824f5445e8228c8e87f0de95b".to_owned()
        );
        assert_eq!(
            check.latest_versions.aziot_edge_hub.linux_amd64.image_id,
            "sha256:23f633ecd57a212f010392e1e944d1e067b84e67460ed5f001390b9f001944c7".to_owned()
        );
        assert_eq!(
            check.latest_versions.aziot_edge_hub.linux_arm32v7.image_id,
            "sha256:74d64b3d279f7a6d975a1be20d2a0afb32cd1142ef612f7831659403eaff728b".to_owned()
        );
        assert_eq!(
            check.latest_versions.aziot_edge_hub.linux_arm64v8.image_id,
            "sha256:2b93201104098d913f6ffcfac0c7575bb22db72448d5c6ef0b38dc8583f2c9c9".to_owned()
        );
    }

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
                        c.image_id.as_str(),
                    ],
                )
                .map_err(|(_, err)| err)
                .context("Failed to run hello-world container")
                .expect("docker run expected to succeed");
            }

            TestHelper {
                docker_host_arg,
                containers_to_create,
                _edged_package_name,
            }
        }

        fn test_actual_module_image_id_helper(&self, module_name: &str, expected_image_id: &str) {
            let actual_image_id_result = AziotEdgeVersion::actual_module_image_id(
                self.docker_host_arg.as_str(),
                module_name,
            );
            assert!(actual_image_id_result.is_ok());

            assert_eq!(actual_image_id_result.unwrap(), expected_image_id,);
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
        image_id: String,
        container_name: String,
    }

    #[test]
    fn test_actual_module_image_id() {
        let module_name = "hello_world".to_owned();
        let expected_image_id =
            "sha256:d1165f2212346b2bab48cb01c1e39ee8ad1be46b87873d9ca7a4e434980a7726".to_owned();
        let helper = TestHelper::new(
            "unix:///var/run/docker.sock".to_owned(),
            vec![Container {
                image_id: expected_image_id.clone(),
                container_name: module_name.clone(),
            }],
            None,
        );
        helper.test_actual_module_image_id_helper(module_name.as_str(), expected_image_id.as_str())
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
