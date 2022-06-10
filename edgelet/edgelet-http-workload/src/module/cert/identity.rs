// Copyright (c) Microsoft. All rights reserved.

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    module_id: String,
    module_uri: String,
    pid: libc::pid_t,
    api: super::CertApi,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
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
        let uri_regex = regex::Regex::new("^/modules/(?P<moduleId>[^/]+)/certificate/identity$")
            .expect("hard-coded regex must compile");
        let captures = uri_regex.captures(path)?;

        let module_id = &captures["moduleId"];
        let module_id = percent_encoding::percent_decode_str(module_id)
            .decode_utf8()
            .ok()?;

        let module_uri = format!(
            "URI: azureiot://{}/devices/{}/modules/{}",
            service.config.hub_name, service.config.device_id, module_id
        );

        let pid = match extensions.get::<Option<libc::pid_t>>().copied().flatten() {
            Some(pid) => pid,
            None => return None,
        };

        let api = super::CertApi::new(
            service.key_client.clone(),
            service.cert_client.clone(),
            &service.config,
        );

        Some(Route {
            module_id: module_id.into_owned(),
            module_uri,
            pid,
            api,
            runtime: service.runtime.clone(),
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    type PostBody = serde::de::IgnoredAny;
    async fn post(self, _body: Option<Self::PostBody>) -> http_common::server::RouteResponse {
        edgelet_http::auth_caller(&self.module_id, self.pid, &self.runtime).await?;

        let cert_id = format!("aziot-edged/module/{}:identity", &self.module_id);

        let subject_alt_names = vec![super::SubjectAltName::Dns(self.module_uri)];

        let csr_extensions = identity_cert_extensions().map_err(|_| {
            edgelet_http::error::server_error("failed to set identity csr extensions")
        })?;

        self.api
            .issue_cert(cert_id, self.module_id, subject_alt_names, csr_extensions)
            .await
    }

    type PutBody = serde::de::IgnoredAny;
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

#[cfg(test)]
mod tests {
    use http_common::server::Route;

    use edgelet_test_utils::{test_route_err, test_route_ok};

    const TEST_PATH: &str = "/modules/testModule/certificate/identity";

    #[test]
    fn parse_uri() {
        // Valid URI
        let route = test_route_ok!(TEST_PATH);
        assert_eq!("testModule", &route.module_id);
        assert_eq!(
            "URI: azureiot://test-hub.test.net/devices/test-device/modules/testModule",
            &route.module_uri
        );
        assert_eq!(nix::unistd::getpid().as_raw(), route.pid);

        // Missing module ID
        test_route_err!("/modules//certificate/identity");

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", TEST_PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}a", TEST_PATH));
    }

    #[tokio::test]
    async fn auth() {
        async fn post(
            route: super::Route<edgelet_test_utils::runtime::Runtime>,
        ) -> http_common::server::RouteResponse {
            route.post(None).await
        }

        edgelet_test_utils::test_auth_caller!(TEST_PATH, "testModule", post);
    }
}
