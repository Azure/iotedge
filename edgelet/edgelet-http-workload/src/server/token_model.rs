use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct Token {
    header : TokenHeader,
    claims : TokenClaims,
    signature : String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenHeader {
   alg: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenClaims {
   sub: String,
   aud: String,
   exp: u32,
   #[serde(rename = "aziot-id", skip_serializing_if = "Option::is_none")]
   aziot_id: Option<String>, 
}
