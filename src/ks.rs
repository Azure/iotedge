use crate::constants::{AES_KEY_BYTES, ENCODE_CHARS};
use crate::util::*;

use hyper::{Client, Method};
use hyper::client::HttpConnector;
use percent_encoding::percent_encode;
use serde_json::{json, from_value, Value};

pub struct Key<'a> {
    id: &'a str,
    value: &'a str
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

    pub async fn create_key(&self, id: &str) -> BoxedResult<Key<'_>> {
        let res = call(
                Method::POST,
                format!("{}/keys", self.endpoint),
                Some(json!({
                    "keyId": id,
                    "lengthBytes": AES_KEY_BYTES
                }))
            )
            .await?;

        let out = slurp_json::<Value>(res).await?;

        Ok(Key {
            id: id,
            value: from_value(out["keysServiceHandle"])?
        })
    }

    pub async fn get_key(&self, id: &str) -> BoxedResult<Key<'_>> {
        let res = call(
                Method::GET,
                format!("{}/keys/{}", self.endpoint, percent_encode(id.as_bytes(), ENCODE_CHARS)),
                None
            )
            .await?;

        let out = slurp_json::<Value>(res).await?;

        Ok(Key {
            id: id,
            value: from_value(out["keysServiceHandle"])?
        })
    }

    // pub async fn encrypt(&self, key: &str, value, &str) -> BoxedResult<...>
}