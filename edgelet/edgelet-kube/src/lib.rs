// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::use_self
)]

mod constants;
mod convert;
mod error;
mod module;
mod runtime;
mod settings;

pub use error::{Error, ErrorKind};
pub use module::KubeModule;
pub use runtime::KubeModuleRuntime;
pub use settings::Settings;

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
            "use_pvc": true,
            "iot_hub_hostname": "iotHub",
            "device_id": "device1",
            "proxy_image": "proxy:latest",
            "proxy_config_path": "/etc/traefik",
            "proxy_config_map_name": PROXY_CONFIG_MAP_NAME,
            "proxy_trust_bundle_path": "/etc/trust-bundle",
            "proxy_trust_bundle_config_map_name": PROXY_TRUST_BUNDLE_CONFIG_MAP_NAME,
            "image_pull_policy": "IfNotPresent",
            "device_hub_selector": "",
        });

        if let Some(merge_json) = merge_json {
            merge(&mut config_json, &merge_json);
        }

        config
            .merge(File::from_str(&config_json.to_string(), FileFormat::Json))
            .unwrap();

        config.try_into().unwrap()
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
