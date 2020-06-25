use crate::constants::{AES_KEY_BYTES, ENCODE_CHARS};
use crate::util::*;

use hyper::{Client, Method};
use hyper::client::HttpConnector;
use percent_encoding::percent_encode;
use serde::Deserialize;
use serde_json::json;

#[derive(Deserialize)]
pub struct KeyHandle<'a>(
    #[serde(rename = "keysServiceHandle")]
    pub &'a str
);

#[derive(Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum Text<'a> {
    Plaintext(&'a str),
    Ciphertext(&'a str)
}

pub struct KSClient<'a> {
    endpoint: &'a str,
    client: Client<HttpConnector>
}

impl<'a> KSClient<'a> {
    pub fn new(endpoint: &'a str) -> Self {
        KSClient {
            endpoint: endpoint,
            client: Client::new()
        }
    }

    pub async fn create_key(&self, id: &str) -> BoxedResult<KeyHandle<'_>> {
        let res = call(
                Method::POST,
                format!("{}/keys", self.endpoint),
                Some(json!({
                    "keyId": id,
                    "lengthBytes": AES_KEY_BYTES
                }))
            )
            .await?;

        Ok(slurp_json(res).await?)
    }

    pub async fn get_key(&self, id: &str) -> BoxedResult<KeyHandle<'_>> {
        let res = call(
                Method::GET,
                format!("{}/keys/{}", self.endpoint, percent_encode(id.as_bytes(), ENCODE_CHARS)),
                None
            )
            .await?;

        Ok(slurp_json(res).await?)
    }

    pub async fn encrypt(&self, key: &str, plaintext: &str, iv: &str, aad: &str) -> BoxedResult<Text<'_>> {
        let res = call(
                Method::POST,
                format!("{}/encrypt", self.endpoint),
                Some(json!({
                    "keysServiceHandle": key,
                    "algorithm": "AEAD",
                    "parameters": {
                        "iv": iv,
                        "aad": aad
                    },
                    "plaintext": plaintext
                }))
            )
            .await?;

        Ok(slurp_json(res).await?)
    }

    pub async fn decrypt(&self, key: &str, ciphertext: &str, iv: &str, aad: &str) -> BoxedResult<Text<'_>> {
        let res = call(
                Method::POST,
                format!("{}/decrypt", self.endpoint),
                Some(json!({
                    "keysServiceHandle": key,
                    "algorithm": "AEAD",
                    "parameters": {
                        "iv": iv,
                        "aad": aad
                    },
                    "ciphertext": ciphertext
                }))
            )
            .await?;
        
        Ok(slurp_json(res).await?)
    }
}