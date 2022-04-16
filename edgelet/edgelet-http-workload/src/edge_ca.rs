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
    rotate_key: bool,
    temp_cert: String,
    cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
    key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    key_connector: http_common::Connector,
}

impl EdgeCaRenewal {
    pub fn new(
        rotate_key: bool,
        edge_ca_id: &str,
        cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
        key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
        key_connector: http_common::Connector,
    ) -> Self {
        let temp_cert = format!("{}-temp", edge_ca_id);

        EdgeCaRenewal {
            rotate_key,
            temp_cert,
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
            .map_err(cert_renewal::Error::retryable_error)?;

        Ok(private_key)
    }

    async fn renew_cert(
        &mut self,
        old_cert: &openssl::x509::X509,
        key_id: &str,
    ) -> Result<(openssl::x509::X509, Self::NewKey), cert_renewal::Error> {
        // Generate a new key if needed. Otherwise, retrieve the existing key.
        let (key_id, key_handle) = {
            let key_client = self.key_client.lock().await;

            if self.rotate_key {
                let key_id = format!("{}-temp", key_id);

                if let Ok(key_handle) = key_client.load_key_pair(&key_id).await {
                    key_client.delete_key_pair(&key_handle).await.map_err(|_| {
                        cert_renewal::Error::retryable_error("failed to clear temp key")
                    })?;
                }

                let key_handle = key_client
                    .create_key_pair_if_not_exists(&key_id, Some("rsa-2048:*"))
                    .await
                    .map_err(|_| {
                        cert_renewal::Error::retryable_error("failed to generate temp key")
                    })?;

                (key_id, key_handle)
            } else {
                let key_handle = key_client.load_key_pair(key_id).await.map_err(|_| {
                    cert_renewal::Error::retryable_error("failed to get identity cert key")
                })?;

                (key_id.to_string(), key_handle)
            }
        };

        let keys = keys(self.key_connector.clone(), &key_handle)
            .map_err(cert_renewal::Error::retryable_error)?;

        // Determine the subject of the old cert. This will be the subject of the new cert.
        let subject = if let Some(subject) = old_cert
            .subject_name()
            .entries_by_nid(openssl::nid::Nid::COMMONNAME)
            .next()
        {
            String::from_utf8(subject.data().as_slice().into())
                .map_err(|_| cert_renewal::Error::fatal_error("bad cert subject"))?
        } else {
            return Err(cert_renewal::Error::fatal_error(
                "cannot determine subject for csr",
            ));
        };

        // Generate a CSR and issue the new cert under a temporary cert ID.
        let extensions = extensions().map_err(|_| {
            cert_renewal::Error::fatal_error("failed to generate edge CA extensions")
        })?;

        let csr = crate::module::cert::new_csr(subject, keys, Vec::new(), extensions)
            .map_err(|_| cert_renewal::Error::retryable_error("failed to create csr"))?;

        let new_cert = {
            let cert_client = self.cert_client.lock().await;

            let new_cert = cert_client
                .create_cert(&self.temp_cert, &csr, None)
                .await
                .map_err(|_| cert_renewal::Error::retryable_error("failed to create new cert"))?;

            // The temporary ID does not need to be persisted, so delete it after the cert is
            // succesfully created.
            if let Err(err) = cert_client.delete_cert(&self.temp_cert).await {
                log::warn!(
                    "Failed to delete temporary certificate created by cert renewal: {}",
                    err
                );
            }

            new_cert
        };

        let new_cert = openssl::x509::X509::from_pem(&new_cert)
            .map_err(|_| cert_renewal::Error::retryable_error("failed to parse new cert"))?;

        Ok((new_cert, key_id))
    }

    async fn write_credentials(
        &mut self,
        old_cert: &openssl::x509::X509,
        new_cert: (&str, &openssl::x509::X509),
        key: (&str, Self::NewKey),
    ) -> Result<(), cert_renewal::Error> {
        let (cert_id, new_cert) = (new_cert.0, new_cert.1);
        let (old_key, new_key) = (key.0, key.1);

        let new_cert_pem = new_cert
            .to_pem()
            .map_err(|_| cert_renewal::Error::retryable_error("bad cert"))?;

        let old_cert_pem = old_cert
            .to_pem()
            .map_err(|_| cert_renewal::Error::retryable_error("bad cert"))?;

        // Commit the new cert to storage.
        {
            let cert_client = self.cert_client.lock().await;

            cert_client
                .import_cert(cert_id, &new_cert_pem)
                .await
                .map_err(|_| cert_renewal::Error::retryable_error("failed to import new cert"))?;
        }

        // Commit the new key to storage if the key was rotated.
        let mut revert_cert = false;

        if old_key != new_key {
            let key_client = self.key_client.lock().await;

            if key_client.move_key_pair(&new_key, old_key).await.is_err() {
                revert_cert = true;
            }
        };

        // Revert to the previous cert if the key could not be written.
        if revert_cert {
            let cert_client = self.cert_client.lock().await;

            cert_client
                .import_cert(cert_id, &old_cert_pem)
                .await
                .map_err(|_| cert_renewal::Error::retryable_error("failed to import new cert"))?;
        }

        // Restart all modules. Modules should request new server certs based on the new
        // Edge CA on restart.

        Ok(())
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
