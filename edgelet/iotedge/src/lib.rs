// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::let_and_return,
    clippy::let_unit_value,
    clippy::missing_errors_doc,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::similar_names,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity,
    clippy::use_self
)]

use std::fmt::{self, Display};

use failure::{self, Fail, ResultExt};
use futures::{Future, Stream};
use serde_derive::{Deserialize, Serialize};

mod check;
pub mod config;
mod error;
mod list;
mod logs;
mod restart;
mod support_bundle;
mod system;
mod unknown;
mod version;

use edgelet_http::client::ClientImpl;
use edgelet_http::MaybeProxyClient;

pub use crate::check::{Check, OutputFormat};
pub use crate::error::{Error, ErrorKind, FetchLatestVersionsReason};
pub use crate::list::List;
pub use crate::logs::Logs;
pub use crate::restart::Restart;
pub use crate::support_bundle::SupportBundleCommand;
pub use crate::system::System;
pub use crate::unknown::Unknown;
pub use crate::version::Version;

pub trait Command {
    type Future: Future<Item = ()> + Send;

    fn execute(self) -> Self::Future;
}

#[derive(Debug, Default, Deserialize, Serialize)]
pub struct LatestVersions {
    #[serde(rename = "aziot-edge")]
    pub aziot_edge: String,
    #[serde(rename = "azureiotedge-agent")]
    pub aziot_edge_agent: AziotEdgeModuleVersion,
    #[serde(rename = "azureiotedge-hub")]
    pub aziot_edge_hub: AziotEdgeModuleVersion,
}

#[derive(Debug, Default, Deserialize, Serialize)]
pub struct AziotEdgeModuleVersion {
    #[serde(rename = "linux-amd64")]
    pub linux_amd64: DockerImageInfo,
    #[serde(rename = "linux-arm32v7")]
    pub linux_arm32v7: DockerImageInfo,
    #[serde(rename = "linux-arm64v8")]
    pub linux_arm64v8: DockerImageInfo,
}

#[derive(Clone, Debug, Default, Deserialize, Serialize)]
pub struct DockerImageInfo {
    #[serde(rename = "repository")]
    pub repository: String,
    #[serde(rename = "image-tag")]
    pub image_tag: String,
    #[serde(rename = "image-id")]
    pub image_id: String,
}

impl Display for DockerImageInfo {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let mut id_beg: usize = 0;
        let mut id_end: usize = self.image_id.len();
        if (self.image_id.len() > 18) && (&self.image_id[0..7] == "sha256:") {
            id_beg = 7;
            id_end = 19;
        }
        write!(
            f,
            "{}:{} (Image ID: {})",
            self.repository,
            self.image_tag,
            &self.image_id[id_beg..id_end]
        )
    }
}

impl LatestVersions {
    pub fn get_latest_versions(
        tokio_runtime: &mut tokio::runtime::Runtime,
        latest_versions_url: &str,
    ) -> Result<LatestVersions, crate::Error> {
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
        })?;

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

        tokio_runtime.block_on(latest_versions_fut)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use httpmock::Method::GET;
    use httpmock::MockServer;

    #[test]
    fn test_get_latest_versions() {
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
                            \"repository\": \"mcr.microsoft.com/azureiotedge-agent\",
                            \"image-tag\": \"1.2.3-linux-amd64\",
                            \"image-id\":  \"sha256:4d911da05d9497d975b400b464d64e42358d172f220fdbc4b0498beaa7c0154e\"
                        },
                        \"linux-arm32v7\": {
                            \"repository\": \"mcr.microsoft.com/azureiotedge-agent\",                            
                            \"image-tag\": \"1.2.3-linux-arm32v7\",
                            \"image-id\":  \"sha256:41f939cdb2c42a1f96dadc39b03e6d02cfb1ecadca8d50ead7f2480d8b7a118a\"
                        },
                        \"linux-arm64v8\": {
                            \"repository\": \"mcr.microsoft.com/azureiotedge-agent\",                            
                            \"image-tag\": \"1.2.3-linux-arm64v8\",
                            \"image-id\":  \"sha256:17b4f95bde627c7fe02267d4fcf7271e3ecab49f1e19bca2f4c5622f3dda5cec\"
                        }
                    },
                    \"azureiotedge-hub\": {
                        \"linux-amd64\": {
                            \"repository\": \"mcr.microsoft.com/azureiotedge-hub\",                            
                            \"image-tag\": \"1.2.3-linux-amd64\",
                            \"image-id\":  \"sha256:23f633ecd57a212f010392e1e944d1e067b84e67460ed5f001390b9f001944c7\"
                        },
                        \"linux-arm32v7\": {
                            \"repository\": \"mcr.microsoft.com/azureiotedge-hub\",                                                        
                            \"image-tag\": \"1.2.3-linux-arm32v7\",
                            \"image-id\":  \"sha256:a8a47588d28c6f1ece90b3b7901a504693a4c46b1b950967e4859e70f2de606f\"
                        },
                        \"linux-arm64v8\": {
                            \"repository\": \"mcr.microsoft.com/azureiotedge-hub\",                                                        
                            \"image-tag\": \"1.2.3-linux-arm64v8\",
                            \"image-id\":  \"sha256:8eb93ea054de87638ee9dcc22066a8454bdbbcc6e9857a1ea15c383b1085f781\"
                        } 
                    }
                }");
        });

        let mut runtime = tokio::runtime::Runtime::new().unwrap();
        let latest_version_res =
            LatestVersions::get_latest_versions(&mut runtime, &server.url("/latest_versions"));

        assert!(latest_version_res.is_ok());

        let latest_versions = latest_version_res.unwrap();
        assert_eq!(latest_versions.aziot_edge, "1.2.3");
        assert_eq!(
            latest_versions.aziot_edge_agent.linux_amd64.image_id,
            "sha256:4d911da05d9497d975b400b464d64e42358d172f220fdbc4b0498beaa7c0154e".to_owned()
        );
        assert_eq!(
            latest_versions.aziot_edge_agent.linux_arm32v7.image_id,
            "sha256:41f939cdb2c42a1f96dadc39b03e6d02cfb1ecadca8d50ead7f2480d8b7a118a".to_owned()
        );
        assert_eq!(
            latest_versions.aziot_edge_agent.linux_arm64v8.image_id,
            "sha256:17b4f95bde627c7fe02267d4fcf7271e3ecab49f1e19bca2f4c5622f3dda5cec".to_owned()
        );
        assert_eq!(
            latest_versions.aziot_edge_hub.linux_amd64.image_id,
            "sha256:23f633ecd57a212f010392e1e944d1e067b84e67460ed5f001390b9f001944c7".to_owned()
        );
        assert_eq!(
            latest_versions.aziot_edge_hub.linux_arm32v7.image_id,
            "sha256:a8a47588d28c6f1ece90b3b7901a504693a4c46b1b950967e4859e70f2de606f".to_owned()
        );
        assert_eq!(
            latest_versions.aziot_edge_hub.linux_arm64v8.image_id,
            "sha256:8eb93ea054de87638ee9dcc22066a8454bdbbcc6e9857a1ea15c383b1085f781".to_owned()
        );
    }
}
