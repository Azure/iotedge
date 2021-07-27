// Copyright (c) Microsoft. All rights reserved.

mod module;
mod trust_bundle;

// The subset of the aziot-edged config needed for workload APIs.
#[derive(Clone)]
struct WorkloadConfig {
    hub_name: String,
    device_id: String,

    trust_bundle: String,
    manifest_trust_bundle: String,

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
            .unwrap_or("aziot-edged-trust-bundle")
            .to_string();

        let manifest_trust_bundle = settings
            .manifest_trust_bundle_cert()
            .unwrap_or("aziot-edged-manifest-trust-bundle")
            .to_string();

        let edge_ca_cert = settings
            .edge_ca_cert()
            .unwrap_or("aziot-edged-ca")
            .to_string();
        let edge_ca_key = settings
            .edge_ca_key()
            .unwrap_or("aziot-edged-ca")
            .to_string();

        WorkloadConfig {
            hub_name: device_info.hub_name.clone(),
            device_id: device_info.device_id.0.clone(),

            trust_bundle,
            manifest_trust_bundle,

            edge_ca_cert,
            edge_ca_key,
        }
    }
}

#[derive(Clone)]
pub struct Service<M>
where
    M: edgelet_core::ModuleRuntime,
{
    // This connector is needed to contruct sync aziot_key_clients when using aziot_key_openssl_engine.
    key_connector: http_common::Connector,

    key_client: std::sync::Arc<futures_util::lock::Mutex<aziot_key_client_async::Client>>,
    cert_client: std::sync::Arc<futures_util::lock::Mutex<aziot_cert_client_async::Client>>,
    identity_client: std::sync::Arc<futures_util::lock::Mutex<aziot_identity_client_async::Client>>,

    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    config: WorkloadConfig,
}

impl<M> Service<M>
where
    M: edgelet_core::ModuleRuntime + Clone + Send + Sync + 'static,
{
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
        );
        let key_client = std::sync::Arc::new(futures_util::lock::Mutex::new(key_client));

        let cert_connector = http_common::Connector::new(endpoints.aziot_certd_url())?;
        let cert_client = aziot_cert_client_async::Client::new(
            aziot_cert_common_http::ApiVersion::V2020_09_01,
            cert_connector,
        );
        let cert_client = std::sync::Arc::new(futures_util::lock::Mutex::new(cert_client));

        let identity_connector = http_common::Connector::new(endpoints.aziot_identityd_url())?;
        let identity_client = aziot_identity_client_async::Client::new(
            aziot_identity_common_http::ApiVersion::V2020_09_01,
            identity_connector,
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
}

http_common::make_service! {
    service: Service<M>,
    { <M> }
    { M: edgelet_core::ModuleRuntime + Send + Sync + 'static }
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
