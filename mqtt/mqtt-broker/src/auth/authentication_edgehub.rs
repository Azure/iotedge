use async_trait::async_trait;
use serde::{Deserialize, Serialize};
use serde_repr::Deserialize_repr;

use crate::auth::{AuthId, Authenticator, Credentials};
use crate::{AuthenticationError, Error};

const API_VERSION: &str = "2020-04-20";

#[derive(Clone)]
pub struct EdgeHubAuthenticator {
    url: String,
}

#[async_trait]
impl Authenticator for EdgeHubAuthenticator {
    type Error = Error;

    async fn authenticate(
        &self,
        username: Option<String>,
        credentials: Credentials,
    ) -> Result<Option<AuthId>, Self::Error> {
        self.authenticate_client((username, credentials).into())
            .await
            .map_err(Error::Authenticate)?
            .into()
    }
}

impl EdgeHubAuthenticator {
    async fn authenticate_client(
        &self,
        request: EdgeHubAuthRequest,
    ) -> Result<EdgeHubAuthResponse, AuthenticationError> {
        let response = reqwest::Client::new()
            .post(&self.url)
            .json(&request)
            .send()
            .await
            .map_err(AuthenticationError::SendRequest)?;

        if response.status() != reqwest::StatusCode::OK {
            return Err(AuthenticationError::ProcessResponse);
        }

        let response = response
            .json::<EdgeHubAuthResponse>()
            .await
            .map_err(|_| AuthenticationError::ProcessResponse)?;

        if !response.version.eq(API_VERSION) {
            return Err(AuthenticationError::ApiVersion(response.version));
        }

        Ok(response)
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

impl From<(Option<String>, Credentials)> for EdgeHubAuthRequest {
    fn from((username, credentials): (Option<String>, Credentials)) -> Self {
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

impl From<EdgeHubAuthResponse> for Result<Option<AuthId>, Error> {
    fn from(item: EdgeHubAuthResponse) -> Self {
        match item.result {
            EdgeHubResultCode::Authenticated => item
                .identity
                .map_or(Err(AuthenticationError::ProcessResponse.into()), |i| {
                    Ok(Some(AuthId::Identity(i)))
                }),
            EdgeHubResultCode::Unauthenticated => Ok(None),
        }
    }
}

#[cfg(test)]
mod tests {

    use base64::decode;
    use mockito::{mock, Matcher};

    use crate::auth::authentication_edgehub::EdgeHubAuthenticator;

    use crate::auth::{AuthId, Authenticator, Certificate, Credentials};

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

        let authenticator = EdgeHubAuthenticator::for_test();
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

        let authenticator = EdgeHubAuthenticator::for_test();
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

        let authenticator = EdgeHubAuthenticator::for_test();
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

        let authenticator = EdgeHubAuthenticator::for_test();
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

        let authenticator = EdgeHubAuthenticator::for_test();
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

        let authenticator = EdgeHubAuthenticator::for_test();
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

        let authenticator = EdgeHubAuthenticator::for_test();
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

        let authenticator = EdgeHubAuthenticator::for_test();
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

        let authenticator = EdgeHubAuthenticator::for_test();
        let result = authenticator
            .authenticate(
                Some("somehub/somedevice/api-version=2018-06-30".to_string()),
                password("qwerty123"),
            )
            .await;

        assert!(result.is_ok());
    }

    impl EdgeHubAuthenticator {
        fn for_test() -> Self {
            EdgeHubAuthenticator {
                url: mockito::server_url() + "/authenticate/",
            }
        }
    }

    fn password(password: &str) -> Credentials {
        Credentials::Password(Some(password.to_string()))
    }
}
