// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route {
    key_client: std::sync::Arc<futures_util::lock::Mutex<aziot_key_client_async::Client>>,
    cert_client: std::sync::Arc<futures_util::lock::Mutex<aziot_cert_client_async::Client>>,
    module_id: String,
    pid: libc::pid_t,
    hub_name: String,
    device_id: String,
    edge_ca_cert: String,
    edge_ca_key: String,
}

#[async_trait::async_trait]
impl http_common::server::Route for Route {
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2018_06_28)..)
    }

    type Service = crate::Service;
    fn from_uri(
        service: &Self::Service,
        path: &str,
        _query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        extensions: &http::Extensions,
    ) -> Option<Self> {
        let uri_regex = regex::Regex::new("^/modules/(?P<moduleId>[^/]+)/certificate/identity$")
            .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module_id = &captures["moduleId"];
        let module_id = percent_encoding::percent_decode_str(module_id)
            .decode_utf8()
            .ok()?;

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        Some(Route {
            key_client: service.key_client.clone(),
            cert_client: service.cert_client.clone(),
            module_id: module_id.into_owned(),
            pid,
            hub_name: service.hub_name.clone(),
            device_id: service.device_id.clone(),
            edge_ca_cert: service.edge_ca_cert.clone(),
            edge_ca_key: service.edge_ca_key.clone(),
        })
    }

    type GetResponse = ();

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();

    type PostBody = serde::de::IgnoredAny;
    type PostResponse = super::CertificateResponse;
    async fn post(
        self,
        _body: Option<Self::PostBody>,
    ) -> http_common::server::RouteResponse<Option<Self::PostResponse>> {
        edgelet_http::auth_caller(&self.module_id, self.pid)?;

        let cert_name = format!("aziot-edged/module/{}:identity", &self.module_id);
        let module_uri = format!(
            "URI: azureiot://{}/devices/{}/modules/{}",
            &self.hub_name, &self.device_id, &self.module_id
        );
        let subject_alt_name = vec![super::SubjectAltName::DNS(module_uri)];

        let csr_extensions = identity_cert_extensions().map_err(|_| {
            edgelet_http::error::server_error("failed to set identity csr extensions")
        })?;

        let keys = super::new_keys().map_err(|_| {
            edgelet_http::error::server_error("failed to generate identity csr keys")
        })?;

        let csr = super::new_csr(&self.module_id, keys, subject_alt_name, csr_extensions)
            .map_err(|_| edgelet_http::error::server_error("failed to generate identity csr"))?;

        let edge_ca_key = super::get_edge_ca(
            self.key_client,
            self.cert_client,
            self.edge_ca_cert,
            self.edge_ca_key,
            self.device_id,
        )
        .await?;

        todo!()
    }

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}

fn identity_cert_extensions(
) -> Result<openssl::stack::Stack<openssl::x509::X509Extension>, openssl::error::ErrorStack> {
    let mut csr_extensions = openssl::stack::Stack::new()?;

    let mut ext_key_usage = openssl::x509::extension::ExtendedKeyUsage::new();
    ext_key_usage.client_auth();

    let ext_key_usage = ext_key_usage.build()?;
    csr_extensions.push(ext_key_usage)?;

    Ok(csr_extensions)
}
