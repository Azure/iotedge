use crate::constants::*;
use crate::util::*;

use hyper::{Body, Client, Method, Request, Response, Uri};
use percent_encoding::percent_encode;
use serde::{Deserialize, Serialize};
use serde_json::{json, to_string};
use zeroize::Zeroize;

#[derive(Deserialize, Zeroize)]
#[serde(rename_all = "camelCase")]
pub enum Key {
    KeyHandle(String)
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
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
        .header("Content-Type", "application/json")
        .body(match payload {
            Some(v) => { println!("{}", to_string(&v).unwrap()); to_string(&v).unwrap().into() },
            None => Body::empty()
        })?;

    // NOTE: convert to boxed result
    Ok(client.request(req).await?)
}

pub async fn create_or_get_key<'a>(id: &str) -> BoxResult<'a, Key> {
    let res = call(
            Method::POST,
            String::from("/key"),
            Some(json!({
                "keyId": id,
                "lengthBytes": AES_KEY_BYTES
            }))
        )
        .await?;

    slurp_json(res).await
}

pub async fn encrypt<'a>(key: &str, plaintext: &str, iv: &str, aad: &str) -> BoxResult<'a, Text> {
    let res = call(
            Method::POST,
            String::from("/encrypt"),
            Some(json!({
                "keyHandle": key,
                "algorithm": "AEAD",
                "parameters": {
                    "iv": iv,
                    "aad": aad
                },
                "plaintext": plaintext
            }))
        )
        .await?;

    slurp_json(res).await
}

pub async fn decrypt<'a>(key: &str, ciphertext: &str, iv: &str, aad: &str) -> BoxResult<'a, Text> {
    let res = call(
            Method::POST,
            String::from("/decrypt"),
            Some(json!({
                "keyHandle": key,
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