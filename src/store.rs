use crate::config::Configuration;
use crate::constants::{AAD_BYTES, IV_BYTES};
use crate::ks;
use crate::ks::{KeyHandle, Text};
use crate::util::BoxFuture;

use base64::encode;
use iotedge_aad::{Auth, TokenSource};
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
// NOTE: could be a struct, if requested
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

    pub fn get_secret<'a>(&'a self, id: String) -> BoxFuture<'a, String> {
        Box::pin(async move {
            let record = self.backend.read_record(&id)?;
            let KeyHandle(key) = ks::get_key(&id).await?;
            let ptext = match ks::decrypt(&key, &record.ciphertext, &record.iv, &record.aad).await? {
                Text::Plaintext(ptext) => ptext,
                _ => panic!("DECRYPTION API CHANGED")
            };

            Ok(ptext)
        })
    }

    pub fn set_secret<'a>(&'a self, id: String, value: String) -> BoxFuture<'a, ()> {
        Box::pin(async move {
            let rng = SystemRandom::new();

            let KeyHandle(key) = ks::create_key(&id).await?;
            let iv: String = encode(generate::<[u8; IV_BYTES]>(&rng)?.expose());
            let aad: String = encode(generate::<[u8; AAD_BYTES]>(&rng)?.expose());

            let ctext = match ks::encrypt(&key, &value, &iv, &aad).await? {
                Text::Ciphertext(ctext) => ctext,
                _ => panic!("ENCRYPTION API CHANGED")
            };

            self.backend.write_record(&id, Record {
                ciphertext: ctext,
                iv: iv,
                aad: aad
            })?;

            Ok(())
        })
    }

    pub fn pull_secrets<'a>(&'a self, keys: &'a [String]) -> BoxFuture<'a, ()> {
        let conf = self.config.to_owned();
        Box::pin(async move {
            let token = Auth::new(None, "https://vault.azure.net")
                .authorize_with_secret(
                        &conf.tenant_id,
                        &conf.client_id,
                        &conf.client_secret
                    )
                .await?
                .get();
            Ok(())
        })
    }
}
