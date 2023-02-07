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

pub(crate) struct EdgeCaRenewal {
    rotate_key: bool,
    temp_cert: String,
    cert_client: std::sync::Arc<tokio::sync::Mutex<CertClient>>,
    key_client: std::sync::Arc<tokio::sync::Mutex<KeyClient>>,
    key_connector: http_common::Connector,
    renewal_tx: tokio::sync::mpsc::UnboundedSender<edgelet_core::WatchdogAction>,
}

impl EdgeCaRenewal {
    pub fn new(
        rotate_key: bool,
        config: &crate::WorkloadConfig,
        cert_client: std::sync::Arc<tokio::sync::Mutex<CertClient>>,
        key_client: std::sync::Arc<tokio::sync::Mutex<KeyClient>>,
        key_connector: http_common::Connector,
        renewal_tx: tokio::sync::mpsc::UnboundedSender<edgelet_core::WatchdogAction>,
    ) -> Self {
        let temp_cert = format!("{}-temp", config.edge_ca_cert);

        EdgeCaRenewal {
            rotate_key,
            temp_cert,
            cert_client,
            key_client,
            key_connector,
            renewal_tx,
        }
    }
}

#[async_trait::async_trait]
impl cert_renewal::CertInterface for EdgeCaRenewal {
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

        // Generate a CSR and issue the new cert under a temporary cert ID.
        let extensions = extensions().map_err(|_| {
            cert_renewal::Error::fatal_error("failed to generate edge CA extensions")
        })?;

        // The new cert will have the same subject as the old cert.
        let csr = crate::module::cert::new_csr(
            old_cert_chain[0].subject_name(),
            keys,
            Vec::new(),
            extensions,
        )
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
        if old_key != new_key {
            let res = {
                let key_client = self.key_client.lock().await;

                key_client.move_key_pair(&new_key, old_key).await
            };

            if res.is_err() {
                // Revert to the previous cert if the key could not be written.
                let cert_client = self.cert_client.lock().await;

                cert_client
                    .import_cert(cert_id, &old_cert_chain_pem)
                    .await
                    .map_err(|_| {
                        cert_renewal::Error::retryable_error("failed to restore old cert")
                    })?;
            }
        }

        log::info!("Edge CA was renewed");

        // Modules should be restarted so that they request new server certs.
        if let Err(err) = self
            .renewal_tx
            .send(edgelet_core::WatchdogAction::EdgeCaRenewal)
        {
            log::warn!("Failed to request module restart: {}", err);
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

#[cfg(test)]
mod tests {
    use super::EdgeCaRenewal;
    use super::{CertClient, KeyClient};

    use cert_renewal::CertInterface;

    fn new_renewal(rotate_key: bool) -> EdgeCaRenewal {
        let settings = edgelet_test_utils::Settings::default();

        let device_info = aziot_identity_common::AzureIoTSpec {
            hub_name: "test-hub.test.net".to_string(),
            gateway_host: "gateway-host.test.net".to_string(),
            device_id: aziot_identity_common::DeviceId("test-device".to_string()),
            module_id: None,
            gen_id: None,
            auth: None,
        };

        let config = crate::WorkloadConfig::new(&settings, &device_info);

        let cert_client = CertClient::default();
        let cert_client = std::sync::Arc::new(tokio::sync::Mutex::new(cert_client));

        let key_client = KeyClient::default();
        let key_client = std::sync::Arc::new(tokio::sync::Mutex::new(key_client));

        // Tests won't actually connect to keyd, so just put any URL in the key connector.
        let key_connector = url::Url::parse("unix:///tmp/test.sock").unwrap();
        let key_connector = http_common::Connector::new(&key_connector).unwrap();

        // We won't use the renewal sender, but it must be created to construct the
        // EdgeCaRenewal struct. Note that we drop the renewal receiver, which will cause
        // tests to panic if they use the renewal sender.
        let (renewal_tx, _) =
            tokio::sync::mpsc::unbounded_channel::<edgelet_core::WatchdogAction>();

        EdgeCaRenewal::new(
            rotate_key,
            &config,
            cert_client,
            key_client,
            key_connector,
            renewal_tx,
        )
    }

    #[tokio::test]
    async fn get_cert() {
        let mut renewal = new_renewal(true);

        // Generate a set of certs to use as a cert chain. It's not a valid chain, but that
        // doesn't matter for this test.
        let (cert_1, _) = test_common::credential::test_certificate("test-cert-1");
        let (cert_2, _) = test_common::credential::test_certificate("test-cert-2");

        let mut cert_1_pem = cert_1.to_pem().unwrap();
        let mut cert_2_pem = cert_2.to_pem().unwrap();
        cert_1_pem.append(&mut cert_2_pem);

        let test_cert_chain = vec![cert_1, cert_2];

        {
            let cert_client = renewal.cert_client.lock().await;

            cert_client.import_cert("empty-cert", &[]).await.unwrap();
            cert_client
                .import_cert("test-cert", &cert_1_pem)
                .await
                .unwrap();
        }

        renewal.get_cert("empty-cert").await.unwrap_err();
        renewal.get_cert("does-not-exist").await.unwrap_err();

        let cert_chain = renewal.get_cert("test-cert").await.unwrap();
        assert_eq!(2, cert_chain.len());
        assert_eq!(
            test_cert_chain[0].to_pem().unwrap(),
            cert_chain[0].to_pem().unwrap()
        );
        assert_eq!(
            test_cert_chain[1].to_pem().unwrap(),
            cert_chain[1].to_pem().unwrap()
        );
    }

    #[tokio::test]
    async fn get_key() {
        let mut renewal = new_renewal(true);

        renewal.get_key("test-key").await.unwrap();

        {
            let mut key_client = renewal.key_client.lock().await;

            key_client.load_key_pair_ok = false;
        }

        renewal.get_key("test-key").await.unwrap_err();
    }
}
