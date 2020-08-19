use crate::config::AADCredentials;
use crate::constants::*;
use crate::error::{Error, ErrorKind};
use crate::util::*;

use std::sync::Arc;

use aziot_key_client_async::Client as KeyClient;
use aziot_key_common::{CreateKeyValue, EncryptMechanism};
use failure::{Fail, ResultExt};
use hyper::{Body, Client as HyperClient, Request, Uri};
use hyper::client::HttpConnector;
use hyper_tls::HttpsConnector;
use iotedge_aad::{Auth, TokenSource};
use lazy_static::lazy_static;
use regex::{Match, Regex};
use reqwest::Client as ReqwestClient;
use ring::rand::{generate, SystemRandom};
use serde::{Deserialize, Serialize};

lazy_static! {
    static ref AUTH_CLIENT: Auth = Auth::new(Arc::new(ReqwestClient::new()), "https://vault.azure.net");
    static ref HTTPS_CLIENT: HyperClient<HttpsConnector<HttpConnector>> = HyperClient::builder().build(HttpsConnector::new());
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
    fn read_record(&self, id: &str) -> Result<Record, Self::Error>;
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
            .read_record(&id)
            .context(ErrorKind::Backend("Read"))?;
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

    pub async fn set_secret(&self, id: String, value: String) -> Result<String, Error> {

        let (ciphertext, iv, aad) = encrypt(&id, value).await?;
        self.backend
            .write_record(&id, Record {
                ciphertext: ciphertext,
                iv: iv,
                aad: aad,
                upstream: None
            })
            .context(ErrorKind::Backend("Write"))?
    }

    pub async fn pull_secret(&self, id: String, remote: String) -> Result<(), Error> {
        let token = AUTH_CLIENT
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

            let req = Request::builder()
                .uri(key_uri.parse::<Uri>().context(ErrorKind::Azure("InvalidKeyUri"))?)
                .header("Authorization", format!("Bearer {}", token))
                .body(Body::empty())
                .context(ErrorKind::Hyper)?;
            let res = HTTPS_CLIENT.request(req)
                .await
                .context(ErrorKind::Hyper)?;
            let value = slurp_json::<serde_json::Value>(res)
                .await
                .map_err(|_| ErrorKind::Hyper)?
                .get("value")
                .context(ErrorKind::Azure("UnexpectedAPIResult"))?
                .as_str()
                .context(ErrorKind::Azure("UnexpectedAPIResult"))?;
            
            let (ciphertext, iv, aad) = encrypt(&id, value).await?;
            self.backend
                .write_record(&id, Record {
                    ciphertext: ciphertext,
                    iv: iv,
                    aad: aad,
                    upstream: Some(key_uri)
                })
                .context(ErrorKind::Backend("Write"))?
        }
        else {
            unimplemented!()
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
                iv: iv,
                aad: aad
            },
            value.as_bytes()
        )
        .await
        .context(ErrorKind::KeyService("Encrypt"))?;

    Ok((ctext, iv, aad))
}