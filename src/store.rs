use crate::config::Configuration;
use crate::constants::*;
use crate::util::*;

use std::sync::Arc;

use aziot_key_client_async::Client as KeyClient;
use aziot_key_common::{CreateKeyValue, EncryptMechanism};
use futures::future::try_join_all;
use hyper::client::HttpConnector;
use iotedge_aad::{Auth, TokenSource};
use lazy_static::lazy_static;
use regex::Regex;
use ring::rand::{generate, SystemRandom};
use serde::{Deserialize, Serialize};
use zeroize::Zeroize;

lazy_static! {
    static ref KEY_CLIENT: KeyClient<HttpConnector> = KeyClient::new(HttpConnector::new());
}

#[derive(Deserialize, Serialize, Zeroize)]
pub struct Record {
    pub ciphertext: Vec<u8>,
    pub iv: Vec<u8>,
    pub aad: Vec<u8>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub upstream: Option<String>
}

// NOTE: open to changing implementation so that Sync is not required
pub trait StoreBackend: Sized + Sync {
    type Error: std::error::Error;

    fn new() -> Result<Self, Self::Error>;

    fn write_record(&self, id: &str, record: Record) -> Result<(), Self::Error>;
    fn update_record(&self, id: &str, record: Record) -> Result<(), Self::Error>;
    fn read_record(&self, id: &str) -> Result<Record, Self::Error>;
    fn delete_record(&self, id: &str) -> Result<(), Self::Error>;
}

// NOTE: not fully public since high-level functions should be
//       invariant over backend implementation
pub(crate) struct Store<T: StoreBackend> {
    backend: T,
    config: Configuration
}

impl<T: StoreBackend> Store<T> {
    pub fn new(backend: T, config: Configuration) -> Self {
        Self {
            backend: backend,
            config: config
        }
    }

    pub async fn get_secret(&self, id: String) -> BoxResult<'_, String> {
        let record = self.backend.read_record(&id)?;
        let key_handle = KEY_CLIENT.create_key_if_not_exists(
                &id,
                CreateKeyValue::Generate { length: AES_KEY_BYTES }
            )
            .await?;
        let pbytes = KEY_CLIENT.decrypt(
                &key_handle,
                EncryptMechanism::Aead {
                    iv: record.iv,
                    aad: record.aad
                },
                record.ciphertext.as_slice()
            )
            .await?;

        let ptext = String::from_utf8(pbytes)?;

        Ok(ptext)
    }

    pub async fn set_secret(&self, id: String, value: String) -> BoxResult<'_, ()> {
        let rng = SystemRandom::new();

        let key_handle = KEY_CLIENT.create_key_if_not_exists(
                &id,
                CreateKeyValue::Generate { length: AES_KEY_BYTES }
            )
            .await?;
        let iv = generate::<[u8; IV_BYTES]>(&rng)?.expose().to_vec();
        let aad = generate::<[u8; AAD_BYTES]>(&rng)?.expose().to_vec();

        let ctext = KEY_CLIENT.encrypt(
                &key_handle,
                EncryptMechanism::Aead {
                    iv: iv.to_vec(),
                    aad: aad.to_vec()
                },
                value.as_bytes()
            )
            .await?;

        self.backend.write_record(&id, Record {
                ciphertext: ctext,
                iv: iv,
                aad: aad,
                upstream: None
            })?;

        Ok(())
    }

    pub async fn pull_secrets(&self, keys: Vec<&str>) -> BoxResult<'_, ()> {
        lazy_static! {
            static ref VAULT_REGEX: Regex = Regex::new(r"^https://[0-9a-zA-Z\-]+\.vault\.azure\.net").unwrap();
        }
        let client = hyper::Client::builder()
            .build(hyper_tls::HttpsConnector::new());
        let token = Auth::new(Arc::new(reqwest::Client::new()), "https://vault.azure.net")
            .authorize_with_secret(
                &self.config.credentials.tenant_id,
                &self.config.credentials.client_id,
                &self.config.credentials.client_secret
            )
            .await?
            .get()
            .to_string();

        let key_values: Vec<hyper::Response<hyper::Body>> = try_join_all(
                keys.into_iter()
                    .filter(|key| VAULT_REGEX.is_match(key))
                    .map(|key| format!("{}?api-version=7.0", key).parse::<hyper::Uri>())
                    .collect::<Result<Vec<hyper::Uri>, _>>()?
                    .into_iter()
                    .map(|key| {
                        let req = hyper::Request::builder()
                            .uri(key)
                            .header("Authorization", format!("Bearer {}", token.to_owned()))
                            .body(hyper::Body::empty())
                            .unwrap();
                        client.request(req)
                    })
                    .collect::<Vec<hyper::client::ResponseFuture>>()
            )
            .await?;

        for key_value in try_join_all(key_values.into_iter().map(|key_value| slurp_json::<serde_json::Value>(key_value))).await? {
            println!("{}", key_value.get("value").unwrap().as_str().unwrap())
        }

        Ok(())
    }
}
