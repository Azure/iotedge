use crate::constants::{AAD_BYTES, IV_BYTES};
use crate::ks;
use crate::ks::{KeyHandle, Text};
use crate::util::BoxFuture;

use base64::encode;
use ring::rand::{generate, SystemRandom};
use serde::{Deserialize, Serialize};
use zeroize::Zeroize;

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
pub(crate) trait Store: StoreBackend {
    fn get_secret<'a>(&'a self, id: &'a str) -> BoxFuture<'a, String> {
        Box::pin(async move {
            let record = self.read_record(id)?;
            let KeyHandle(key) = ks::get_key(id).await?;
            let ptext = match ks::decrypt(&key, &record.ciphertext, &record.iv, &record.aad).await? {
                Text::Plaintext(ptext) => ptext,
                _ => panic!("DECRYPTION API CHANGED")
            };

            Ok(ptext)
        })
    }

    fn set_secret<'a>(&'a self, id: &'a str, value: String) -> BoxFuture<'a, ()> {
        Box::pin(async move {
            let rng = SystemRandom::new();

            let KeyHandle(key) = ks::create_key(id).await?;
            let iv: String = encode(generate::<[u8; IV_BYTES]>(&rng)?.expose());
            let aad: String = encode(generate::<[u8; AAD_BYTES]>(&rng)?.expose());

            let ctext = match ks::encrypt(&key, &value, &iv, &aad).await? {
                Text::Ciphertext(ctext) => ctext,
                _ => panic!("ENCRYPTION API CHANGED")
            };

            self.write_record(id, Record {
                ciphertext: ctext,
                iv: iv,
                aad: aad
            })?;

            Ok(())
        })
    }

    /*
    fn pull_secrets(&'a self, vault: ..., keys: [&str]) -> Pin<Box<dyn Future<Output = BoxedResult<()>> + 'a>>;
    */
}

#[derive(Deserialize, Serialize, Zeroize)]
#[zeroize(drop)]
pub struct Record {
    pub ciphertext: String,
    pub iv: String,
    pub aad: String
}

// WARN: THIS IS VERY IMPORTANT
impl<T: StoreBackend> Store for T { }
