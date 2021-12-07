// Copyright (c) Microsoft. All rights reserved.

pub(crate) mod identity;
pub(crate) mod server;

#[cfg(not(test))]
use aziot_cert_client_async::Client as CertClient;
#[cfg(not(test))]
use aziot_key_client_async::Client as KeyClient;
#[cfg(not(test))]
use aziot_key_openssl_engine as KeyEngine;

#[cfg(test)]
use edgelet_test_utils::clients::CertClient;
#[cfg(test)]
use edgelet_test_utils::clients::KeyClient;
#[cfg(test)]
use edgelet_test_utils::clients::KeyEngine;

#[derive(Debug, serde::Serialize)]
#[cfg_attr(test, derive(serde::Deserialize))]
#[serde(tag = "type")]
pub(crate) enum PrivateKey {
    #[serde(rename = "key")]
    Key { bytes: String },
}

#[derive(Debug, serde::Serialize)]
#[cfg_attr(test, derive(serde::Deserialize))]
pub(crate) struct CertificateResponse {
    #[serde(rename = "privateKey")]
    private_key: PrivateKey,

    certificate: String,
    expiration: String,
}

enum SubjectAltName {
    Dns(String),
    Ip(String),
}

struct CertApi {
    key_connector: http_common::Connector,
    key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,

    device_id: String,
    edge_ca_cert: String,
    edge_ca_key: String,
}

impl CertApi {
    pub fn new(
        key_connector: http_common::Connector,
        key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
        cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
        config: &crate::WorkloadConfig,
    ) -> Self {
        CertApi {
            key_connector,
            key_client,
            cert_client,
            device_id: config.device_id.clone(),
            edge_ca_cert: config.edge_ca_cert.clone(),
            edge_ca_key: config.edge_ca_key.clone(),
        }
    }

    pub async fn issue_cert(
        self,
        cert_id: String,
        common_name: String,
        subject_alt_names: Vec<SubjectAltName>,
        extensions: openssl::stack::Stack<openssl::x509::X509Extension>,
    ) -> Result<hyper::Response<hyper::Body>, http_common::server::Error> {
        let keys = new_keys()
            .map_err(|_| edgelet_http::error::server_error("failed to generate csr keys"))?;
        let private_key = key_to_pem(&keys.0);

        let csr = new_csr(common_name, keys, subject_alt_names, extensions)
            .map_err(|_| edgelet_http::error::server_error("failed to generate csr"))?;

        let edge_ca_key_handle =
            edge_ca_key_handle(self.key_client.clone(), &self.edge_ca_key).await?;

        check_edge_ca(
            self.cert_client.clone(),
            &self.edge_ca_cert,
            &self.device_id,
            &edge_ca_key_handle,
            self.key_connector.clone(),
        )
        .await?;

        let cert = self
            .create_cert(&cert_id, &csr, &edge_ca_key_handle)
            .await?;

        let expiration = get_expiration(&cert)?;

        let response = CertificateResponse {
            private_key: PrivateKey::Key { bytes: private_key },
            certificate: cert,
            expiration,
        };
        let response = http_common::server::response::json(hyper::StatusCode::CREATED, &response);

        Ok(response)
    }

    async fn create_cert(
        &self,
        cert_id: &str,
        csr: &[u8],
        edge_ca_key_handle: &aziot_key_common::KeyHandle,
    ) -> Result<String, http_common::server::Error> {
        let cert = {
            let cert_client = self.cert_client.lock().await;

            cert_client
                .create_cert(cert_id, csr, Some((&self.edge_ca_cert, edge_ca_key_handle)))
                .await
                .map_err(|_| {
                    edgelet_http::error::server_error(format!("failed to create cert {}", cert_id))
                })
        }?;

        let cert = std::str::from_utf8(&cert)
            .map_err(|_| edgelet_http::error::server_error("invalid cert created"))?;

        Ok(cert.to_string())
    }
}

fn new_keys() -> Result<
    (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ),
    openssl::error::ErrorStack,
> {
    let rsa = openssl::rsa::Rsa::generate(2048)?;
    let private_key = openssl::pkey::PKey::from_rsa(rsa)?;

    let public_key = private_key.public_key_to_pem()?;
    let public_key = openssl::pkey::PKey::public_key_from_pem(&public_key)?;

    Ok((private_key, public_key))
}

fn new_csr(
    common_name: String,
    keys: (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ),
    subject_alt_names: Vec<SubjectAltName>,
    mut extensions: openssl::stack::Stack<openssl::x509::X509Extension>,
) -> Result<Vec<u8>, openssl::error::ErrorStack> {
    let private_key = keys.0;
    let public_key = keys.1;

    let mut csr = openssl::x509::X509Req::builder()?;
    csr.set_version(0)?;

    let mut subject_name = openssl::x509::X509Name::builder()?;
    subject_name.append_entry_by_text("CN", &common_name)?;
    let subject_name = subject_name.build();
    csr.set_subject_name(&subject_name)?;

    csr.set_pubkey(&public_key)?;

    if !subject_alt_names.is_empty() {
        let mut names = openssl::x509::extension::SubjectAlternativeName::new();

        for name in subject_alt_names {
            match name {
                SubjectAltName::Dns(name) => names.dns(&name),
                SubjectAltName::Ip(name) => names.ip(&name),
            };
        }

        let names = names.build(&csr.x509v3_context(None))?;
        extensions.push(names)?;
    }

    csr.add_extensions(&extensions)?;

    csr.sign(&private_key, openssl::hash::MessageDigest::sha256())?;

    let csr = csr.build().to_pem()?;

    Ok(csr)
}

fn get_expiration(cert: &str) -> Result<String, http_common::server::Error> {
    let cert = openssl::x509::X509::from_pem(cert.as_bytes())
        .map_err(|_| edgelet_http::error::server_error("failed to parse cert"))?;

    // openssl::asn1::Asn1TimeRef does not expose any way to convert the ASN1_TIME to a Rust-friendly type
    //
    // Its Display impl uses ASN1_TIME_print, so we convert it into a String and parse it back
    // into a chrono::DateTime<chrono::Utc>
    let expiration = cert.not_after().to_string();
    let expiration = chrono::NaiveDateTime::parse_from_str(&expiration, "%b %e %H:%M:%S %Y GMT")
        .expect("cert not_after should parse");
    let expiration = chrono::DateTime::<chrono::Utc>::from_utc(expiration, chrono::Utc);

    Ok(expiration.to_rfc3339())
}

fn key_to_pem(key: &openssl::pkey::PKey<openssl::pkey::Private>) -> String {
    // The key parameter is always generated by this library. It should be valid.
    let key_pem = key.private_key_to_pem_pkcs8().expect("key is invalid");

    let key_pem = std::str::from_utf8(&key_pem)
        .expect("key is invalid")
        .to_string();

    key_pem
}

pub(crate) async fn edge_ca_key_handle(
    key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    edge_ca_key: &str,
) -> Result<aziot_key_common::KeyHandle, http_common::server::Error> {
    let key_client = key_client.lock().await;

    key_client
        .create_key_pair_if_not_exists(edge_ca_key, Some("rsa-2048:*"))
        .await
        .map_err(|_| edgelet_http::error::server_error("failed to retrieve edge ca key"))
}

pub(crate) async fn check_edge_ca(
    cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
    edge_ca_cert: &str,
    device_id: &str,
    key_handle: &aziot_key_common::KeyHandle,
    key_connector: http_common::Connector,
) -> Result<(), http_common::server::Error> {
    let cert_client = cert_client.lock().await;

    log::info!("Checking Edge CA certificate...");

    if should_renew(&cert_client, edge_ca_cert).await? {
        log::info!("Requesting new Edge CA certificate...");

        let common_name = format!("iotedged workload ca {}", device_id);
        let keys = edge_ca_keys(key_connector, key_handle)?;

        let extensions = edge_ca_extensions().map_err(|_| {
            edgelet_http::error::server_error("failed to set edge ca csr extensions")
        })?;

        let csr = new_csr(common_name, keys, Vec::new(), extensions)
            .map_err(|_| edgelet_http::error::server_error("failed to generate edge ca csr"))?;

        cert_client
            .create_cert(edge_ca_cert, &csr, None)
            .await
            .map_err(|_| edgelet_http::error::server_error("failed to create edge ca cert"))?;

        log::info!("Created new Edge CA certificate");
    } else {
        log::info!("Current Edge CA certificate does not need to be renewed");
    }

    Ok(())
}

async fn should_renew(
    cert_client: &CertClient,
    cert_id: &str,
) -> Result<bool, http_common::server::Error> {
    match cert_client.get_cert(cert_id).await {
        Ok(cert) => {
            let cert = openssl::x509::X509::from_pem(&cert)
                .map_err(|_| edgelet_http::error::server_error("failed to parse edge ca cert"))?;

            let current_time =
                openssl::asn1::Asn1Time::days_from_now(0).expect("current time must be valid");

            let diff = current_time.diff(cert.not_after()).map_err(|_| {
                edgelet_http::error::server_error("failed to determine edge ca expiration time")
            })?;
            let diff = i64::from(diff.secs) + i64::from(diff.days) * 86400;

            // Renew certificate if it expires in the next 5 minutes.
            Ok(diff < 300)
        }
        Err(_) => Ok(true),
    }
}

fn edge_ca_keys(
    key_connector: http_common::Connector,
    key_handle: &aziot_key_common::KeyHandle,
) -> Result<
    (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ),
    http_common::server::Error,
> {
    // The openssl engine must use a sync client. Elsewhere, the async client is used.
    let key_client = aziot_key_client::Client::new(
        aziot_key_common_http::ApiVersion::V2020_09_01,
        key_connector,
    );
    let key_client = std::sync::Arc::new(key_client);
    let key_handle =
        std::ffi::CString::new(key_handle.0.clone()).expect("key handle contained null");

    let mut engine = KeyEngine::load(key_client)
        .map_err(|_| edgelet_http::error::server_error("failed to load openssl key engine"))?;

    let private_key = engine
        .load_private_key(&key_handle)
        .map_err(|_| edgelet_http::error::server_error("failed to load edge ca private key"))?;

    let public_key = engine
        .load_public_key(&key_handle)
        .map_err(|_| edgelet_http::error::server_error("failed to load edge ca public key"))?;

    Ok((private_key, public_key))
}

fn edge_ca_extensions(
) -> Result<openssl::stack::Stack<openssl::x509::X509Extension>, openssl::error::ErrorStack> {
    let mut csr_extensions = openssl::stack::Stack::new()?;

    let mut key_usage = openssl::x509::extension::KeyUsage::new();
    key_usage.critical().digital_signature().key_cert_sign();

    let mut basic_constraints = openssl::x509::extension::BasicConstraints::new();
    basic_constraints.ca().critical().pathlen(0);

    let key_usage = key_usage.build()?;
    let basic_constraints = basic_constraints.build()?;

    csr_extensions.push(key_usage)?;
    csr_extensions.push(basic_constraints)?;

    Ok(csr_extensions)
}

#[cfg(test)]
mod tests {
    fn test_api() -> super::CertApi {
        // Tests won't actually connect to keyd, so just put any URL in the key connector.
        let key_connector = url::Url::parse("unix:///tmp/test.sock").unwrap();
        let key_connector = http_common::Connector::new(&key_connector).unwrap();

        let key_client = edgelet_test_utils::clients::KeyClient::default();
        let key_client = std::sync::Arc::new(futures_util::lock::Mutex::new(key_client));

        let cert_client = edgelet_test_utils::clients::CertClient::default();
        let cert_client = std::sync::Arc::new(futures_util::lock::Mutex::new(cert_client));

        super::CertApi {
            key_connector,
            key_client,
            cert_client,

            device_id: "test-device".to_string(),
            edge_ca_cert: "test-device-cert".to_string(),
            edge_ca_key: "test-device-key".to_string(),
        }
    }

    #[tokio::test]
    async fn edge_ca_renew() {
        // Test data. Tuples contain a test certificate and whether the test should expect
        // the cert to be renewed.
        let test_certs = vec![
            // Expired certificate.
            (
                edgelet_test_utils::test_certificate(
                    "testCertificate",
                    Some(|cert| {
                        let expired = openssl::asn1::Asn1Time::from_unix(1).unwrap();
                        cert.set_not_after(&expired).unwrap();
                    }),
                ),
                true,
            ),
            // Certificate that is near expiry.
            (
                edgelet_test_utils::test_certificate(
                    "testCertificate",
                    Some(|cert| {
                        let now = std::time::SystemTime::now()
                            .duration_since(std::time::UNIX_EPOCH)
                            .unwrap();

                        // Certs within 5 mins of expiration should renew, so set an expiration
                        // time 2 mins from now.
                        let expiry_time = now.as_secs() + 120;
                        let expiry_time: libc::time_t =
                            std::convert::TryInto::try_into(expiry_time).unwrap();

                        let expiry_time = openssl::asn1::Asn1Time::from_unix(expiry_time).unwrap();
                        cert.set_not_after(&expiry_time).unwrap();
                    }),
                ),
                true,
            ),
            // Certificate that is not near expiry.
            (
                edgelet_test_utils::test_certificate("testCertificate", None),
                false,
            ),
        ];

        for ((cert, _), should_renew) in test_certs {
            let api = test_api();
            let cert = cert.to_pem().unwrap();

            // Place test certificate in cert client.
            {
                let cert_client = api.cert_client.lock().await;
                cert_client
                    .create_cert(&api.edge_ca_cert, &cert, None)
                    .await
                    .unwrap();
            }

            let key_handle = aziot_key_common::KeyHandle(api.edge_ca_key.clone());
            super::check_edge_ca(
                api.cert_client.clone(),
                &api.edge_ca_cert,
                &api.device_id,
                &key_handle,
                api.key_connector.clone(),
            )
            .await
            .unwrap();

            let new_cert = {
                let cert_client = api.cert_client.lock().await;
                cert_client.get_cert(&api.edge_ca_cert).await.unwrap()
            };

            // If the cert should have been renewed, check that the new cert is different.
            // If the cert shouldn't have been renewed, check that it is unchanged.
            if should_renew {
                assert_ne!(new_cert, cert);
            } else {
                assert_eq!(new_cert, cert);
            }
        }
    }

    #[tokio::test]
    async fn issue_cert() {
        let api = test_api();

        let (_, issuer_key) = {
            let cert_client = api.cert_client.lock().await;
            (cert_client.issuer.clone(), cert_client.issuer_key.clone())
        };

        // It doesn't matter what extensions we use for this test, so just use the edge CA extensions.
        let extensions = super::edge_ca_extensions().unwrap();

        let response = api
            .issue_cert(
                "testCertificate".to_string(),
                "testCertificate".to_string(),
                // This test won't check these fields, so it doesn't matter what's passed here.
                vec![],
                extensions,
            )
            .await
            .unwrap();

        // Parse response
        let body = hyper::body::to_bytes(response.into_body()).await.unwrap();
        let response: super::CertificateResponse = serde_json::from_slice(&body).unwrap();

        let cert = openssl::x509::X509::from_pem(response.certificate.as_bytes()).unwrap();
        let private_key = match response.private_key {
            super::PrivateKey::Key { bytes } => {
                openssl::pkey::PKey::private_key_from_pem(bytes.as_bytes()).unwrap()
            }
        };
        let expiration = chrono::DateTime::parse_from_rfc3339(&response.expiration).unwrap();

        // Check that expiration is in the future.
        assert!(expiration > chrono::offset::Utc::now());

        // Check that private key in response matches certificate.
        let public_key = private_key.public_key_to_pem().unwrap();
        let cert_public_key = cert.public_key().unwrap().public_key_to_pem().unwrap();
        assert_eq!(cert_public_key, public_key);

        // Check certificate is signed by issuer key.
        assert!(cert.verify(&issuer_key).unwrap());
    }
}
