use std::{
    convert::{TryFrom, TryInto},
    time::Duration,
};

use async_trait::async_trait;
use backoff::{future::FutureOperation as _, Error, ExponentialBackoff};
use bytes::buf::BufExt;
use http::{header, StatusCode};
use hyper::{body, client::HttpConnector, Body, Client, Request};
use serde::{Deserialize, Serialize};
use serde_repr::Deserialize_repr;
use tracing::info;

use mqtt_broker::{
    auth::{AuthenticationContext, Authenticator},
    AuthId,
};

const API_VERSION: &str = "2020-04-20";

#[derive(Clone)]
pub struct EdgeHubAuthenticator {
    client: Client<HttpConnector>,
    url: String,
}

impl EdgeHubAuthenticator {
    pub fn new(url: String) -> Self {
        let client = Client::new();
        Self { client, url }
    }

    async fn authenticate(
        &self,
        context: &AuthenticationContext,
    ) -> Result<Option<AuthId>, AuthenticateError> {
        let auth_req = EdgeHubAuthRequest::from_auth(context);
        let body = serde_json::to_string(&auth_req).map_err(AuthenticateError::SerializeRequest)?;
        let req = Request::post(&self.url)
            .header(header::CONTENT_TYPE, "application/json; charset=utf-8")
            .body(Body::from(body))
            .map_err(AuthenticateError::PrepareRequest)?;

        let http_res = self
            .client
            .request(req)
            .await
            .map_err(AuthenticateError::SendRequest)?;

        if http_res.status() != StatusCode::OK {
            return Err(AuthenticateError::UnsuccessfulResponse(http_res.status()));
        }

        let body = body::aggregate(http_res)
            .await
            .map_err(AuthenticateError::ReadResponse)?;
        let auth_res: EdgeHubAuthResponse = serde_json::from_reader(body.reader())
            .map_err(AuthenticateError::DeserializeResponse)?;

        if auth_res.version != API_VERSION {
            return Err(AuthenticateError::ApiVersion(auth_res.version));
        }

        auth_res.try_into()
    }
}

#[async_trait]
impl Authenticator for EdgeHubAuthenticator {
    type Error = AuthenticateError;

    async fn authenticate(
        &self,
        context: AuthenticationContext,
    ) -> Result<Option<AuthId>, Self::Error> {
        let authenticate = || async {
            info!("authenticate client");
            self.authenticate(&context).await.map_err(|e| match e {
                error @ AuthenticateError::SendRequest(_)
                | error @ AuthenticateError::UnsuccessfulResponse(_) => Error::Transient(error),
                error => Error::Permanent(error),
            })
        };

        // try to authenticate a client and give up after 1min of trying.
        // it starts with 500ms interval and exponentially increases timeout.
        // it will make 10 attempts during 1min interval.
        let auth_id = authenticate
            .retry(ExponentialBackoff {
                max_elapsed_time: Some(Duration::from_secs(60)),
                ..ExponentialBackoff::default()
            })
            .await?;

        Ok(auth_id)
    }
}

#[derive(Serialize)]
#[serde(rename_all = "snake_case")]
pub struct EdgeHubAuthRequest<'a> {
    version: &'a str,
    username: Option<&'a str>,
    password: Option<&'a str>,
    certificate: Option<&'a str>,
    certificate_chain: Option<Vec<&'a str>>,
}

impl<'a> EdgeHubAuthRequest<'a> {
    fn from_auth(context: &'a AuthenticationContext) -> Self {
        Self {
            version: API_VERSION,
            username: context.username(),
            password: context.password(),
            certificate: context.certificate().map(|cert| cert.as_ref()),
            certificate_chain: context
                .cert_chain()
                .map(|chain| chain.iter().map(|cert| cert.as_ref()).collect()),
        }
    }
}

#[derive(Debug, Copy, Clone, Deserialize_repr)]
#[repr(i32)]
pub enum EdgeHubResultCode {
    Authenticated = 200,
    Unauthenticated = 403,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "snake_case")]
pub struct EdgeHubAuthResponse {
    version: String,
    result: EdgeHubResultCode,
    identity: Option<String>,
}

impl TryFrom<EdgeHubAuthResponse> for Option<AuthId> {
    type Error = AuthenticateError;

    fn try_from(value: EdgeHubAuthResponse) -> Result<Self, Self::Error> {
        match (value.result, value.identity) {
            (EdgeHubResultCode::Authenticated, Some(identity)) => Ok(Some(identity.into())),
            (EdgeHubResultCode::Authenticated, None) => Err(AuthenticateError::InvalidResponse),
            (EdgeHubResultCode::Unauthenticated, _) => Ok(None),
        }
    }
}

/// Authentication error.
#[derive(Debug, thiserror::Error)]
pub enum AuthenticateError {
    #[error("failed to serialize request.")]
    SerializeRequest(#[source] serde_json::Error),

    #[error("failed to prepare request.")]
    PrepareRequest(#[source] http::Error),

    #[error("failed to send request.")]
    SendRequest(#[source] hyper::Error),

    #[error("received unsuccessful status code: {0}")]
    UnsuccessfulResponse(http::StatusCode),

    #[error("failed to process response.")]
    ReadResponse(#[source] hyper::Error),

    #[error("failed to deserialize response.")]
    DeserializeResponse(#[source] serde_json::Error),

    #[error("not supported response version.")]
    ApiVersion(String),

    #[error("not supported response body.")]
    InvalidResponse,
}

#[cfg(test)]
mod tests {
    use std::{net::SocketAddr, time::Duration};

    use matches::assert_matches;
    use mockito::{mock, Matcher};
    use tokio::{sync::oneshot, time};

    use mqtt_broker::auth::{AuthenticationContext, Authenticator, Certificate};

    use super::{AuthenticateError, EdgeHubAuthenticator};

    const CERT: &str = "-----BEGIN CERTIFICATE-----
MIIEbjCCAlagAwIBAgIEdLDcUTANBgkqhkiG9w0BAQsFADAfMR0wGwYDVQQDDBRp
b3RlZGdlZCB3b3JrbG9hZCBjYTAeFw0yMDA3MDkyMTI5NTNaFw0yMDEwMDcyMDEw
MDJaMBQxEjAQBgNVBAMMCWxvY2FsaG9zdDCCASIwDQYJKoZIhvcNAQEBBQADggEP
ADCCAQoCggEBAME8iPiXdDF1fS3Ppq53HOD01BQqkDlhv5+8Lxwrz8Hz+K9dM7q4
xlXjywYeY/f6W7vL4vjxUbBSw3e0L7+X+UShwco8vwbiQqjbNfjAz95rlRwcrfff
xl04+GEcy7Uahrv2143s32CIPtYKEgUH0HVdRxBh6KrwWjCQuUfsysoxHsM1KqPI
5p4Gpp87Y4uRkX248IriJz3ap2+LWuAgV54VzuzAMx0SH9Mbgv0/g2k18PfoqJM5
mCktU88brojoOx6SGOu/kXpT3KmWXmckVMKakjqFERa9GXTe+jggFUS7uVIKY2dz
cV2fSnX9CiMRvXrtUKHrIg1qH2SPUsRYtgMCAwEAAaOBvDCBuTAJBgNVHRMEAjAA
MA4GA1UdDwEB/wQEAwID+DATBgNVHSUEDDAKBggrBgEFBQcDATAdBgNVHREEFjAU
gglsb2NhbGhvc3SCB2VkZ2VodWIwHQYDVR0OBBYEFNiOqy/sZzR6MHKk6pYU0SlR
z7TGMEkGA1UdIwRCMECAFAbOyQy9xAl46d39FhI0dQXohbRPoSKkIDAeMRwwGgYD
VQQDDBNUZXN0IEVkZ2UgRGV2aWNlIENBggRri0VnMA0GCSqGSIb3DQEBCwUAA4IC
AQBGZvInLdwQ9mKMcwM2E6kjIWuCcBOb1HXEswyDc8IKmtJS504s6Ybir/+Pb30m
rfeWfoMWMP8GS4UeIm9T0zzYFBuDcwqmsQxLZLSigUMmXryEwt6zp1ksZSIEIkFi
mKNFLuJSzPmLFFACsNQwsgl3qG2qqaMhOrRDEl/OH57tCFbLFVnSLWwB3XX4CsF3
vN/3Ys+Bf4Y1gtY6gctByI6NCimQQYEaC1BygSUh/nwyjlAy1H8Vu+8+TymJ0KHK
eee+y/9OkCxUqPHDHmE6JKVefkNwqbb6w+Sl9MQZXRVepNfuTzVF3iTyKu4SARPE
w19SRlNEfKM+W9U/T0shv3ay0W+3dry/5eY5nX6nuKx2Tt56iC5bjhCpUmsKuWoU
XGE7z48ZhG2qwPIlNIbTzKFvXL4AXGEhoCot7xPwohTwPUxuDAYGibAB9BKjm3/0
NgAPXqT82xpwX//mtRAoLFpSGct3E62KiLZD+RJnoC5A2X7KnQKnQndmEHwKGotS
GJ1GZwU99C+kuG9MD+aNZJZBozcdoRZKT56438J25pOemjTy4MjFs+t3nWe4jtK8
/gKeDoonQcvGbHR6+ukI+BDgyQwe+jvulA5ESanERONm42bnmZUuXxp2pZYKiB6q
ov2gTgQyaRE8rbX4SSPZghE5km7p6FAIjm/uqU9kGMUk3A==
-----END CERTIFICATE-----";

    #[tokio::test]
    async fn response_ok_feeds_identity() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(
                r#"{"result": 200, "identity":"somehub/somedevice","version": "2020-04-20"}"#,
            )
            .create();

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_password("qwerty123");

        let authenticator = authenticator();
        let result = authenticator.authenticate(&context).await.unwrap().unwrap();

        assert_eq!(result, "somehub/somedevice".into());
    }

    #[tokio::test]
    async fn response_ok_requires_identity() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(r#"{"result": 200,"version": "2020-04-20"}"#)
            .create();

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_password("qwerty123");

        let authenticator = authenticator();
        let result = authenticator.authenticate(&context).await;

        assert!(result.is_err());
    }

    #[tokio::test]
    async fn response_unauth_returns_none() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(r#"{"result": 403, "version": "2020-04-20"}"#)
            .create();

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_password("qwerty123");

        let authenticator = authenticator();
        let result = authenticator.authenticate(&context).await.unwrap();

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

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_password("qwerty123");

        let authenticator = authenticator();
        let result = authenticator.authenticate(&context).await;

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

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_password("qwerty123");

        let authenticator = authenticator();
        let result = authenticator.authenticate(&context).await;

        assert!(result.is_err());
    }

    #[tokio::test]
    async fn no_result_code_results_error() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .with_body(r#"{"identity":"somehub/somedevice","version": "2222-22-22"}"#)
            .create();

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_password("qwerty123");

        let authenticator = authenticator();
        let result = authenticator.authenticate(&context).await;

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

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_password("qwerty123");

        let authenticator = authenticator();
        let result = authenticator.authenticate(&context).await;

        assert!(result.is_ok());
    }

    #[tokio::test]
    async fn cert_cred_sends_cert() {
        let _m = mock("POST", "/authenticate/")
            .with_status(200)
            .match_body(Matcher::Regex(
                "\"certificate\":\"-----BEGIN CERTIFICATE-----".to_string(),
            ))
            .with_body(
                r#"{"result": 200, "identity":"somehub/somedevice","version": "2020-04-20"}"#,
            )
            .create();

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_certificate(Certificate::from(CERT.to_string()));

        let authenticator = authenticator();
        let result = authenticator.authenticate(&context).await;

        assert_matches!(result, Ok(_));
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

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_password("qwerty123");

        let authenticator = authenticator();
        let result = authenticator.authenticate(&context).await;

        assert!(result.is_ok());
    }

    #[tokio::test]
    async fn it_retries_when_edgehub_unavailable_for_some_time() {
        let (tx, rx) = oneshot::channel();

        // create mock http server with unused endpoint just to prevent
        // other tests to make endpoint which could override the current one
        let _mock = mock("POST", "/unused").create();

        let handle = tokio::spawn(async {
            // emulate edgehub startup delay 1s
            // it will make authenticator make 1 or 2 attempts to get response
            time::delay_for(Duration::from_secs(1)).await;

            let _m = mock("POST", "/authenticate/")
                .with_status(200)
                .with_body(
                    r#"{"result": 200, "identity":"somehub/somedevice","version": "2020-04-20"}"#,
                )
                .create();

            // wait until test finishes
            rx.await.unwrap();
        });

        let mut context = AuthenticationContext::new("client_1".into(), peer_addr());
        context.with_username("somehub/somedevice/api-version=2018-06-30");
        context.with_password("qwerty123");

        let authenticator: &dyn Authenticator<Error = AuthenticateError> = &authenticator();
        let result = authenticator.authenticate(context).await.unwrap().unwrap();

        tx.send(()).unwrap();
        handle.await.unwrap();

        assert_eq!(result, "somehub/somedevice".into());
    }

    fn authenticator() -> EdgeHubAuthenticator {
        EdgeHubAuthenticator::new(mockito::server_url() + "/authenticate/")
    }

    fn peer_addr() -> SocketAddr {
        "127.0.0.1:12345".parse().unwrap()
    }
}
