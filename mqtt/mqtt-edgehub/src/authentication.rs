use std::convert::{TryFrom, TryInto};

use async_trait::async_trait;
use reqwest::Client;
use serde::{Deserialize, Serialize};
use serde_repr::Deserialize_repr;

use mqtt_broker_core::auth::{AuthId, Authenticator, Credentials};

const API_VERSION: &str = "2020-04-20";

#[derive(Clone)]
pub struct EdgeHubAuthenticator(Client, String);

impl EdgeHubAuthenticator {
    #[allow(dead_code)]
    pub fn new(url: String) -> Self {
        let client = reqwest::Client::new();
        Self(client, url)
    }
}

#[async_trait]
impl Authenticator for EdgeHubAuthenticator {
    type Error = AuthenticateError;

    async fn authenticate(
        &self,
        username: Option<String>,
        credentials: Credentials,
    ) -> Result<Option<AuthId>, Self::Error> {
        let response = self
            .0
            .post(&self.1)
            .json(&EdgeHubAuthRequest::new(username, credentials))
            .send()
            .await
            .map_err(AuthenticateError::SendRequest)?;

        if response.status() != reqwest::StatusCode::OK {
            return Err(AuthenticateError::ProcessResponse);
        }

        let response = response
            .json::<EdgeHubAuthResponse>()
            .await
            .map_err(|_| AuthenticateError::ProcessResponse)?;

        if response.version != API_VERSION {
            return Err(AuthenticateError::ApiVersion(response.version));
        }

        response.try_into()
    }
}

#[derive(Serialize)]
#[serde(rename_all = "snake_case")]
pub struct EdgeHubAuthRequest {
    version: String,
    username: Option<String>,
    password: Option<String>,
    certificate: Option<String>,
    certificate_chain: Option<Vec<String>>,
}

impl EdgeHubAuthRequest {
    fn new(username: Option<String>, credentials: Credentials) -> Self {
        let (password, certificate) = match credentials {
            Credentials::Password(p) => (p, None),
            Credentials::ClientCertificate(c) => (None, Some(base64::encode(c))),
        };

        EdgeHubAuthRequest {
            version: API_VERSION.to_string(),
            username,
            password,
            certificate,
            certificate_chain: None,
        }
    }
}

#[derive(Deserialize_repr, Copy, Clone)]
#[repr(i32)]
pub enum EdgeHubResultCode {
    Authenticated = 200,
    Unauthenticated = 403,
}

#[derive(Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct EdgeHubAuthResponse {
    version: String,
    result: EdgeHubResultCode,
    identity: Option<String>,
}

impl TryFrom<EdgeHubAuthResponse> for Option<AuthId> {
    type Error = AuthenticateError;

    fn try_from(value: EdgeHubAuthResponse) -> Result<Self, Self::Error> {
        match value.result {
            EdgeHubResultCode::Authenticated => value
                .identity
                .map_or(Err(AuthenticateError::ProcessResponse), |i| {
                    Ok(Some(AuthId::Identity(i)))
                }),
            EdgeHubResultCode::Unauthenticated => Ok(None),
        }
    }
}

/// Authentication error.
#[derive(Debug, thiserror::Error)]
pub enum AuthenticateError {
    #[error("failed to send request: {0}.")]
    SendRequest(#[from] reqwest::Error),

    #[error("failed to process response.")]
    ProcessResponse,

    #[error("not supported response version {0}.")]
    ApiVersion(String),
}

#[cfg(test)]
mod tests {
    use base64::decode;
    use mockito::{mock, Matcher};

    use mqtt_broker_core::auth::{AuthId, Authenticator, Certificate, Credentials};

    use crate::authentication::EdgeHubAuthenticator;

    const CERT: &str = "MIIBLjCB1AIJAOTg4Zxl8B7jMAoGCCqGSM49BAMCMB8xHTAbBgNVBAMMFFRodW1i\
                        cHJpbnQgVGVzdCBDZXJ0MB4XDTIwMDQyMzE3NTgwN1oXDTMzMTIzMTE3NTgwN1ow\
                        HzEdMBsGA1UEAwwUVGh1bWJwcmludCBUZXN0IENlcnQwWTATBgcqhkjOPQIBBggq\
                        hkjOPQMBBwNCAARDJJBtVlgM0mBWMhAYagF7Wuc2aQYefhj0cG4wAmn3M4XcxJ39\
                        XkEup2RRAj7SSdOYhTmRpg5chhpZX/4/eF8gMAoGCCqGSM49BAMCA0kAMEYCIQD/\
                        wNzMjU1B8De5/+jEif8rkLDtqnohmVRXuAE5dCfbvAIhAJTJ+Fyg19uLSKVyOK8R\
                        5q87sIqhJXhTfNYvIt77Dq4J";

    #[tokio::test]
    async fn response_ok_feeds_identity() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(
                r#"{"result": 200, "identity":"somehub/somedevice","version": "2020-04-20"}"#,
            )
            .create();

        let authenticator = authenticator();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                password("qwerty123"),
            )
            .await
            .unwrap()
            .unwrap();

        let s = "somehub/somedevice".to_string();
        assert_eq!(result, AuthId::Identity(s));
    }

    #[tokio::test]
    async fn response_ok_requires_identity() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(r#"{"result": 200,"version": "2020-04-20"}"#)
            .create();

        let authenticator = authenticator();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                password("qwerty123"),
            )
            .await;

        assert!(result.is_err());
    }

    #[tokio::test]
    async fn response_unauth_returns_none() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(r#"{"result": 403, "version": "2020-04-20"}"#)
            .create();

        let authenticator = authenticator();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                password("qwerty123"),
            )
            .await
            .unwrap();

        assert!(result.is_none());
    }

    #[tokio::test]
    async fn http_non_ok_results_error() {
        let _m = mock("POST", "/authenticate/")
            .with_status(500)
            .with_body(
                r#"{"result": 200, "identity":"somehub/somedevice","version": "2020-04-20"}"#,
            )
            .create();

        let authenticator = authenticator();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                password("qwerty123"),
            )
            .await;

        assert!(result.is_err());
    }

    #[tokio::test]
    async fn bad_api_version_results_error() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(
                r#"{"result": 200, "identity":"somehub/somedevice","version": "2222-22-22"}"#,
            )
            .create();

        let authenticator = authenticator();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                password("qwerty123"),
            )
            .await;

        assert!(result.is_err());
    }

    #[tokio::test]
    async fn no_result_code_results_error() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(r#"{"identity":"somehub/somedevice","version": "2222-22-22"}"#)
            .create();

        let authenticator = authenticator();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                password("qwerty123"),
            )
            .await;

        assert!(result.is_err());
    }

    #[tokio::test]
    async fn password_cred_sends_password() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .match_body(Matcher::Regex("\"password\":\"qwerty123\"".to_string()))
            .with_body(
                r#"{"result": 200, "identity":"somehub/somedevice","version": "2020-04-20"}"#,
            )
            .create();

        let authenticator = authenticator();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                password("qwerty123"),
            )
            .await;

        assert!(result.is_ok());
    }

    #[tokio::test]
    async fn cert_cred_sends_cert() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .match_body(Matcher::Regex("\"certificate\":\"MIIBL".to_string()))
            .with_body(
                r#"{"result": 200, "identity":"somehub/somedevice","version": "2020-04-20"}"#,
            )
            .create();

        let credentials =
            Credentials::ClientCertificate(Certificate::from(decode(CERT.to_string()).unwrap()));

        let authenticator = authenticator();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                credentials,
            )
            .await;

        assert!(result.is_ok());
    }

    #[tokio::test]
    async fn sends_user_and_version() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .match_body(Matcher::AllOf(vec![
                Matcher::Regex("\"username\":\"somehub".to_string()),
                Matcher::Regex("\"version\":\"2020-04-20".to_string()),
            ]))
            .with_body(
                r#"{"result": 200, "identity":"somehub/somedevice","version": "2020-04-20"}"#,
            )
            .create();

        let authenticator = authenticator();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                password("qwerty123"),
            )
            .await;

        assert!(result.is_ok());
    }

    fn authenticator() -> EdgeHubAuthenticator {
        EdgeHubAuthenticator::new(mockito::server_url() + "/authenticate/")
    }

    fn password(password: &str) -> Credentials {
        Credentials::Password(Some(password.to_string()))
    }
}
