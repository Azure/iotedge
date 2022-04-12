use anyhow::Context;
use regex::Regex;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};
use crate::error::{Error, FetchLatestVersionsReason};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct AziotEdgedVersion {
    actual_version: Option<String>,
    expected_version: Option<String>,
}

#[async_trait::async_trait]
impl Checker for AziotEdgedVersion {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "aziot-edge-version",
            description: "aziot-edge package is up-to-date",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        // let request = hyper::Request::get("https://aka.ms/latest-aziot-edge")

        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl AziotEdgedVersion {
    async fn get_version(&mut self, check: &Check) -> anyhow::Result<crate::LatestVersions> {
        let proxy = check
            .proxy_uri
            .as_ref()
            .map(|proxy| proxy.parse::<hyper::Uri>())
            .transpose()
            .context(Error::FetchLatestVersions(
                FetchLatestVersionsReason::CreateClient,
            ))?;

        let connector = http_common::MaybeProxyConnector::new(proxy, None, &[])
            .context("could not initialize HTTP connector")?;
        let client: hyper::Client<_, hyper::Body> = hyper::Client::builder().build(connector);

        let mut uri: hyper::Uri = "https://aka.ms/latest-aziot-edge"
            .parse()
            .expect("hard-coded URI cannot fail to parse");
        let latest_versions: crate::LatestVersions = loop {
            let req = {
                let mut req = hyper::Request::new(Default::default());
                *req.uri_mut() = uri.clone();
                req
            };

            let res = client
                .request(req)
                .await
                .context(Error::FetchLatestVersions(
                    FetchLatestVersionsReason::GetResponse,
                ))?;
            match res.status() {
                status_code if status_code.is_redirection() => {
                    uri = res
                        .headers()
                        .get(hyper::header::LOCATION)
                        .ok_or(Error::FetchLatestVersions(
                            FetchLatestVersionsReason::InvalidOrMissingLocationHeader,
                        ))?
                        .to_str()
                        .map_err(|_| {
                            Error::FetchLatestVersions(
                                FetchLatestVersionsReason::InvalidOrMissingLocationHeader,
                            )
                        })?
                        .parse()
                        .map_err(|_| {
                            Error::FetchLatestVersions(
                                FetchLatestVersionsReason::InvalidOrMissingLocationHeader,
                            )
                        })?;
                }

                hyper::StatusCode::OK => {
                    let body = hyper::body::aggregate(res.into_body())
                        .await
                        .context("could not read HTTP response")?;
                    let body = serde_json::from_reader(hyper::body::Buf::reader(body))
                        .context("could not read HTTP response")?;
                    break body;
                }

                status_code => {
                    return Err(Error::FetchLatestVersions(
                        FetchLatestVersionsReason::ResponseStatusCode(status_code),
                    )
                    .into())
                }
            }
        };

        Ok(latest_versions)
    }

    async fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        let latest_versions =
            if let Some(expected_aziot_edged_version) = &check.expected_aziot_edged_version {
                crate::LatestVersions {
                    aziot_edge: expected_aziot_edged_version.clone(),
                }
            } else {
                if check.parent_hostname.is_some() {
                    // This is a nested Edge device so it may not be able to access aka.ms or github.com.
                    // In the best case the request would be blocked immediately, but in the worst case it may take a long time to time out.
                    // The user didn't provide the `expected_aziot_edged_version` param on the CLI, so we just ignore this check.
                    return Ok(CheckResult::Ignored);
                }

                self.get_version(check).await?
            };
        self.expected_version = Some(latest_versions.aziot_edge.clone());

        let mut process = tokio::process::Command::new(&check.aziot_edged);
        process.arg("--version");

        let output = process
            .output()
            .await
            .context("Could not spawn aziot-edged process")?;
        if !output.status.success() {
            return Err(anyhow::Error::msg(format!(
                "aziot-edged returned {}, stderr = {}",
                output.status,
                String::from_utf8_lossy(&*output.stderr),
            ))
            .context("Could not spawn aziot-edged process"));
        }

        let output = String::from_utf8(output.stdout)
            .context("Could not parse output of aziot-edged --version")?;

        let aziot_edged_version_regex = Regex::new(r"^aziot-edged ([^ ]+)(?: \(.*\))?$")
            .expect("This hard-coded regex is expected to be valid.");
        let captures = aziot_edged_version_regex
            .captures(output.trim())
            .ok_or_else(|| {
                anyhow::Error::msg(format!(
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

        if version != latest_versions.aziot_edge {
            return Ok(CheckResult::Warning(
            anyhow::Error::msg(format!(
                "Installed IoT Edge daemon has version {} but {} is the latest stable version available.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                version, latest_versions.aziot_edge,
            )),
        ));
        }

        Ok(CheckResult::Ok)
    }
}
