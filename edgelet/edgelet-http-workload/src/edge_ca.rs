// Copyright (c) Microsoft. All rights reserved.

#[cfg(not(test))]
use aziot_cert_client_async::Client as CertClient;
#[cfg(not(test))]
use aziot_key_client_async::Client as KeyClient;
#[cfg(not(test))]
use aziot_key_openssl_engine as KeyEngine;

#[cfg(test)]
use test_common::client::CertClient;
#[cfg(test)]
use test_common::client::KeyClient;
#[cfg(test)]
use test_common::client::KeyEngine;

#[derive(Clone)]
pub(crate) struct EdgeCaRenewal {
    cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
    key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    key_connector: http_common::Connector,
}

impl EdgeCaRenewal {
    pub fn new(
        cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
        key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
        key_connector: http_common::Connector,
    ) -> Self {
        EdgeCaRenewal {
            cert_client,
            key_client,
            key_connector,
        }
    }
}

#[async_trait::async_trait]
impl cert_renewal::CertInterface for EdgeCaRenewal {
    type NewKey = String;

    async fn get_cert(
        &mut self,
        cert_id: &str,
    ) -> Result<openssl::x509::X509, cert_renewal::Error> {
        let cert_client = self.cert_client.lock().await;

        let cert = cert_client
            .get_cert(cert_id)
            .await
            .map_err(|_| cert_renewal::Error::retryable_error("failed to retrieve edge CA cert"))?;

        openssl::x509::X509::from_pem(&cert)
            .map_err(|_| cert_renewal::Error::fatal_error("failed to parse edge CA cert"))
    }

    async fn get_key(
        &mut self,
        key_id: &str,
    ) -> Result<openssl::pkey::PKey<openssl::pkey::Private>, cert_renewal::Error> {
        let key_client = self.key_client.lock().await;

        let key_handle = key_client
            .load_key_pair(key_id)
            .await
            .map_err(|_| cert_renewal::Error::retryable_error("failed to get identity cert key"))?;

        let (private_key, _) = keys(self.key_connector.clone(), &key_handle)
            .map_err(|err| cert_renewal::Error::retryable_error(err))?;

        Ok(private_key)
    }

    async fn renew_cert(
        &mut self,
        old_cert: &openssl::x509::X509,
        key_id: &str,
    ) -> Result<(openssl::x509::X509, Self::NewKey), cert_renewal::Error> {
        todo!()
    }

    async fn write_credentials(
        &mut self,
        old_cert: &openssl::x509::X509,
        new_cert: (&str, &openssl::x509::X509),
        key: (&str, Self::NewKey),
    ) -> Result<(), cert_renewal::Error> {
        todo!()
    }
}

pub(crate) fn keys(
    key_connector: http_common::Connector,
    key_handle: &aziot_key_common::KeyHandle,
) -> Result<
    (
        openssl::pkey::PKey<openssl::pkey::Private>,
        openssl::pkey::PKey<openssl::pkey::Public>,
    ),
    String,
> {
    // The openssl engine must use a sync client. Elsewhere, the async client is used.
    let key_client = aziot_key_client::Client::new(
        aziot_key_common_http::ApiVersion::V2021_05_01,
        key_connector,
    );
    let key_client = std::sync::Arc::new(key_client);
    let key_handle =
        std::ffi::CString::new(key_handle.0.clone()).expect("key handle contained null");

    let mut engine =
        KeyEngine::load(key_client).map_err(|_| "failed to load openssl key engine".to_string())?;

    let private_key = engine
        .load_private_key(&key_handle)
        .map_err(|_| "failed to load edge ca private key".to_string())?;

    let public_key = engine
        .load_public_key(&key_handle)
        .map_err(|_| "failed to load edge ca public key".to_string())?;

    Ok((private_key, public_key))
}

pub(crate) fn extensions(
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
