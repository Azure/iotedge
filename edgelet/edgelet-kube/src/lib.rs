// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::use_self
)]

mod constants;
mod convert;
mod error;
mod module;
mod registry;
mod runtime;
mod settings;

use std::convert::TryFrom;

pub use error::{Error, ErrorKind, MissingMetadataReason, Result};
pub use module::KubeModule;
pub use runtime::KubeModuleRuntime;
pub use settings::Settings;

use k8s_openapi::api::apps::v1 as api_apps;
use k8s_openapi::Resource;

type Deployment = api_apps::Deployment;

#[derive(Clone)]
pub struct KubeModuleOwner {
    name: String,
    api_version: String,
    kind: String,
    uid: String,
}

impl KubeModuleOwner {
    pub fn new(name: String, api_version: String, kind: String, uid: String) -> Self {
        KubeModuleOwner {
            name,
            api_version,
            kind,
            uid,
        }
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn api_version(&self) -> &str {
        &self.api_version
    }

    pub fn kind(&self) -> &str {
        &self.kind
    }

    pub fn uid(&self) -> &str {
        &self.uid
    }
}

impl TryFrom<Deployment> for KubeModuleOwner {
    type Error = Error;

    fn try_from(deployment: Deployment) -> Result<Self> {
        let metadata = deployment
            .metadata
            .as_ref()
            .ok_or(ErrorKind::MissingMetadata(
                MissingMetadataReason::DeploymentMetadata,
            ))?;
        Ok(Self {
            name: metadata
                .name
                .as_ref()
                .map(String::to_string)
                .ok_or(ErrorKind::MissingMetadata(MissingMetadataReason::Name))?,
            api_version: Deployment::api_version().to_string(),
            kind: Deployment::kind().to_string(),
            uid: metadata
                .uid
                .as_ref()
                .map(String::to_string)
                .ok_or(ErrorKind::MissingMetadata(MissingMetadataReason::Uid))?,
        })
    }
}

#[cfg(test)]
mod tests {
    use config::{Config, File, FileFormat};
    use futures::future;
    use hyper::service::Service;
    use hyper::{Body, Request, Response, StatusCode};
    use json_patch::merge;
    use native_tls::TlsConnector;
    use serde_json::{self, json, Value as JsonValue};
    use typed_headers::{mime, ContentLength, ContentType, HeaderMapExt};
    use url::Url;

    use edgelet_test_utils::web::ResponseFuture;
    use kube_client::{Client as KubeClient, Config as KubeConfig, Error, TokenSource};

    use crate::settings::Settings;
    use crate::KubeModuleOwner;
    use crate::KubeModuleRuntime;

    pub const PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME: &str = "device1-iotedged-proxy-trust-bundle";
    pub const PROXY_CONFIG_MAP_NAME: &str = "device1-iotedged-proxy-config";

    pub fn make_settings(merge_json: Option<JsonValue>) -> Settings {
        let mut config = Config::default();
        let mut config_json = json!({
            "provisioning": {
                "source": "manual",
                "device_connection_string": "HostName=moo.azure-devices.net;DeviceId=boo;SharedAccessKey=boo"
            },
            "agent": {
                "name": "edgeAgent",
                "type": "docker",
                "env": {},
                "config": {
                    "image": "mcr.microsoft.com/azureiotedge-agent:1.0",
                    "auth": {}
                }
            },
            "hostname": "default1",
            "connect": {
                "management_uri": "http://localhost:35000",
                "workload_uri": "http://localhost:35001"
            },
            "listen": {
                "management_uri": "http://localhost:35000",
                "workload_uri": "http://localhost:35001"
            },
            "homedir": "/var/lib/iotedge",
            "namespace": "default",
            "iot_hub_hostname": "iotHub",
            "device_id": "device1",
            "device_hub_selector": "",
            "proxy": {
               "image": "proxy:latest",
               "image_pull_policy": "IfNotPresent",
               "auth": {},
               "config_path": "/etc/traefik",
               "config_map_name": PROXY_CONFIG_MAP_NAME,
               "trust_bundle_path": "/etc/trust-bundle",
               "trust_bundle_config_map_name": PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME,
            },
        });

        if let Some(merge_json) = merge_json {
            merge(&mut config_json, &merge_json);
        }

        config
            .merge(File::from_str(&config_json.to_string(), FileFormat::Json))
            .unwrap();

        config.try_into().unwrap()
    }

    pub fn create_module_owner() -> KubeModuleOwner {
        KubeModuleOwner::new(
            "iotedged".to_string(),
            "v1".to_string(),
            "Deployment".to_string(),
            "123".to_string(),
        )
    }

    #[derive(Clone)]
    pub struct TestTokenSource;

    impl TokenSource for TestTokenSource {
        type Error = Error;

        fn get(&self) -> kube_client::error::Result<Option<String>> {
            Ok(None)
        }
    }

    pub fn create_runtime<S: Service>(
        settings: Settings,
        service: S,
    ) -> KubeModuleRuntime<TestTokenSource, S> {
        let client = KubeClient::with_client(get_config(), service);

        KubeModuleRuntime::new(client, settings)
    }

    pub fn get_config() -> KubeConfig<TestTokenSource> {
        KubeConfig::new(
            Url::parse("https://localhost:443").unwrap(),
            "/api".to_string(),
            TestTokenSource,
            TlsConnector::new().unwrap(),
        )
    }

    pub fn response(
        status_code: StatusCode,
        response: impl Fn() -> String + Clone + Send + 'static,
    ) -> ResponseFuture {
        let response = response();
        let response_len = response.len();

        let mut response = Response::new(response.into());
        *response.status_mut() = status_code;
        response
            .headers_mut()
            .typed_insert(&ContentLength(response_len as u64));
        response
            .headers_mut()
            .typed_insert(&ContentType(mime::APPLICATION_JSON));

        Box::new(future::ok(response)) as ResponseFuture
    }

    pub fn not_found_handler(_: Request<Body>) -> ResponseFuture {
        let response = Response::builder()
            .status(StatusCode::NOT_FOUND)
            .body(Body::default())
            .unwrap();

        Box::new(future::ok(response))
    }
}
