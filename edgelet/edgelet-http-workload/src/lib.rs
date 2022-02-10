// Copyright (c) Microsoft. All rights reserved.

mod module;
mod trust_bundle;

#[cfg(not(test))]
use aziot_cert_client_async::Client as CertClient;
#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;
#[cfg(not(test))]
use aziot_key_client_async::Client as KeyClient;

#[cfg(test)]
use test_common::client::CertClient;
#[cfg(test)]
use test_common::client::IdentityClient;
#[cfg(test)]
use test_common::client::KeyClient;

#[derive(Clone)]
pub struct Service<M>
where
    M: edgelet_core::ModuleRuntime,
{
    // This connector is needed to contruct sync aziot_key_clients when using aziot_key_openssl_engine.
    key_connector: http_common::Connector,

    key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
    identity_client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,

    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    config: WorkloadConfig,
}

impl<M> Service<M>
where
    M: edgelet_core::ModuleRuntime,
{
    #[cfg(not(test))]
    pub fn new(
        settings: &impl edgelet_settings::RuntimeSettings,
        runtime: M,
        device_info: &aziot_identity_common::AzureIoTSpec,
    ) -> Result<Self, http_common::ConnectorError> {
        let endpoints = settings.endpoints();

        let key_connector = http_common::Connector::new(endpoints.aziot_keyd_url())?;
        let key_client = aziot_key_client_async::Client::new(
            aziot_key_common_http::ApiVersion::V2020_09_01,
            key_connector.clone(),
            1,
        );
        let key_client = std::sync::Arc::new(futures_util::lock::Mutex::new(key_client));

        let cert_connector = http_common::Connector::new(endpoints.aziot_certd_url())?;
        let cert_client = aziot_cert_client_async::Client::new(
            aziot_cert_common_http::ApiVersion::V2020_09_01,
            cert_connector,
            1,
        );
        let cert_client = std::sync::Arc::new(futures_util::lock::Mutex::new(cert_client));

        let identity_connector = http_common::Connector::new(endpoints.aziot_identityd_url())?;
        let identity_client = aziot_identity_client_async::Client::new(
            aziot_identity_common_http::ApiVersion::V2020_09_01,
            identity_connector,
            1,
        );
        let identity_client = std::sync::Arc::new(futures_util::lock::Mutex::new(identity_client));

        let runtime = std::sync::Arc::new(futures_util::lock::Mutex::new(runtime));
        let config = WorkloadConfig::new(settings, device_info);

        Ok(Service {
            key_connector,
            key_client,
            cert_client,
            identity_client,
            runtime,
            config,
        })
    }

    pub async fn check_edge_ca(&self) -> Result<(), String> {
        let key_handle =
            module::cert::edge_ca_key_handle(self.key_client.clone(), &self.config.edge_ca_key)
                .await
                .map_err(|err| err.message)?;

        module::cert::check_edge_ca(
            self.cert_client.clone(),
            &self.config.edge_ca_cert,
            &self.config.device_id,
            &key_handle,
            self.key_connector.clone(),
        )
        .await
        .map_err(|err| err.message)?;

        Ok(())
    }

    // Test constructor used to create a test Workload Service.
    #[cfg(test)]
    pub fn new(runtime: M) -> Self {
        // Tests won't actually connect to keyd, so just put any URL in the key connector.
        let key_connector = url::Url::parse("unix:///tmp/test.sock").unwrap();
        let key_connector = http_common::Connector::new(&key_connector).unwrap();

        let key_client = KeyClient::default();
        let key_client = std::sync::Arc::new(futures_util::lock::Mutex::new(key_client));

        let cert_client = CertClient::default();
        let cert_client = std::sync::Arc::new(futures_util::lock::Mutex::new(cert_client));

        let identity_client = IdentityClient::default();
        let identity_client = std::sync::Arc::new(futures_util::lock::Mutex::new(identity_client));

        let runtime = std::sync::Arc::new(futures_util::lock::Mutex::new(runtime));

        let config = WorkloadConfig {
            hub_name: "test-hub.test.net".to_string(),
            device_id: "test-device".to_string(),
            trust_bundle: "test-trust-bundle".to_string(),
            manifest_trust_bundle: "test-manifest-trust-bundle".to_string(),
            dps_trust_bundle: "test-dps-trust-bundle".to_string(),
            edge_ca_cert: "test-ca-cert".to_string(),
            edge_ca_key: "test-ca-key".to_string(),
        };

        Service {
            key_connector,
            key_client,
            cert_client,
            identity_client,
            runtime,
            config,
        }
    }
}

http_common::make_service! {
    service: Service<M>,
    { <M> }
    {
        M: edgelet_core::ModuleRuntime + Send + Sync + 'static,
    }
    api_version: edgelet_http::ApiVersion,
    routes: [
        module::list::Route<M>,

        module::cert::identity::Route<M>,
        module::cert::server::Route<M>,

        module::data::decrypt::Route<M>,
        module::data::encrypt::Route<M>,
        module::data::sign::Route<M>,

        trust_bundle::Route<M>,
    ],
}

// The subset of the aziot-edged config needed for workload APIs.
#[derive(Clone, Debug)]
#[cfg_attr(test, derive(PartialEq))]
struct WorkloadConfig {
    hub_name: String,
    device_id: String,

    trust_bundle: String,
    manifest_trust_bundle: String,
    dps_trust_bundle: String,

    edge_ca_cert: String,
    edge_ca_key: String,
}

impl WorkloadConfig {
    pub fn new(
        settings: &impl edgelet_settings::RuntimeSettings,
        device_info: &aziot_identity_common::AzureIoTSpec,
    ) -> Self {
        let trust_bundle = settings
            .trust_bundle_cert()
            .unwrap_or(edgelet_settings::TRUST_BUNDLE_ALIAS)
            .to_string();

        let manifest_trust_bundle = settings
            .manifest_trust_bundle_cert()
            .unwrap_or(edgelet_settings::MANIFEST_TRUST_BUNDLE_ALIAS)
            .to_string();

        let edge_ca_cert = settings
            .edge_ca_cert()
            .unwrap_or(edgelet_settings::AZIOT_EDGED_CA_ALIAS)
            .to_string();
        let edge_ca_key = settings
            .edge_ca_key()
            .unwrap_or(edgelet_settings::AZIOT_EDGED_CA_ALIAS)
            .to_string();

        WorkloadConfig {
            hub_name: device_info.hub_name.clone(),
            device_id: device_info.device_id.0.clone(),

            trust_bundle,
            manifest_trust_bundle,
            dps_trust_bundle: settings.dps_trust_bundle().to_string(),

            edge_ca_cert,
            edge_ca_key,
        }
    }
}

#[cfg(test)]
mod tests {
    #[test]
    fn workload_config_defaults() {
        let device_info = aziot_identity_common::AzureIoTSpec {
            hub_name: "test-hub.test.net".to_string(),
            gateway_host: "gateway-host.test.net".to_string(),
            device_id: aziot_identity_common::DeviceId("test-device".to_string()),
            module_id: None,
            gen_id: None,
            auth: None,
        };

        let settings = edgelet_test_utils::Settings::default();

        // Check that default values are used when settings do not provide them.
        let config = super::WorkloadConfig::new(&settings, &device_info);
        assert_eq!(
            super::WorkloadConfig {
                hub_name: device_info.hub_name,
                device_id: device_info.device_id.0,

                trust_bundle: edgelet_settings::TRUST_BUNDLE_ALIAS.to_string(),
                manifest_trust_bundle: edgelet_settings::MANIFEST_TRUST_BUNDLE_ALIAS.to_string(),
                dps_trust_bundle: settings.dps_trust_bundle,

                edge_ca_cert: edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_string(),
                edge_ca_key: edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_string(),
            },
            config
        );
    }

    #[test]
    fn workload_config_settings() {
        let device_info = aziot_identity_common::AzureIoTSpec {
            hub_name: "test-hub.test.net".to_string(),
            gateway_host: "gateway-host.test.net".to_string(),
            device_id: aziot_identity_common::DeviceId("test-device".to_string()),
            module_id: None,
            gen_id: None,
            auth: None,
        };

        let settings = edgelet_test_utils::Settings {
            edge_ca_cert: Some("test-ca-cert".to_string()),
            edge_ca_key: Some("test-ca-key".to_string()),
            trust_bundle: Some("test-trust-bundle".to_string()),
            manifest_trust_bundle: Some("test-manifest-trust-bundle".to_string()),
            dps_trust_bundle: "test-dps-trust-bundle".to_string(),
        };

        // Check that values from settings are used when provided.
        let config = super::WorkloadConfig::new(&settings, &device_info);
        assert_eq!(
            super::WorkloadConfig {
                hub_name: device_info.hub_name,
                device_id: device_info.device_id.0,

                trust_bundle: "test-trust-bundle".to_string(),
                manifest_trust_bundle: "test-manifest-trust-bundle".to_string(),
                dps_trust_bundle: "test-dps-trust-bundle".to_string(),

                edge_ca_cert: "test-ca-cert".to_string(),
                edge_ca_key: "test-ca-key".to_string(),
            },
            config
        );
    }
}
