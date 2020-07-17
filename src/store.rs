use crate::config::Configuration;
use crate::constants::{AAD_BYTES, IV_BYTES};
use crate::ks;
use crate::ks::{Key, Text};
use crate::util::*;

use base64::{decode, encode};
use futures::future::try_join_all;
use iotedge_aad::{Auth, TokenSource};
use lazy_static::lazy_static;
use regex::Regex;
use ring::rand::{generate, SystemRandom};
use serde::{Deserialize, Serialize};
use zeroize::Zeroize;

#[derive(Deserialize, Serialize, Zeroize)]
#[zeroize(drop)]
pub struct Record {
    pub ciphertext: String,
    pub iv: String,
    pub aad: String
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
        let Key::KeyHandle(key) = ks::create_or_get_key(&id).await?;
        let ptext = match ks::decrypt(&key, &record.ciphertext, &record.iv, &record.aad).await? {
            Text::Plaintext(ptext) => String::from_utf8(decode(ptext)?)?,
            _ => panic!("DECRYPTION API CHANGED")
        };

        Ok(ptext)
    }

    pub async fn set_secret(&self, id: String, value: String) -> BoxResult<'_, ()> {
        let rng = SystemRandom::new();

        let Key::KeyHandle(key) = ks::create_or_get_key(&id).await?;
        let ptext = encode(value);
        let iv = encode(generate::<[u8; IV_BYTES]>(&rng)?.expose());
        let aad = encode(generate::<[u8; AAD_BYTES]>(&rng)?.expose());

        let ctext = match ks::encrypt(&key, &ptext, &iv, &aad).await? {
            Text::Ciphertext(ctext) => ctext,
            _ => panic!("ENCRYPTION API CHANGED")
        };

        self.backend.write_record(&id, Record {
            ciphertext: ctext,
            iv: iv,
            aad: aad
        })?;

        Ok(())
    }

    pub async fn pull_secrets(&self, keys: Vec<&str>) -> BoxResult<'_, ()> {
        lazy_static! {
            static ref VAULT_REGEX: Regex = Regex::new(r"^https://[0-9a-zA-Z\-]+\.vault\.azure\.net").unwrap();
        }
        let client = hyper::Client::builder()
            .build(hyper_tls::HttpsConnector::new());
        let token = Auth::new(None, "https://vault.azure.net")
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
