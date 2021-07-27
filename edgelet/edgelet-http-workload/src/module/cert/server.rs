// Copyright (c) Microsoft. All rights reserved.

use std::str::FromStr;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    module_id: String,
    gen_id: String,
    pid: libc::pid_t,
    api: super::CertApi,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
}

#[derive(Debug, serde::Deserialize)]
pub(crate) struct ServerCertificateRequest {
    #[serde(rename = "commonName")]
    common_name: String,
}

#[async_trait::async_trait]
impl<M> http_common::server::Route for Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    type ApiVersion = edgelet_http::ApiVersion;
    fn api_version() -> &'static dyn http_common::DynRangeBounds<Self::ApiVersion> {
        &((edgelet_http::ApiVersion::V2018_06_28)..)
    }

    type Service = crate::Service<M>;
    fn from_uri(
        service: &Self::Service,
        path: &str,
        _query: &[(std::borrow::Cow<'_, str>, std::borrow::Cow<'_, str>)],
        extensions: &http::Extensions,
    ) -> Option<Self> {
        let uri_regex = regex::Regex::new(
            "^/modules/(?P<moduleId>[^/]+)/genid/(?P<genId>[^/]+)/certificate/server$",
        )
        .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module_id = &captures["moduleId"];
        let module_id = percent_encoding::percent_decode_str(module_id)
            .decode_utf8()
            .ok()?;

        let gen_id = &captures["genId"];
        let gen_id = percent_encoding::percent_decode_str(gen_id)
            .decode_utf8()
            .ok()?;

        let pid = match extensions.get::<Option<libc::pid_t>>().cloned().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        let api = super::CertApi::new(
            service.key_connector.clone(),
            service.key_client.clone(),
            service.cert_client.clone(),
            &service.config,
        );

        Some(Route {
            module_id: module_id.into_owned(),
            gen_id: gen_id.into_owned(),
            pid,
            api,
            runtime: service.runtime.clone(),
        })
    }

    type GetResponse = ();

    type DeleteBody = serde::de::IgnoredAny;
    type DeleteResponse = ();

    type PostBody = ServerCertificateRequest;
    type PostResponse = super::CertificateResponse;
    async fn post(
        self,
        body: Option<Self::PostBody>,
    ) -> http_common::server::RouteResponse<Option<Self::PostResponse>> {
        edgelet_http::auth_caller(&self.module_id, self.pid, &self.runtime)?;

        let common_name = match body {
            Some(body) => body.common_name,
            None => return Err(edgelet_http::error::bad_request("missing request body")),
        };

        let cert_id = format!(
            "aziot-edged/module/{}:{}:server",
            &self.module_id, &self.gen_id
        );

        // SANs take precedence over CN. The CN must be in the SAN list to be considered.
        let common_name_san = common_name.clone();

        let common_name_san = if std::net::IpAddr::from_str(&common_name_san).is_ok() {
            super::SubjectAltName::Ip(common_name_san)
        } else {
            let common_name_san = super::sanitize_dns_name(common_name_san);

            super::SubjectAltName::Dns(common_name_san)
        };

        // Server certificates have the module ID and certificate CN as the SANs.
        let module_id_san = super::sanitize_dns_name(self.module_id);
        let module_id_san = super::SubjectAltName::Dns(module_id_san);

        let subject_alt_names = vec![common_name_san, module_id_san];

        let csr_extensions = server_cert_extensions().map_err(|_| {
            edgelet_http::error::server_error("failed to set server csr extensions")
        })?;

        self.api
            .issue_cert(cert_id, common_name, subject_alt_names, csr_extensions)
            .await
    }

    type PutBody = serde::de::IgnoredAny;
    type PutResponse = ();
}

fn server_cert_extensions(
) -> Result<openssl::stack::Stack<openssl::x509::X509Extension>, openssl::error::ErrorStack> {
    let mut csr_extensions = openssl::stack::Stack::new()?;

    let mut ext_key_usage = openssl::x509::extension::ExtendedKeyUsage::new();
    ext_key_usage.server_auth();

    let ext_key_usage = ext_key_usage.build()?;
    csr_extensions.push(ext_key_usage)?;

    Ok(csr_extensions)
}
