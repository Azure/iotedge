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
        // Determine actual running versions of iotedge daemon, agent, and hub
        self.actual_edge_agent_version.sha256 =
            match AziotEdgeVersion::actual_module_sha256("edgeAgent") {
                Ok(v) => v.to_owned(),
                Err(e) => return CheckResult::Failed(e),
            };
        // TODO: Consider ignoring errors caused by EdgeHub not currently running
        self.actual_edge_hub_version.sha256 =
            match AziotEdgeVersion::actual_module_sha256("edgeHub") {
                Ok(v) => v.to_owned(),
                Err(e) => return CheckResult::Failed(e),
            };
        self.actual_edged_version = match AziotEdgeVersion::actual_edged_version(check) {
            Ok(v) => v.to_owned(),
            Err(e) => return CheckResult::Failed(e),
        };

        if check.expected_aziot_edged_version.is_none()
            || check.expected_aziot_edge_agent_sha256.is_none()
            || check.expected_aziot_edge_hub_sha256.is_none()
        {
            if check.parent_hostname.is_some() {
                // This is a nested Edge device so it may not be able to access aka.ms or github.com.
                // In the best case the request would be blocked immediately, but in the worst case it may take a long time to time out.
                // The user didn't provide the `expected_aziot_edged_version` param on the CLI, so we just ignore this check.
                return CheckResult::Ignored;
            }

            let check_result = self.init_latest_versions(tokio_runtime);
            match check_result {
                CheckResult::Ok => (),
                _ => return check_result,
            }

            // Determine OS and Arch
            // TODO: Instead run docker image inspect
            let bitness = os_info::get().bitness();
            // TODO: Consider using an enum for os_arch instead of a string slice
            let os_arch: &str = if ARCH == "x86_64" && OS == "linux" {
                "linux-amd64"
            } else if ARCH == "arm" && OS == "linux" && bitness == Bitness::X32 {
                "linux-arm32v7"
            } else if ARCH == "arm" && OS == "linux" && bitness == Bitness::X64 {
                "linux-arm64v8"
            // } else if ARCH == "x86_64" && OS == "windows" {
            //     "windows-amd64"
            } else {
                return CheckResult::Failed(failure::Error::from(ErrorKind::UnknownPlatform {
                    os: OS.to_string(),
                    arch: ARCH.to_string(),
                    bitness: bitness.to_string(),
                }));
            };

            // Set expected edgeAgent version if not provided as cmd line arg
            if check.expected_aziot_edge_agent_sha256.is_none() {
                self.expected_edge_agent_version = match os_arch {
                    "linux-amd64" => self.latest_versions.aziot_edge_agent.linux_amd64.clone(),
                    "linux-arm32v7" => self.latest_versions.aziot_edge_agent.linux_arm32v7.clone(),
                    "linux-arm64v8" => self.latest_versions.aziot_edge_agent.linux_arm64v8.clone(),
                    // "windows-amd64" => self.latest_versions.aziot_edge_agent.windows_amd64.clone(),
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
            if check.expected_aziot_edge_hub_sha256.is_none() {
                self.expected_edge_hub_version = match os_arch {
                    "linux-amd64" => self.latest_versions.aziot_edge_hub.linux_amd64.clone(),
                    "linux-arm32v7" => self.latest_versions.aziot_edge_hub.linux_arm32v7.clone(),
                    "linux-arm64v8" => self.latest_versions.aziot_edge_hub.linux_arm64v8.clone(),
                    // "windows-amd64" => self.latest_versions.aziot_edge_agent.windows_amd64.clone(),
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
        if self.actual_edge_agent_version.sha256 != self.expected_edge_agent_version.sha256 {
            return CheckResult::Warning(
            failure::Error::from(Context::new(format!(
                "Running edgeAgent module has sha256 {} but {} is the sha256 of the latest stable version available.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                self.actual_edge_agent_version.sha256, self.expected_edge_agent_version.sha256,
           )))
        );
        }
        if self.actual_edge_hub_version.sha256 != self.expected_edge_hub_version.sha256 {
            return CheckResult::Warning(
            failure::Error::from(Context::new(format!(
                "Running edgeHub module has sha256 {} but {} is the sha256 of the latest stable version available.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                self.actual_edge_hub_version.sha256, self.expected_edge_hub_version.sha256,
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
    fn actual_module_sha256(module_name: &str) -> Result<String, failure::Error> {
        // Run docker inspect
        // TODO Use super::docker() instead and remove new DetermineEdgeVersion and DetermineEdgeVersionReason types
        let output = Command::new("docker inspect")
            .arg(module_name)
            .output()
            .context(ErrorKind::DetermineEdgeVersion(
                DetermineEdgeVersionReason::DockerInspectFailed(module_name.to_owned()),
            ))?;
        if !output.status.success() {
            return Err(failure::Error::from(ErrorKind::DetermineEdgeVersion(
                DetermineEdgeVersionReason::DockerInspectExitCode(
                    output.status,
                    String::from_utf8_lossy(&*output.stderr).into(),
                ),
            )));
        }

        // Convert output text to json
        let output = String::from_utf8(output.stdout).context(ErrorKind::DetermineEdgeVersion(
            DetermineEdgeVersionReason::JsonParseError(module_name.to_owned()),
        ))?;
        let output_json: Value =
            serde_json::from_str(&output).context(ErrorKind::DetermineEdgeVersion(
                DetermineEdgeVersionReason::JsonParseError(module_name.to_owned()),
            ))?;

        // Grab sha256 value
        let re = Regex::new(r"sha256:([0-9a-fA-F]{64})")
            .expect("This hard-coded regex is expected to be valid.");
        let image_value: &str =
            output_json["Image"]
                .as_str()
                .ok_or(ErrorKind::DetermineEdgeVersion(
                    DetermineEdgeVersionReason::ImageKeyNotFound,
                ))?;
        let sha256_captures = re
            .captures(image_value)
            .ok_or(ErrorKind::DetermineEdgeVersion(
                DetermineEdgeVersionReason::ImageValueUnexpectedFormat,
            ))?;
        Ok(sha256_captures
            .get(1)
            .expect("unreachable: regex defines one capturing group")
            .as_str()
            .to_owned())
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

    fn init_latest_versions(&mut self, tokio_runtime: &mut tokio::runtime::Runtime) -> CheckResult {
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

        let request = hyper::Request::get("https://aka.ms/latest-aziot-edge")
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

        return CheckResult::Ok;
    }
}
