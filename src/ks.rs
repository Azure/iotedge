use crate::constants::*;
use crate::util::*;

use hyper::{Body, Client, Method, Request, Response, Uri};
use percent_encoding::percent_encode;
use serde::{Deserialize, Serialize};
use serde_json::{json, to_string, Value};
use zeroize::Zeroize;

#[derive(Deserialize, Zeroize)]
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

async fn call<'a>(method: Method, resource: String, payload: Option<impl Serialize>) -> BoxResult<'a, Response<Body>> {
    let client = Client::new();
    let uri = format!("{}{}", HSM_SERVER, percent_encode(resource.as_bytes(), ENCODE_CHARS));

    let req = Request::builder()
        .uri(uri.parse::<Uri>()?)
        .method(method)
        .body(match payload {
            Some(v) => Body::from(to_string(&v).unwrap()),
            None => Body::empty()
        })?;

    Ok(client.request(req).await?)
}

pub async fn create_key<'a>(id: &str) -> BoxResult<'a, KeyHandle> {
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

pub async fn get_key<'a>(id: &str) -> BoxResult<'a, KeyHandle> {
    let res = call(
            Method::GET,
            format!("/keys/{}", id),
            <Option<Value>>::None
        )
        .await?;


    Ok(slurp_json(res).await?)
}

pub async fn encrypt<'a>(key: &str, plaintext: &str, iv: &str, aad: &str) -> BoxResult<'a, Text> {
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

pub async fn decrypt<'a>(key: &str, ciphertext: &str, iv: &str, aad: &str) -> BoxResult<'a, Text> {
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