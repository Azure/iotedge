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
    trust_bundle: Vec<String>,
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
        // When getting the default trust bundle, also get any trust bundle provided by DPS.
        let trust_bundle = match path {
            TRUST_BUNDLE_PATH => vec![
                service.config.trust_bundle.clone(),
                service.config.dps_trust_bundle.clone(),
            ],
            MANIFEST_TRUST_BUNDLE_PATH => vec![service.config.manifest_trust_bundle.clone()],
            _ => return None,
        };

        Some(Route {
            client: service.cert_client.clone(),
            trust_bundle,
            _runtime: std::marker::PhantomData,
        })
    }

    type DeleteBody = serde::de::IgnoredAny;

    async fn get(self) -> http_common::server::RouteResponse {
        let cert_response = {
            let client = self.client.lock().await;
            let mut cert_response: Vec<String> = Vec::new();

            for bundle in self.trust_bundle {
                match client.get_cert(&bundle).await {
                    Ok(certs) => {
                        let mut certs = std::str::from_utf8(&certs)
                            .map_err(|err| {
                                edgelet_http::error::server_error(format!(
                                    "could not parse trust bundle {}: {}",
                                    bundle, err
                                ))
                            })?
                            .to_string();

                        let last = certs.chars().last().ok_or_else(|| {
                            edgelet_http::error::server_error(format!(
                                "empty trust bundle {}",
                                bundle
                            ))
                        })?;

                        if last != '\n' {
                            certs.push('\n');
                        }

                        cert_response.push(certs);
                    }
                    Err(err) => {
                        log::warn!("Failed to get trust bundle {}: {}", bundle, err);
                    }
                };
            }

            cert_response
        };

        let res: TrustBundleResponse = cert_response.into();
        let res = http_common::server::response::json(hyper::StatusCode::OK, &res);

        Ok(res)
    }

    type PostBody = serde::de::IgnoredAny;

    type PutBody = serde::de::IgnoredAny;
}

impl std::convert::From<Vec<String>> for TrustBundleResponse {
    fn from(trust_bundle: Vec<String>) -> TrustBundleResponse {
        let mut cert_response = std::collections::HashSet::new();

        for certs in trust_bundle {
            // Parse each trust bundle to drop invalid certificates.
            match openssl::x509::X509::stack_from_pem(certs.as_bytes()) {
                Ok(bundle) => {
                    for cert in bundle {
                        let cert = cert
                            .to_pem()
                            .expect("parsed certificate should convert to pem");
                        let cert = std::str::from_utf8(&cert)
                            .expect("parsed certificate should contain valid utf-8")
                            .to_string();

                        // Remove duplicates by adding each certificate to a hash set.
                        cert_response.insert(cert);
                    }
                }
                Err(err) => {
                    log::warn!("Ignoring invalid trust bundle: {}", err);
                }
            }
        }

        let mut trust_bundle = String::new();
        for cert in cert_response {
            trust_bundle.push_str(&cert);
        }

        TrustBundleResponse {
            certificate: trust_bundle,
        }
    }
}

#[cfg(test)]
mod tests {
    use http_common::server::Route;

    use edgelet_test_utils::{test_route_err, test_route_ok};

    /// Generate a self-signed CA certificate for testing.
    fn new_cert(common_name: &str) -> String {
        let rsa = openssl::rsa::Rsa::generate(2048).unwrap();
        let private_key = openssl::pkey::PKey::from_rsa(rsa).unwrap();

        let public_key = private_key.public_key_to_pem().unwrap();
        let public_key = openssl::pkey::PKey::public_key_from_pem(&public_key).unwrap();

        let mut builder = openssl::x509::X509Builder::new().unwrap();
        builder.set_version(2).unwrap();

        let not_before = openssl::asn1::Asn1Time::days_from_now(0).unwrap();
        let not_after = openssl::asn1::Asn1Time::days_from_now(30).unwrap();
        builder.set_not_before(&not_before).unwrap();
        builder.set_not_after(&not_after).unwrap();

        let mut name = openssl::x509::X509NameBuilder::new().unwrap();
        name.append_entry_by_text("CN", common_name).unwrap();
        let name = name.build();

        builder.set_issuer_name(&name).unwrap();
        builder.set_subject_name(&name).unwrap();

        let mut basic_constraints = openssl::x509::extension::BasicConstraints::new();
        basic_constraints.ca().critical().pathlen(0);
        let basic_constraints = basic_constraints.build().unwrap();
        builder.append_extension(basic_constraints).unwrap();

        builder.set_pubkey(&public_key).unwrap();
        builder
            .sign(&private_key, openssl::hash::MessageDigest::sha256())
            .unwrap();

        let cert = builder.build();
        let cert = cert.to_pem().unwrap();
        let cert = std::str::from_utf8(&cert).unwrap().to_string();

        cert
    }

    /// Check a string of certificates where the order doesn't matter.
    fn check_cert_response(certs_1: String, certs_2: String) {
        let mut certs_1: Vec<&str> = certs_1.split('\n').collect();
        certs_1.sort_unstable();

        let mut certs_2: Vec<&str> = certs_2.split('\n').collect();
        certs_2.sort_unstable();

        assert_eq!(certs_1, certs_2);
    }

    #[test]
    fn parse_uri() {
        // Valid URIs
        let route = test_route_ok!(super::TRUST_BUNDLE_PATH);
        assert_eq!(
            vec![
                "test-trust-bundle".to_string(),
                "test-dps-trust-bundle".to_string(),
            ],
            route.trust_bundle
        );

        let route = test_route_ok!(super::MANIFEST_TRUST_BUNDLE_PATH);
        assert_eq!(
            vec!["test-manifest-trust-bundle".to_string()],
            route.trust_bundle
        );

        // Extra character at beginning of URI
        test_route_err!(&format!("a{}", super::TRUST_BUNDLE_PATH));
        test_route_err!(&format!("a{}", super::MANIFEST_TRUST_BUNDLE_PATH));

        // Extra character at end of URI
        test_route_err!(&format!("{}a", super::TRUST_BUNDLE_PATH));
        test_route_err!(&format!("a{}", super::MANIFEST_TRUST_BUNDLE_PATH));
    }

    #[tokio::test]
    async fn select_trust_bundle() {
        let trust_cert = new_cert("test-trust-cert");
        let dps_trust_cert = new_cert("dps-trust-cert");
        let manifest_trust_cert = new_cert("manifest-trust-cert");

        let mut certs = std::collections::BTreeMap::new();
        certs.insert(
            "test-trust-bundle".to_string(),
            trust_cert.as_bytes().to_owned(),
        );
        certs.insert(
            "test-dps-trust-bundle".to_string(),
            dps_trust_cert.as_bytes().to_owned(),
        );
        certs.insert(
            "test-manifest-trust-bundle".to_string(),
            manifest_trust_cert.as_bytes().to_owned(),
        );

        // The expected default trust bundle should contain two certificates.
        let mut expected_trust_bundle = trust_cert;
        expected_trust_bundle.push_str(&dps_trust_cert);

        // Check that path /trust-bundle selects the default trust bundle,
        // and path /manifest-trust-bundle selects the manifest trust bundle.
        let paths = vec![
            (super::TRUST_BUNDLE_PATH, expected_trust_bundle),
            (super::MANIFEST_TRUST_BUNDLE_PATH, manifest_trust_cert),
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

            check_cert_response(expected, trust_bundle.certificate);
        }
    }

    #[test]
    fn validate_and_dedup() {
        let cert_1 = new_cert("cert_1");
        let cert_2 = new_cert("cert_2");
        let cert_3 = new_cert("cert_3");

        let input = vec![
            cert_1.clone() + &cert_2,
            cert_2.clone() + &cert_1 + &cert_2,
            cert_3.clone() + "invalid cert",
            "invalid cert".to_string(),
        ];

        let expected = cert_1 + &cert_2 + &cert_3;
        let response: super::TrustBundleResponse = input.into();
        check_cert_response(expected, response.certificate);
    }

    #[tokio::test]
    async fn empty_trust_bundle() {
        // Return empty string if cert doesn't exist.
        let route = test_route_ok!(super::MANIFEST_TRUST_BUNDLE_PATH);
        let response = route.get().await.unwrap();
        let body = hyper::body::to_bytes(response.into_body()).await.unwrap();
        let trust_bundle: super::TrustBundleResponse = serde_json::from_slice(&body).unwrap();
        assert_eq!(String::new(), trust_bundle.certificate);
    }
}
