// Copyright (c) Microsoft. All rights reserved.

use std::str::FromStr;

use aziot_identity_common_http::get_provisioning_info::Response as ProvisioningInfo;

#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;

#[cfg(test)]
use edgelet_test_utils::clients::IdentityClient;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    module_id: String,
    gen_id: String,
    identity_client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,
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
            identity_client: service.identity_client.clone(),
            pid,
            api,
            runtime: service.runtime.clone(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    type PostBody = ServerCertificateRequest;
    async fn post(self, body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        edgelet_http::auth_caller(&self.module_id, self.pid, &self.runtime).await?;

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
            super::SubjectAltName::Dns(common_name_san)
        };

        // Server certificates have the module ID and certificate CN as the SANs.
        let module_id_san = super::SubjectAltName::Dns(self.module_id);

        let subject_alt_names = vec![common_name_san, module_id_san];

        // Check if a policy for issuing server certificates is available in Identity Service.
        let identity_client = self.identity_client.lock().await;

        if let Ok(provisioning_info) = identity_client.get_provisioning_info().await {
            if let ProvisioningInfo::Dps { cert_policy, .. } = provisioning_info {

            }
        }

        let csr_extensions = server_cert_extensions().map_err(|_| {
            edgelet_http::error::server_error("failed to set server csr extensions")
        })?;

        self.api
            .issue_cert(cert_id, common_name, subject_alt_names, csr_extensions)
            .await
    }

    type PutBody = serde::de::IgnoredAny;
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

#[cfg(test)]
mod tests {
    use crate::module::cert::CertificateResponse;
    use http_common::server::Route;

    use edgelet_test_utils::{test_route_err, test_route_ok};

    const TEST_PATH: &str = "/modules/testModule/genid/1/certificate/server";

    const MODULE_NAME: &str = "testModule";

    async fn post(
        route: super::Route<edgelet_test_utils::runtime::Runtime>,
    ) -> http_common::server::RouteResponse {
        let body = super::ServerCertificateRequest {
            common_name: MODULE_NAME.to_string(),
        };

        route.post(Some(body)).await
    }

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!(TEST_PATH);
        assert_eq!(MODULE_NAME, &route.module_id);
        assert_eq!("1", &route.gen_id);
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);

        // Missing module ID
        test_route_err!("/modules//genid/1/certificate/server");

        // Missing generation ID
        test_route_err!("/modules/testModule/genid//certificate/server");

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", TEST_PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}a", TEST_PATH));
    }

    #[tokio::test]
    async fn auth() {
        edgelet_test_utils::test_auth_caller!(TEST_PATH, MODULE_NAME, post);
    }

    #[tokio::test]
    async fn verifysans() {
        let route = edgelet_test_utils::test_route_ok!(TEST_PATH);
        {
            let pid = nix::unistd::getpid().as_raw();
            let mut runtime = route.runtime.lock().await;
            runtime.module_auth = std::collections::BTreeMap::new();
            runtime
                .module_auth
                .insert(MODULE_NAME.to_string(), vec![pid]);
        }

        let response = post(route).await.unwrap();
        let body_bytes = hyper::body::to_bytes(response.into_body()).await.unwrap();
        let cert_response = serde_json::from_str::<CertificateResponse>(
            &String::from_utf8(body_bytes.to_vec()).unwrap(),
        )
        .unwrap()
        .certificate;

        let cert = openssl::x509::X509::from_pem(cert_response.as_bytes())
            .map_err(|_| edgelet_http::error::server_error("failed to parse cert"));
        let sans = cert.unwrap().subject_alt_names();
        for san in sans.unwrap().iter() {
            let name = san.dnsname().unwrap();
            assert_eq!(MODULE_NAME.to_lowercase(), name.to_lowercase());
        }
    }
}
