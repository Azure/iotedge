use async_trait::async_trait;
use base64::encode;
use failure::ResultExt;
use serde::{Deserialize, Serialize};
use std::fmt;

use crate::auth::{AuthId, Authenticator, Credentials};
use crate::{Error, ErrorKind};

pub struct EdgeHubAuthenticator {
    url: String,
}

const API_VERSION: &str = "2020-04-20";

#[async_trait]
impl Authenticator for EdgeHubAuthenticator {
    async fn authenticate(
        &self,
        username: Option<String>,
        credentials: Credentials,
    ) -> Result<Option<AuthId>, Error> {
        self.internal_authenticate((username, credentials).into())
            .await
            .context(ErrorKind::AuthExecution)?
            .into_auth_id()
    }
}

impl EdgeHubAuthenticator {
    async fn internal_authenticate(
        &self,
        request: EdgeHubAuthRequest,
    ) -> Result<EdgeHubAuthResponse, Error> {
        let response = reqwest::Client::new()
            .post(&self.url)
            .json(&request)
            .send()
            .await
            .context(ErrorKind::AuthSendRequest)?;

        if response.status() != reqwest::StatusCode::OK {
            return Err(ErrorKind::AuthProcessResponse.into());
        }

        let response = response
            .json::<EdgeHubAuthResponse>()
            .await
            .context(ErrorKind::AuthProcessResponse)?;

        if !response.version.eq(API_VERSION) {
            return Err(ErrorKind::AuthApiVersion(response.version).into());
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
    fn from(item: (Option<String>, Credentials)) -> Self {
        let (password, certificate) = match item.1 {
            Credentials::Password(p) => (p, None),
            Credentials::ClientCertificate(c) => (None, Some(encode(c))),
        };

        EdgeHubAuthRequest {
            version: API_VERSION.to_string(),
            username: item.0,
            password,
            certificate,
            certificate_chain: None,
        }
    }
}

#[derive(Copy, Clone)]
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

impl EdgeHubAuthResponse {
    fn into_auth_id(self) -> Result<Option<AuthId>, Error> {
        match self.result {
            EdgeHubResultCode::Authenticated => self
                .identity
                .map_or(Err(ErrorKind::AuthProcessResponse.into()), |i| {
                    Ok(Some(AuthId::Value(i)))
                }),
            EdgeHubResultCode::Unauthenticated => Ok(None),
        }
    }
}

macro_rules! impl_for{
    ($fun_name:ident, $typ:ident) => {
        fn $fun_name<E: serde::de::Error>(self, v: $typ) -> Result<EdgeHubResultCode, E> {
            match v {
                200 => Ok(EdgeHubResultCode::Authenticated),
                403 => Ok(EdgeHubResultCode::Unauthenticated),
                v => Err(E::custom(format!("unexpected int: {}", v))),
            }
        }
    }
}

struct EdgeHubResultCodeVisitor;
impl<'de> serde::de::Visitor<'de> for EdgeHubResultCodeVisitor {
    type Value = EdgeHubResultCode;
    fn expecting(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str("200 or 403")
    }

    impl_for!(visit_i32, i32);
    impl_for!(visit_i64, i64);
    impl_for!(visit_u32, u32);
    impl_for!(visit_u64, u64);
}

impl<'de> serde::Deserialize<'de> for EdgeHubResultCode {
    fn deserialize<D: serde::Deserializer<'de>>(d: D) -> Result<EdgeHubResultCode, D::Error> {
        d.deserialize_i32(EdgeHubResultCodeVisitor)
    }
}

#[cfg(test)]
mod tests {

    use base64::decode;
    use matches::assert_matches;
    use mockito::{mock, Matcher};

    use crate::auth::{AuthId, Authenticator, Certificate, Credentials};
    use crate::edgehub_auth::EdgeHubAuthenticator;
    use crate::Error;

    const CERT: &str = "MIIBLjCB1AIJAOTg4Zxl8B7jMAoGCCqGSM49BAMCMB8xHTAbBgNVBAMMFFRodW1i\
                        cHJpbnQgVGVzdCBDZXJ0MB4XDTIwMDQyMzE3NTgwN1oXDTMzMTIzMTE3NTgwN1ow\
                        HzEdMBsGA1UEAwwUVGh1bWJwcmludCBUZXN0IENlcnQwWTATBgcqhkjOPQIBBggq\
                        hkjOPQMBBwNCAARDJJBtVlgM0mBWMhAYagF7Wuc2aQYefhj0cG4wAmn3M4XcxJ39\
                        XkEup2RRAj7SSdOYhTmRpg5chhpZX/4/eF8gMAoGCCqGSM49BAMCA0kAMEYCIQD/\
                        wNzMjU1B8De5/+jEif8rkLDtqnohmVRXuAE5dCfbvAIhAJTJ+Fyg19uLSKVyOK8R\
                        5q87sIqhJXhTfNYvIt77Dq4J";

    #[tokio::test]
    async fn test_call_auth_agent() {
        let _m = mock("POST", "/authenticate/")
        .with_status(200)
        .with_body(r#"{"result": 200, "identity":"vikauthtest.azure-devices.net/ca_root","version": "2020-04-20"}"#)
        .create();

        let authenticator = EdgeHubAuthenticator {
            url: mockito::server_url() + "/authenticate/",
        };
        let cert_as_string = CERT.to_string();
        let raw_cert_result = decode(cert_as_string);
        let raw_cert = raw_cert_result.unwrap();
        let cert: Certificate = Certificate::from(raw_cert);
        let credentials = Credentials::ClientCertificate(cert);

        let result = authenticator
            .authenticate(
                Some("vikauthtest/thumb2/api-version=2018-06-30".to_string()),
                credentials,
            )
            .await;

        assert_matches!(result, Ok(Some(_)));
    }

    #[tokio::test]
    async fn response_ok_feeds_identity() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(
                r#"{"result": 200, "identity":"somehub/somedevice","version": "2020-04-20"}"#,
            )
            .create();

        let result = auth_request(
            "somehub/somedevice/api-version=2018-06-30",
            password("qwerty123"),
        )
        .await
        .unwrap()
        .unwrap();

        let s = "somehub/somedevice".to_string();
        assert_eq!(result, AuthId::Value(s));
    }

    #[tokio::test]
    async fn response_ok_requires_identity() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(r#"{"result": 200,"version": "2020-04-20"}"#)
            .create();

        let result = auth_request(
            "somehub/somedevice/api-version=2018-06-30",
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

        let result = auth_request(
            "somehub/somedevice/api-version=2018-06-30",
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

        let result = auth_request(
            "somehub/somedevice/api-version=2018-06-30",
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

        let result = auth_request(
            "somehub/somedevice/api-version=2018-06-30",
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

        let result = auth_request(
            "somehub/somedevice/api-version=2018-06-30",
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

        let result = auth_request(
            "somehub/somedevice/api-version=2018-06-30",
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

        let result = auth_request("somehub/somedevice/api-version=2018-06-30", credentials).await;

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

        let credentials =
            Credentials::ClientCertificate(Certificate::from(decode(CERT.to_string()).unwrap()));

        let result = auth_request("somehub/somedevice/api-version=2018-06-30", credentials).await;

        assert!(result.is_ok());
    }

    async fn auth_request(
        username: &str,
        credentials: Credentials,
    ) -> Result<Option<AuthId>, Error> {
        let authenticator = EdgeHubAuthenticator {
            url: mockito::server_url() + "/authenticate/",
        };

        authenticator
            .authenticate(Some(username.to_string()), credentials)
            .await
    }

    fn password(password: &str) -> Credentials {
        Credentials::Password(Some(password.to_string()))
    }
}
