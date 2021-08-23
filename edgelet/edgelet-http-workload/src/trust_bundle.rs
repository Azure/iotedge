// Copyright (c) Microsoft. All rights reserved.

#[cfg(not(test))]
use aziot_cert_client_async::Client as CertClient;

#[cfg(test)]
use edgelet_test_utils::clients::CertClient;

pub(crate) struct Route<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
    trust_bundle: String,
    optional: bool,
    _runtime: std::marker::PhantomData<M>,
}

#[derive(Debug, serde::Serialize)]
#[cfg_attr(test, derive(serde::Deserialize))]
pub(crate) struct TrustBundleResponse {
    certificate: String,
}

const TRUST_BUNDLE_PATH: &str = "/trust-bundle";
const MANIFEST_TRUST_BUNDLE_PATH: &str = "/manifest-trust-bundle";

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
        _extensions: &http::Extensions,
    ) -> Option<Self> {
        // The default trust bundle is required, but the manifest trust bundle is optional.
        let (trust_bundle, optional) = match path {
            TRUST_BUNDLE_PATH => (service.config.trust_bundle.clone(), false),
            MANIFEST_TRUST_BUNDLE_PATH => (service.config.manifest_trust_bundle.clone(), true),
            _ => return None,
        };

        Some(Route {
            client: service.cert_client.clone(),
            trust_bundle,
            optional,
            _runtime: std::marker::PhantomData,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    async fn get(self) -> http_common::server::RouteResponse {
        let client = self.client.lock().await;

        let certificate = client.get_cert(&self.trust_bundle).await.map_err(|_| {
            edgelet_http::error::not_found(format!("certificate {} not found", self.trust_bundle))
        });

        let certificate = match (certificate, self.optional) {
            (Ok(certificate), _) => std::str::from_utf8(&certificate)
                .map_err(|err| {
                    edgelet_http::error::server_error(format!(
                        "could not parse certificate: {}",
                        err
                    ))
                })?
                .to_string(),

            (Err(_), true) => String::new(),

            (Err(err), false) => {
                return Err(err);
            }
        };

        let res = TrustBundleResponse { certificate };
        let res = http_common::server::response::json(hyper::StatusCode::OK, &res);

        Ok(res)
    }

    type PostBody = serde::de::IgnoredAny;

    type PutBody = serde::de::IgnoredAny;
}

#[cfg(test)]
mod tests {
    use http_common::server::Route;

    use edgelet_test_utils::{test_route_err, test_route_ok};

    #[test]
    fn parse_uri() {
        // Valid URIs
        let route = test_route_ok!(super::TRUST_BUNDLE_PATH);
        assert_eq!("test-trust-bundle", route.trust_bundle);
        assert_eq!(false, route.optional);

        let route = test_route_ok!(super::MANIFEST_TRUST_BUNDLE_PATH);
        assert_eq!("test-manifest-trust-bundle", route.trust_bundle);
        assert_eq!(true, route.optional);

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", super::TRUST_BUNDLE_PATH));
        test_route_err!(&format!("a{}", super::MANIFEST_TRUST_BUNDLE_PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}a", super::TRUST_BUNDLE_PATH));
        test_route_err!(&format!("a{}", super::MANIFEST_TRUST_BUNDLE_PATH));
    }

    #[tokio::test]
    async fn select_trust_bundle() {
        let mut certs = std::collections::BTreeMap::new();
        certs.insert(
            "test-trust-bundle".to_string(),
            "TRUST_BUNDLE".as_bytes().to_owned(),
        );
        certs.insert(
            "test-manifest-trust-bundle".to_string(),
            "MANIFEST_TRUST_BUNDLE".as_bytes().to_owned(),
        );

        // Check that path /trust-bundle selects the default trust bundle,
        // and path /manifest-trust-bundle selects the manifest trust bundle.
        let paths = vec![
            (super::TRUST_BUNDLE_PATH, "TRUST_BUNDLE"),
            (super::MANIFEST_TRUST_BUNDLE_PATH, "MANIFEST_TRUST_BUNDLE"),
        ];

        for (path, expected) in paths {
            let route = test_route_ok!(path);

            {
                let mut client = route.client.lock().await;
                client.certs =
                    futures_util::lock::Mutex::new(std::cell::RefCell::new(certs.clone()));
            }

            let response = route.get().await.unwrap();
            let body = hyper::body::to_bytes(response.into_body()).await.unwrap();
            let trust_bundle: super::TrustBundleResponse = serde_json::from_slice(&body).unwrap();

            assert_eq!(expected, trust_bundle.certificate);
        }
    }

    #[tokio::test]
    async fn optional_trust_bundle() {
        // Required trust bundle: fail if cert doesn't exist.
        let route = test_route_ok!(super::TRUST_BUNDLE_PATH);
        route.get().await.unwrap_err();

        // Optional trust bundle: return empty string if cert doesn't exist.
        let route = test_route_ok!(super::MANIFEST_TRUST_BUNDLE_PATH);
        let response = route.get().await.unwrap();
        let body = hyper::body::to_bytes(response.into_body()).await.unwrap();
        let trust_bundle: super::TrustBundleResponse = serde_json::from_slice(&body).unwrap();
        assert_eq!(String::new(), trust_bundle.certificate);
    }
}
