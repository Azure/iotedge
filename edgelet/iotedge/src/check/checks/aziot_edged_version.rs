use std::process::Command;

use failure::{self, Context, Fail, ResultExt};
use futures::{future, Future, Stream};
use regex::Regex;

use edgelet_core::RuntimeSettings;
use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;

use crate::check::{checker::Checker, Check, CheckResult};
use crate::error::{Error, ErrorKind, FetchLatestVersionsReason};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct AziotEdgedVersion {
    actual_version: Option<String>,
    expected_version: Option<String>,
}

impl Checker for AziotEdgedVersion {
    fn id(&self) -> &'static str {
        "aziot-edged-version"
    }
    fn description(&self) -> &'static str {
        "latest security daemon"
    }
    fn execute(
        &mut self,
        check: &mut Check,
        tokio_runtime: &mut tokio::runtime::Runtime,
    ) -> CheckResult {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return CheckResult::Skipped;
        };

        if settings.parent_hostname().is_some() {
            return CheckResult::Ignored;
        }

        let latest_versions = if let Some(expected_aziot_edged_version) =
            &check.expected_aziot_edged_version
        {
            future::Either::A(future::ok::<_, Error>(crate::LatestVersions {
                iotedged: expected_aziot_edged_version.clone(),
            }))
        } else {
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

            let request = hyper::Request::get("https://aka.ms/latest-iotedge-stable")
                .body(hyper::Body::default())
                .expect("can't fail to create request");

            future::Either::B(
                hyper_client
                    .call(request)
                    .then(|response| -> Result<_, Error> {
                        let response = response.context(ErrorKind::FetchLatestVersions(
                            FetchLatestVersionsReason::GetResponse,
                        ))?;
                        Ok(response)
                    })
                    .and_then(move |response| match response.status() {
                        hyper::StatusCode::MOVED_PERMANENTLY => {
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
                            hyper::StatusCode::OK => {
                                Ok(response.into_body().concat2().map_err(|err| {
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
                        }
                    })
                    .flatten()
                    .and_then(|body| {
                        Ok(serde_json::from_slice(&body).context(
                            ErrorKind::FetchLatestVersions(FetchLatestVersionsReason::GetResponse),
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
        latest_versions: Result<crate::LatestVersions, Option<Error>>,
    ) -> Result<CheckResult, failure::Error> {
        let latest_versions = match latest_versions {
            Ok(latest_versions) => latest_versions,
            Err(mut err) => match err.take() {
                Some(err) => return Ok(CheckResult::Warning(err.into())),
                None => return Ok(CheckResult::Skipped),
            },
        };
        self.expected_version = Some(latest_versions.iotedged.to_owned());

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
        let version = captures
            .get(1)
            .expect("unreachable: regex defines one capturing group")
            .as_str();
        self.actual_version = Some(version.to_owned());

        check.additional_info.aziot_edged_version = Some(version.to_owned());

        if version != latest_versions.iotedged {
            return Ok(CheckResult::Warning(
            Context::new(format!(
                "Installed IoT Edge daemon has version {} but {} is the latest stable version available.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                version, latest_versions.iotedged,
            ))
            .into(),
        ));
        }

        Ok(CheckResult::Ok)
    }
}
