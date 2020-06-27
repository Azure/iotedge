use crate::constants::AES_KEY_BYTES;
use crate::util::*;

use hyper::Method;
use serde::Deserialize;
use serde_json::{json, Value};

#[derive(Deserialize)]
pub struct KeyHandle(
    #[serde(rename = "keysServiceHandle")]
    pub String
);

#[derive(Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum Text {
    Plaintext(String),
    Ciphertext(String)
}

pub async fn create_key<'a>(id: &str) -> BoxedResult<'a, KeyHandle> {
    let res = call(
            Method::POST,
            String::from("/keys"),
            Some(json!({
                "keyId": id,
                "lengthBytes": AES_KEY_BYTES
            }))
        )
        .await?;

    Ok(slurp_json(res).await?)
}

pub async fn get_key<'a>(id: &str) -> BoxedResult<'a, KeyHandle> {
    let res = call(
            Method::GET,
            format!("/keys/{}", id),
            <Option<Value>>::None
        )
        .await?;


    Ok(slurp_json(res).await?)
}

pub async fn encrypt<'a>(key: &str, plaintext: &str, iv: &str, aad: &str) -> BoxedResult<'a, Text> {
    let res = call(
            Method::POST,
            String::from("/encrypt"),
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

pub async fn decrypt<'a>(key: &str, ciphertext: &str, iv: &str, aad: &str) -> BoxedResult<'a, Text> {
    let res = call(
            Method::POST,
            String::from("/decrypt"),
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