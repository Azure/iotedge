use crate::config::AADCredentials;
use crate::constants::*;
use crate::error::{Error, ErrorKind};

use std::sync::Arc;

use aziot_key_client_async::Client as KeyClient;
use aziot_key_common::{CreateKeyValue, EncryptMechanism};
use failure::{Fail, ResultExt};
use hyper::client::HttpConnector;
use iotedge_aad::{Auth, TokenSource};
use lazy_static::lazy_static;
use regex::Regex;
use reqwest::Client as ReqwestClient;
use ring::rand::{generate, SystemRandom};
use serde::{Deserialize, Serialize};

lazy_static! {
    static ref REQWEST: Arc<ReqwestClient> = Arc::new(ReqwestClient::new());
    static ref AAD_CLIENT: Auth = Auth::new(REQWEST.clone(), "https://vault.azure.net");
    static ref KEY_CLIENT: KeyClient<HttpConnector> = KeyClient::new(HttpConnector::new());
    static ref VAULT_REGEX: Regex = Regex::new(r"(?P<vault_id>[0-9a-zA-Z-]+)/(?P<secret_id>[0-9a-zA-Z-]+)(?:/(?P<secret_version>[0-9a-zA-Z-]+))?").unwrap();
}

#[derive(Deserialize, Serialize)]
pub struct Record {
    pub ciphertext: Vec<u8>,
    pub iv: Vec<u8>,
    pub aad: Vec<u8>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub upstream: Option<String>
}

// NOTE: open to changing implementation so that Sync is not required
pub trait StoreBackend: Send + Sync {
    type Error: Fail + PartialEq + 'static;

    fn init(&self) -> Result<(), Self::Error>;

    fn write_record(&self, id: &str, record: Record) -> Result<(), Self::Error>;
    fn update_record(&self, id: &str, record: Record) -> Result<(), Self::Error>;
    fn try_read_record(&self, id: &str) -> Result<Option<Record>, Self::Error>;
    fn delete_record(&self, id: &str) -> Result<(), Self::Error>;
}

// NOTE: not fully public since high-level functions should be
//       invariant over backend implementation
pub(crate) struct Store<T: StoreBackend> {
    backend: T,
    credentials: AADCredentials
}

impl<T: StoreBackend> Store<T> {
    pub fn new(backend: T, credentials: AADCredentials) -> Self {
        backend.init()
            .expect("Could not initialize storage backend");

        Self {
            backend: backend,
            credentials: credentials
        }
    }

    pub async fn get_secret(&self, id: String) -> Result<String, Error> {
        let record = self.backend
            .try_read_record(&id)
            .context(ErrorKind::Backend("Read"))?;
        if let Some(record) = record {
            let key_handle = KEY_CLIENT.create_key_if_not_exists(
                    &id,
                    CreateKeyValue::Generate { length: AES_KEY_BYTES }
                )
                .await
                .context(ErrorKind::KeyService("GetKey"))?;
            let pbytes = KEY_CLIENT.decrypt(
                    &key_handle,
                    EncryptMechanism::Aead {
                        iv: record.iv,
                        aad: record.aad
                    },
                    record.ciphertext.as_slice()
                )
                .await
                .context(ErrorKind::KeyService("Decrypt"))?;

            let ptext = String::from_utf8(pbytes)
                .context(ErrorKind::CorruptData)?;

            Ok(ptext)
        }
        else {
            Err(Error::from(ErrorKind::NotFound))
        }
    }

    pub async fn set_secret(&self, id: String, value: String) -> Result<(), Error> {
        let (ciphertext, iv, aad) = encrypt(&id, value).await?;
        self.backend
            .write_record(&id, Record {
                ciphertext: ciphertext,
                iv: iv,
                aad: aad,
                upstream: None
            })
            .context(ErrorKind::Backend("Write"))?;
        Ok(())
    }

    pub async fn delete_secret(&self, id: String) -> Result<(), Error> {
        self.backend
            .delete_record(&id)
            .context(ErrorKind::Backend("Delete"))?;

        Ok(())
    }

    pub async fn pull_secret(&self, id: String, remote: String) -> Result<(), Error> {
        let token = AAD_CLIENT
            .authorize_with_secret(
                &self.credentials.tenant_id,
                &self.credentials.client_id,
                &self.credentials.client_secret
            )
            .await
            .map_err(|_| ErrorKind::Azure("GetToken"))?
            .get()
            .to_string();

        if let Some(captures) = VAULT_REGEX.captures(&remote) {
            let vault_id = captures.name("vault_id").map_or_else(|| "", |grp| grp.as_str());
            let secret_id = captures.name("secret_id").map_or_else(|| "", |grp| grp.as_str());
            let secret_version = captures.name("secret_version").map_or_else(|| "", |grp| grp.as_str());

            let key_uri = format!(
                    "https://{}.vault.azure.net/secrets/{}/{}?api-version={}",
                    vault_id,
                    secret_id,
                    secret_version,
                    AKV_API_VERSION
                );

            let res = REQWEST.get(&key_uri)
                .bearer_auth(token)
                .send()
                .await
                .map_err(|_| ErrorKind::Azure("Fetch"))?;
            let value = res.json::<serde_json::Value>().await
                .map_err(|_| ErrorKind::Reqwest)?
                .get("value")
                .ok_or(ErrorKind::Azure("UnexpectedAPIResult"))?
                .as_str()
                .ok_or(ErrorKind::Azure("UnexpectedAPIResult"))?
                .to_string();
            
            let (ciphertext, iv, aad) = encrypt(&id, value).await?;
            self.backend
                .write_record(&id, Record {
                    ciphertext: ciphertext,
                    iv: iv,
                    aad: aad,
                    upstream: Some(key_uri)
                })
                .context(ErrorKind::Backend("Write"))?;
            Ok(())
        }
        else {
            Err(Error::from(ErrorKind::Azure("BadAKVSpecifier")))
        }
    }
}

async fn encrypt(id: &str, value: String) -> Result<(Vec<u8>, Vec<u8>, Vec<u8>), Error> {
    let rng = SystemRandom::new();

    let key_handle = KEY_CLIENT.create_key_if_not_exists(
            &id,
            CreateKeyValue::Generate { length: AES_KEY_BYTES }
        )
        .await
        .context(ErrorKind::KeyService("GetKey"))?;
    let iv = generate::<[u8; IV_BYTES]>(&rng)
        .context(ErrorKind::RandomNumberGenerator)?
        .expose()
        .to_vec();
    let aad = generate::<[u8; AAD_BYTES]>(&rng)
        .context(ErrorKind::RandomNumberGenerator)?
        .expose()
        .to_vec();

    let ctext = KEY_CLIENT.encrypt(
            &key_handle,
            EncryptMechanism::Aead {
                iv: iv.clone(),
                aad: aad.clone()
            },
            value.as_bytes()
        )
        .await
        .context(ErrorKind::KeyService("Encrypt"))?;

    Ok((ctext, iv, aad))
}