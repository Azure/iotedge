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

pub(crate) struct EdgeCaRenewal<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    rotate_key: bool,
    temp_cert: String,
    runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
    cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
    key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    key_connector: http_common::Connector,
}

impl<M> EdgeCaRenewal<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    pub fn new(
        rotate_key: bool,
        edge_ca_id: &str,
        runtime: std::sync::Arc<futures_util::lock::Mutex<M>>,
        cert_client: std::sync::Arc<futures_util::lock::Mutex<CertClient>>,
        key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
        key_connector: http_common::Connector,
    ) -> Self {
        let temp_cert = format!("{}-temp", edge_ca_id);

        EdgeCaRenewal {
            rotate_key,
            temp_cert,
            runtime,
            cert_client,
            key_client,
            key_connector,
        }
    }
}

#[async_trait::async_trait]
impl<M> cert_renewal::CertInterface for EdgeCaRenewal<M>
where
    M: edgelet_core::ModuleRuntime + Send + Sync,
{
    type NewKey = String;

    async fn get_cert(
        &mut self,
        cert_id: &str,
    ) -> Result<Vec<openssl::x509::X509>, cert_renewal::Error> {
        let cert_client = self.cert_client.lock().await;

        let cert = cert_client
            .get_cert(cert_id)
            .await
            .map_err(|_| cert_renewal::Error::retryable_error("failed to retrieve edge CA cert"))?;

        let cert_chain = openssl::x509::X509::stack_from_pem(&cert)
            .map_err(|_| cert_renewal::Error::fatal_error("failed to parse edge CA cert"))?;

        if cert_chain.is_empty() {
            Err(cert_renewal::Error::fatal_error("no certs in chain"))
        } else {
            Ok(cert_chain)
        }
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
        old_cert_chain: &[openssl::x509::X509],
        key_id: &str,
    ) -> Result<(Vec<openssl::x509::X509>, Self::NewKey), cert_renewal::Error> {
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
        let subject = if let Some(subject) = old_cert_chain[0]
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

        let new_cert_chain = openssl::x509::X509::stack_from_pem(&new_cert)
            .map_err(|_| cert_renewal::Error::retryable_error("failed to parse new cert"))?;

        if new_cert_chain.is_empty() {
            Err(cert_renewal::Error::retryable_error("no certs in chain"))
        } else {
            Ok((new_cert_chain, key_id))
        }
    }

    async fn write_credentials(
        &mut self,
        old_cert_chain: &[openssl::x509::X509],
        new_cert_chain: (&str, &[openssl::x509::X509]),
        key: (&str, Self::NewKey),
    ) -> Result<(), cert_renewal::Error> {
        let (cert_id, new_cert_chain) = (new_cert_chain.0, new_cert_chain.1);
        let (old_key, new_key) = (key.0, key.1);

        if old_cert_chain.is_empty() || new_cert_chain.is_empty() {
            return Err(cert_renewal::Error::retryable_error("no certs in chain"));
        }

        let mut new_cert_chain_pem = Vec::new();

        for cert in new_cert_chain {
            let mut cert = cert
                .to_pem()
                .map_err(|_| cert_renewal::Error::retryable_error("bad cert"))?;

            new_cert_chain_pem.append(&mut cert);
        }

        let mut old_cert_chain_pem = Vec::new();

        for cert in old_cert_chain {
            let mut cert = cert
                .to_pem()
                .map_err(|_| cert_renewal::Error::retryable_error("bad cert"))?;

            old_cert_chain_pem.append(&mut cert);
        }

        // Commit the new cert to storage.
        {
            let cert_client = self.cert_client.lock().await;

            cert_client
                .import_cert(cert_id, &new_cert_chain_pem)
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
        }

        // Revert to the previous cert if the key could not be written.
        if revert_cert {
            let cert_client = self.cert_client.lock().await;

            cert_client
                .import_cert(cert_id, &old_cert_chain_pem)
                .await
                .map_err(|_| cert_renewal::Error::retryable_error("failed to restore old cert"))?;
        }

        // Modules should be restarted so that they request new server certs. Stop all modules here;
        // the Edge daemon watchdog will restart them.
        let runtime = self.runtime.lock().await;

        if let Err(err) = runtime.stop_all(None).await {
            log::warn!("Failed to restart modules after Edge CA renewal: {}", err);
        } else {
            log::info!("Edge CA renewal stopped all modules");
        }

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
