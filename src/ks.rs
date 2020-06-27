use crate::constants::AES_KEY_BYTES;
use crate::util::*;

use hyper::Method;
use serde::Deserialize;
use serde_json::{json, Value};

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

pub async fn create_key<'a>(id: &str) -> BoxedResult<KeyHandle<'a>> {
    let res = call(
            Method::POST,
            "/keys",
            Some(json!({
                "keyId": id,
                "lengthBytes": AES_KEY_BYTES
            }))
        )
        .await?;

    Ok(slurp_json(res).await?)
}

pub async fn get_key<'a>(id: &str) -> BoxedResult<KeyHandle<'a>> {
    let res = call(
            Method::GET,
            format!("/keys/{}", id),
            <Option<Value>>::None
        )
        .await?;


    Ok(slurp_json(res).await?)
}

pub async fn encrypt<'a>(key: &str, plaintext: &str, iv: &str, aad: &str) -> BoxedResult<Text<'a>> {
    let res = call(
            Method::POST,
            "/encrypt",
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

pub async fn decrypt<'a>(key: &str, ciphertext: &str, iv: &str, aad: &str) -> BoxedResult<Text<'a>> {
    let res = call(
            Method::POST,
            "/decrypt",
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