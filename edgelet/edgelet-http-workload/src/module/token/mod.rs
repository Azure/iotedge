pub(crate) mod generate;
pub(crate) mod validate;

#[cfg(not(test))]
use aziot_key_client_async::Client as KeyClient;

#[cfg(test)]
use edgelet_test_utils::clients::KeyClient;

#[cfg(not(test))]
use aziot_identity_client_async::Client as IdentityClient;

#[cfg(test)]
use edgelet_test_utils::clients::IdentityClient;

use log::{error, info};
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

pub(crate) const TOKEN_EXPIRY_TIME_SECONDS: u64 = 3600;

#[derive(Debug, Serialize, Deserialize)]
pub struct Token {
    header: TokenHeader,
    claims: TokenClaims,
    signature: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenHeader {
    alg: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenClaims {
    sub: String,
    aud: String,
    exp: u64,
    #[serde(rename = "aziot-id", skip_serializing_if = "Option::is_none")]
    aziot_id: Option<String>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenGenerateResponse {
    token: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenValidateRequest {
    token: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct TokenValidateResponse {
    token: String,
}

struct TokenGeneratorAPI {
    key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
    identity_client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,
    device_id: String,
    iot_hub: String,
}

impl TokenGeneratorAPI {
    pub fn new(
        key_client: std::sync::Arc<futures_util::lock::Mutex<KeyClient>>,
        identity_client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,
        config: &crate::WorkloadConfig,
    ) -> Self {
        TokenGeneratorAPI {
            key_client,
            identity_client,
            device_id: config.device_id.clone(),
            iot_hub: config.hub_name.clone(),
        }
    }

    pub async fn generate_token(
        self,
        module_id: String,
        exp: u64,
    ) -> Result<hyper::Response<hyper::Body>, http_common::server::Error> {
        let module_key = get_device_key(self.identity_client).await?;
        let key_client = self.key_client.lock().await;

        let sub = format!(
            "spiffe://{}/{}/{}",
            &self.iot_hub, &self.device_id, module_id
        );

        let header = TokenHeader {
            alg: "HS256".to_string(),
        };
        let header = serde_json::to_string(&header).map_err(|_| {
            edgelet_http::error::server_error("failed to parse header in JWT token")
        })?;
        let header = base64::encode_config(header.as_bytes(), base64::STANDARD_NO_PAD);

        let claims = TokenClaims {
            sub,
            aud: module_id.to_string(),
            exp,
            aziot_id: None,
        };
        let claims = serde_json::to_string(&claims).map_err(|_| {
            edgelet_http::error::server_error("failed to parse claims in JWT token")
        })?;
        let claims = base64::encode_config(claims.as_bytes(), base64::STANDARD_NO_PAD);

        info!("generating signature {}", format!("{}.{}", header, claims));
        let signature = key_client
            .sign(
                &module_key,
                aziot_key_common::SignMechanism::HmacSha256,
                format!("{}.{}", header, claims).as_bytes(),
            )
            .await
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;
        let signature = base64::encode_config(signature, base64::STANDARD_NO_PAD);

        let token = format!("{}.{}.{}", header, claims, signature);

        info!("Generating token: {}", token);

        let response = TokenGenerateResponse { token };

        let response = http_common::server::response::json(hyper::StatusCode::CREATED, &response);

        Ok(response)
    }

    pub async fn validate_token(
        self,
        _module_id: String,
        token: String,
        exp: u64,
    ) -> Result<hyper::Response<hyper::Body>, http_common::server::Error> {
        let key_client = self.key_client.lock().await;

        info!("Received token: {}", token);
        let split = token.split('.').collect::<Vec<&str>>();

        if split.len() != 3 {
            return Err(edgelet_http::error::bad_request("Invalid token format"));
        }

        let header_b64encoded = split[0];
        let claims_b64encoded = split[1];
        let signature_to_validate_b64encoded = split[2];

        let claims = base64::decode_config(claims_b64encoded, base64::STANDARD_NO_PAD)
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;
        let claims: TokenClaims = serde_json::from_slice(&claims)
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;


        // Validate the signature
        info!("checking signature {}", format!("{}.{}", header_b64encoded, claims_b64encoded));
        let module_key = get_device_key(self.identity_client).await?;
        let signature = key_client
            .sign(
                &module_key,
                aziot_key_common::SignMechanism::HmacSha256,
                format!("{}.{}", header_b64encoded, claims_b64encoded).as_bytes(),
            )
            .await
            .map_err(|err| edgelet_http::error::server_error(err.to_string()))?;
        let signature_b64encoded = base64::encode_config(signature, base64::STANDARD_NO_PAD);

        if !signature_b64encoded.eq(signature_to_validate_b64encoded) {
            error!("Signature error, expected {}, got {}", signature_b64encoded, signature_to_validate_b64encoded);
            return Err(edgelet_http::error::bad_request(
                "Could not authenticate token",
            ));
        }

        // Validate if the token is not expired.
        if exp > claims.exp {
            return Err(edgelet_http::error::bad_request("Token is expired"));
        }

        info!("Token is validated");

        let response = TokenValidateResponse { token };

        let response = http_common::server::response::json(hyper::StatusCode::OK, &response);

        Ok(response)
    }
}

// !! duplicated code
async fn get_device_key(
    client: std::sync::Arc<futures_util::lock::Mutex<IdentityClient>>,
) -> Result<aziot_key_common::KeyHandle, http_common::server::Error> {
    let identity = {
        let client = client.lock().await;

        client.get_device_identity().await.map_err(|err| {
            edgelet_http::error::server_error(format!(
                "failed to get device identity {}",
                err
            ))
        })
    }?;

    let identity = match identity {
        aziot_identity_common::Identity::Aziot(identity) => identity,
        aziot_identity_common::Identity::Local(_) => {
            return Err(edgelet_http::error::server_error("invalid identity type"))
        }
    };

    let auth = identity
        .auth
        .ok_or_else(|| edgelet_http::error::server_error("module identity missing auth"))?;

    auth.key_handle
        .ok_or_else(|| edgelet_http::error::server_error("module identity missing key"))
}
