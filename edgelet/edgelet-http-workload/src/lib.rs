// Copyright (c) Microsoft. All rights reserved.

mod edge_ca;
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
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    // This connector is needed to contruct sync aziot_key_clients when using aziot_key_openssl_engine.
    key_connector: http_common::Connector,

    key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
    identity_client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,

    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    renewal_engine: Option<
        std::sync::Arc<
            futures_util::lock::Mutex<cert_renewal::RenewalEngine<edge_ca::EdgeCaRenewal<M>>>,
        >,
    >,
    config: WorkloadConfig,
}

impl<M> Service<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync + 'static,
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

        let renewal_engine = if config.edge_ca_auto_renew.is_some() {
            let engine = cert_renewal::engine::new();

            Some(engine)
        } else {
            None
        };

        Ok(Service {
            key_connector,
            key_client,
            cert_client,
            identity_client,
            runtime,
            renewal_engine,
            config,
        })
    }

    pub async fn check_edge_ca(&self) -> Result<(), String> {
        // Create the Edge CA if it does not exist.
        let key_handle = {
            let key_client = self.key_client.lock().await;

            key_client
                .create_key_pair_if_not_exists(&self.config.edge_ca_key, Some("rsa-2048:*"))
                .await
                .map_err(|err| err.to_string())?
        };

        {
            let cert_client = self.cert_client.lock().await;

            if cert_client
                .get_cert(&self.config.edge_ca_cert)
                .await
                .is_err()
            {
                log::info!("Requesting new Edge CA certificate...");

                let keys = edge_ca::keys(self.key_connector.clone(), &key_handle)?;
                let extensions = edge_ca::extensions()
                    .map_err(|_| "failed to set edge ca csr extensions".to_string())?;

                let common_name = format!("aziot-edge CA {}", self.config.device_id);
                let csr = module::cert::new_csr(common_name, keys, Vec::new(), extensions)
                    .map_err(|_| "failed to generate edge ca csr".to_string())?;

                cert_client
                    .create_cert(&self.config.edge_ca_cert, &csr, None)
                    .await
                    .map_err(|_| "failed to create edge ca cert".to_string())?;

                log::info!("Created new Edge CA certificate");
            } else {
                log::info!("Using existing Edge CA certificate");
            }
        }

        if let Some(engine) = &self.renewal_engine {
            let policy = self
                .config
                .edge_ca_auto_renew
                .as_ref()
                .expect("auto renew config should exist if engine exists")
                .policy
                .to_owned();

            let rotate_key = self
                .config
                .edge_ca_auto_renew
                .as_ref()
                .expect("auto renew config should exist if engine exists")
                .rotate_key;

            let interface = edge_ca::EdgeCaRenewal::new(
                rotate_key,
                &self.config.edge_ca_cert,
                self.runtime.clone(),
                self.cert_client.clone(),
                self.key_client.clone(),
                self.key_connector.clone(),
            );

            cert_renewal::engine::add_credential(
                engine,
                &self.config.edge_ca_cert,
                &self.config.edge_ca_key,
                policy,
                interface,
            )
            .await
            .map_err(|err| format!("failed to configure Edge CA auto renew: {}", err))?;
        } else {
            log::warn!(
                "Auto renewal of the Edge CA is not configured. Edge CA will not be automatically renewed",
            );
        }

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
            edge_ca_cert: "test-ca-cert".to_string(),
            edge_ca_key: "test-ca-key".to_string(),
            edge_ca_auto_renew: None,
        };

        Service {
            key_connector,
            key_client,
            cert_client,
            identity_client,
            runtime,
            renewal_engine: None,
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

    edge_ca_cert: String,
    edge_ca_key: String,
    edge_ca_auto_renew: Option<cert_renewal::AutoRenewConfig>,
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
        let edge_ca_auto_renew = settings.edge_ca_auto_renew().to_owned();

        WorkloadConfig {
            hub_name: device_info.hub_name.clone(),
            device_id: device_info.device_id.0.clone(),

            trust_bundle,
            manifest_trust_bundle,

            edge_ca_cert,
            edge_ca_key,
            edge_ca_auto_renew,
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

                edge_ca_cert: edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_string(),
                edge_ca_key: edgelet_settings::AZIOT_EDGED_CA_ALIAS.to_string(),
                edge_ca_auto_renew: None,
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
            edge_ca_auto_renew: None,
            trust_bundle: Some("test-trust-bundle".to_string()),
            manifest_trust_bundle: Some("test-manifest-trust-bundle".to_string()),
        };

        // Check that values from settings are used when provided.
        let config = super::WorkloadConfig::new(&settings, &device_info);
        assert_eq!(
            super::WorkloadConfig {
                hub_name: device_info.hub_name,
                device_id: device_info.device_id.0,

                trust_bundle: "test-trust-bundle".to_string(),
                manifest_trust_bundle: "test-manifest-trust-bundle".to_string(),

                edge_ca_cert: "test-ca-cert".to_string(),
                edge_ca_key: "test-ca-key".to_string(),
                edge_ca_auto_renew: None,
            },
            config
        );
    }
}
