use std::process::Command;

use anyhow::Context;
use futures::{future, Future, Stream};
use regex::Regex;

use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;

use crate::check::{checker::Checker, Check, CheckResult};
use crate::error::{Error, FetchLatestVersionsReason};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct AziotEdgedVersion {
    actual_version: Option<String>,
    expected_version: Option<String>,
}

impl Checker for AziotEdgedVersion {
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
        let latest_versions =
            if let Some(expected_aziot_edged_version) = &check.expected_aziot_edged_version {
                future::Either::A(future::ok::<_, anyhow::Error>(crate::LatestVersions {
                    aziot_edge: expected_aziot_edged_version.clone(),
                }))
            } else {
                if check.parent_hostname.is_some() {
                    // This is a nested Edge device so it may not be able to access aka.ms or github.com.
                    // In the best case the request would be blocked immediately, but in the worst case it may take a long time to time out.
                    // The user didn't provide the `expected_aziot_edged_version` param on the CLI, so we just ignore this check.
                    return CheckResult::Ignored;
                }

                let proxy = std::env::var("HTTPS_PROXY")
                    .ok()
                    .or_else(|| std::env::var("https_proxy").ok())
                    .map(|proxy| proxy.parse::<hyper::Uri>())
                    .transpose()
                    .context(Error::FetchLatestVersions(
                        FetchLatestVersionsReason::CreateClient,
                    ));
                let hyper_client = proxy.and_then(|proxy| {
                    MaybeProxyClient::new(proxy, None, None).context(Error::FetchLatestVersions(
                        FetchLatestVersionsReason::CreateClient,
                    ))
                });
                let hyper_client = match hyper_client {
                    Ok(hyper_client) => hyper_client,
                    Err(err) => return CheckResult::Failed(err),
                };

                let request = hyper::Request::get("https://aka.ms/latest-aziot-edge")
                    .body(hyper::Body::default())
                    .expect("can't fail to create request");

                future::Either::B(
                    hyper_client
                        .call(request)
                        .then(|response| -> anyhow::Result<_> {
                            let response = response.context(Error::FetchLatestVersions(
                                FetchLatestVersionsReason::GetResponse,
                            ))?;
                            Ok(response)
                        })
                        .and_then(move |response| match response.status() {
                            status_code if status_code.is_redirection() => {
                                let uri = response
                                    .headers()
                                    .get(hyper::header::LOCATION)
                                    .ok_or(Error::FetchLatestVersions(
                                        FetchLatestVersionsReason::InvalidOrMissingLocationHeader,
                                    ))?
                                    .to_str()
                                    .context(Error::FetchLatestVersions(
                                        FetchLatestVersionsReason::InvalidOrMissingLocationHeader,
                                    ))?;
                                let request = hyper::Request::get(uri)
                                    .body(hyper::Body::default())
                                    .expect("can't fail to create request");
                                Ok(hyper_client.call(request).map_err(|err| {
                                    anyhow::anyhow!(err).context(Error::FetchLatestVersions(
                                        FetchLatestVersionsReason::GetResponse,
                                    ))
                                }))
                            }
                            status_code => Err(Error::FetchLatestVersions(
                                FetchLatestVersionsReason::ResponseStatusCode(status_code),
                            )
                            .into()),
                        })
                        .flatten()
                        .and_then(|response| -> anyhow::Result<_> {
                            match response.status() {
                                hyper::StatusCode::OK => {
                                    Ok(response.into_body().concat2().map_err(|err| {
                                        anyhow::anyhow!(err).context(Error::FetchLatestVersions(
                                            FetchLatestVersionsReason::GetResponse,
                                        ))
                                    }))
                                }
                                status_code => Err(Error::FetchLatestVersions(
                                    FetchLatestVersionsReason::ResponseStatusCode(status_code),
                                )
                                .into()),
                            }
                        })
                        .flatten()
                        .and_then(|body| {
                            Ok(serde_json::from_slice(&body).context(
                                Error::FetchLatestVersions(FetchLatestVersionsReason::GetResponse),
                            )?)
                        }),
                )
            };

        self.inner_execute(check, tokio_runtime.block_on(latest_versions).map_err(Some))
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl AziotEdgedVersion {
    fn inner_execute(
        &mut self,
        check: &mut Check,
        latest_versions: Result<crate::LatestVersions, Option<anyhow::Error>>,
    ) -> anyhow::Result<CheckResult> {
        let latest_versions = match latest_versions {
            Ok(latest_versions) => latest_versions,
            Err(mut err) => match err.take() {
                Some(err) => return Ok(CheckResult::Warning(err)),
                None => return Ok(CheckResult::Skipped),
            },
        };
        self.expected_version = Some(latest_versions.aziot_edge.to_owned());

        let mut process = Command::new(&check.aziot_edged);
        process.arg("--version");

        let output = process
            .output()
            .context("Could not spawn aziot-edged process")?;
        if !output.status.success() {
            return Err(anyhow::anyhow!(
                "aziot-edged returned {}, stderr = {}",
                output.status,
                String::from_utf8_lossy(&*output.stderr),
            )
            .context("Could not spawn aziot-edged process"));
        }

        let output = String::from_utf8(output.stdout)
            .context("Could not parse output of aziot-edged --version")?;

        let aziot_edged_version_regex = Regex::new(r"^aziot-edged ([^ ]+)(?: \(.*\))?$")
            .expect("This hard-coded regex is expected to be valid.");
        let captures = aziot_edged_version_regex
            .captures(output.trim())
            .with_context(|| format!("output {:?} does not match expected format", output))
            .context("Could not parse output of aziot-edged --version")?;
        let version = captures
            .get(1)
            .expect("unreachable: regex defines one capturing group")
            .as_str();
        self.actual_version = Some(version.to_owned());

        check.additional_info.aziot_edged_version = Some(version.to_owned());

        if version != latest_versions.aziot_edge {
            return Ok(CheckResult::Warning(
            anyhow::anyhow!(
                "Installed IoT Edge daemon has version {} but {} is the latest stable version available.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                version, latest_versions.aziot_edge,
            )
        ));
        }

        Ok(CheckResult::Ok)
    }
}
