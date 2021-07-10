// Copyright (c) Microsoft. All rights reserved.

mod module;
mod trust_bundle;

use edgelet_core::RuntimeSettings;

#[derive(Clone)]
pub struct Service {
    cert_client: std::sync::Arc<futures_util::lock::Mutex<aziot_cert_client_async::Client>>,
    trust_bundle: String,
    manifest_trust_bundle: String,
}

impl Service {
    pub fn new(
        settings: &impl edgelet_core::RuntimeSettings,
    ) -> Result<Self, http_common::ConnectorError> {
        let endpoints = settings.endpoints();

        let cert_connector = http_common::Connector::new(endpoints.aziot_certd_url())?;
        let cert_client = aziot_cert_client_async::Client::new(
            aziot_cert_common_http::ApiVersion::V2020_09_01,
            cert_connector,
        );
        let cert_client = std::sync::Arc::new(futures_util::lock::Mutex::new(cert_client));

        let trust_bundle = settings
            .trust_bundle_cert()
            .unwrap_or(edgelet_core::crypto::TRUST_BUNDLE_ALIAS)
            .to_string();

        let manifest_trust_bundle = settings
            .manifest_trust_bundle_cert()
            .unwrap_or(edgelet_core::crypto::MANIFEST_TRUST_BUNDLE_ALIAS)
            .to_string();

        Ok(Service {
            cert_client,
            trust_bundle,
            manifest_trust_bundle,
        })
    }
}

http_common::make_service! {
    service: Service,
    api_version: edgelet_http::ApiVersion,
    routes: [
        module::list::Route,

        trust_bundle::Route,
    ],
}
