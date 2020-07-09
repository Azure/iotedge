use std::{
    convert::TryInto,
    error::Error as StdError,
    fmt::Display,
    mem::MaybeUninit,
    pin::Pin,
    task::{Context, Poll},
};

use bytes::{buf::BufExt, Buf, BufMut};
use chrono::{DateTime, Utc};
use futures_util::{future::BoxFuture, FutureExt};
use http::{uri::InvalidUri, Request, StatusCode, Uri};
use hyper::{
    body,
    client::{connect::Connection, HttpConnector},
    Body, Client,
};
use hyperlocal::UnixConnector;
use serde::{Deserialize, Serialize};
use tokio::io::{AsyncRead, AsyncWrite};
use tower_service::Service;
use url::Url;

pub fn workload<U>(uri: &U) -> Result<WorkloadClient, Error>
where
    U: TryInto<Uri, Error = InvalidUri> + Display + Clone,
{
    let uri = uri
        .clone()
        .try_into()
        .map_err(|e| Error::ParseUrl(uri.to_string(), e))?;

    let (connector, scheme) = match uri.scheme_str() {
        Some("unix") => (
            Connector::Unix(UnixConnector),
            Scheme::Unix(uri.path().to_string()),
        ),
        Some("http") => (
            Connector::Http(HttpConnector::new()),
            Scheme::Http(uri.to_string()),
        ),
        _ => return Err(Error::UnrecognizedUrlScheme(uri.to_string())),
    };

    let client = Client::builder().build(connector);
    Ok(WorkloadClient { client, scheme })
}

pub struct WorkloadClient {
    client: Client<Connector, Body>,
    scheme: Scheme,
}

impl WorkloadClient {
    pub async fn create_server_cert(
        &self,
        module_id: &str,
        generation_id: &str,
        hostname: &str,
        expiration: DateTime<Utc>,
    ) -> Result<CertificateResponse, WorkloadError> {
        let path = format!(
            "/modules/{}/genid/{}/certificate/server?api-version=2019-01-30",
            module_id, generation_id
        );
        let uri = make_hyper_uri(&self.scheme, &path).map_err(|e| ApiError::ConstructRequest(e))?;

        let req = ServerCertificateRequest {
            common_name: hostname.to_string(),
            expiration: expiration.to_rfc3339(),
        };
        let body = serde_json::to_string(&req).map_err(|e| ApiError::ConstructRequest(e.into()))?;
        let req = Request::post(uri)
            .body(Body::from(body))
            .map_err(|e| ApiError::ConstructRequest(e.into()))?;

        let res = self
            .client
            .request(req)
            .await
            .map_err(ApiError::ExecuteRequest)?;

        if res.status() != StatusCode::OK {
            return Err(ApiError::UnsuccessfulResponse(res.status()).into());
        }

        let body = body::aggregate(res).await.map_err(ApiError::ReadResponse)?;

        let cert = serde_json::from_reader(body.reader())
            .map_err(|e| ApiError::ParseResponseBody(e.into()))?;

        Ok(cert)
    }
}

#[derive(Debug, thiserror::Error)]
pub enum WorkloadError {
    #[error("could not make workload API call")]
    Api(#[from] ApiError),
}

#[derive(Debug, thiserror::Error)]
pub enum ApiError {
    #[error("could not construct request URL")]
    ConstructRequestUrl(#[source] Box<dyn StdError>),

    #[error("could not construct request")]
    ConstructRequest(#[source] Box<dyn StdError>),

    #[error("could not construct request")]
    ExecuteRequest(#[source] hyper::Error),

    #[error("response has status code {0}")]
    UnsuccessfulResponse(http::StatusCode),

    #[error("could not read response")]
    ReadResponse(#[source] hyper::Error),

    #[error("could not deserialize response")]
    ParseResponseBody(Box<dyn StdError>),

    #[error("could not serialize request")]
    SerializeRequestBody(#[source] serde_json::Error),
}

fn make_hyper_uri(scheme: &Scheme, path: &str) -> Result<Uri, Box<dyn StdError + Send + Sync>> {
    match scheme {
        Scheme::Unix(base) => Ok(hyperlocal::Uri::new(base, path).into()),
        Scheme::Http(base) => {
            let base = Url::parse(base)?;
            let url = base.join(path)?;
            let url = url.as_str().parse()?;
            Ok(url)
        }
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub struct ServerCertificateRequest {
    /// Subject common name
    #[serde(rename = "commonName")]
    common_name: String,

    /// Certificate expiration date-time (ISO 8601)
    #[serde(rename = "expiration")]
    expiration: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct CertificateResponse {
    #[serde(rename = "privateKey")]
    pub private_key: PrivateKey,

    /// Base64 encoded PEM formatted byte array containing the certificate and its chain.
    #[serde(rename = "certificate")]
    pub certificate: String,

    /// Certificate expiration date-time (ISO 8601)
    #[serde(rename = "expiration")]
    pub expiration: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct PrivateKey {
    /// Indicates format of the key (present in PEM formatted bytes or a reference)
    #[serde(rename = "type")]
    type_: String,

    /// Reference to private key.
    #[serde(rename = "ref", skip_serializing_if = "Option::is_none")]
    ref_: Option<String>,

    /// Base64 encoded PEM formatted byte array
    #[serde(rename = "bytes", skip_serializing_if = "Option::is_none")]
    bytes: Option<String>,
}

enum Scheme {
    Unix(String),
    Http(String),
}

#[derive(Debug, Clone)]
pub enum Connector {
    Unix(UnixConnector),
    Http(HttpConnector),
}

impl Service<Uri> for Connector {
    type Response = Stream;
    type Error = Box<dyn StdError + Send + Sync + 'static>;
    type Future = BoxFuture<'static, Result<Self::Response, Self::Error>>;

    fn call(&mut self, req: Uri) -> Self::Future {
        match self {
            Connector::Unix(connector) => {
                let fut = connector
                    .call(req)
                    .map(|stream| stream.map(Stream::Unix).map_err(|e| Box::new(e).into()));
                Box::pin(fut)
            }
            Connector::Http(connector) => {
                let fut = connector
                    .call(req)
                    .map(|stream| stream.map(Stream::Http).map_err(|e| Box::new(e).into()));
                Box::pin(fut)
            }
        }
    }

    fn poll_ready(&mut self, cx: &mut Context<'_>) -> Poll<Result<(), Self::Error>> {
        match self {
            Connector::Unix(connector) => connector.poll_ready(cx).map_err(|e| Box::new(e).into()),
            Connector::Http(connector) => connector.poll_ready(cx).map_err(|e| Box::new(e).into()),
        }
    }
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("could not parse URL: {0}. {1}")]
    ParseUrl(String, #[source] InvalidUri),

    #[error("unrecognized scheme {0}")]
    UnrecognizedUrlScheme(String),

    #[error("could not construct request")]
    ConstructRequest(Box<dyn StdError>),
}

pub enum Stream {
    Unix(<UnixConnector as Service<Uri>>::Response),
    Http(<HttpConnector as Service<Uri>>::Response),
}

impl AsyncRead for Stream {
    #[inline]
    unsafe fn prepare_uninitialized_buffer(&self, buf: &mut [MaybeUninit<u8>]) -> bool {
        match self {
            Self::Unix(stream) => stream.prepare_uninitialized_buffer(buf),
            Self::Http(stream) => stream.prepare_uninitialized_buffer(buf),
        }
    }

    #[inline]
    fn poll_read_buf<B: BufMut>(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut B,
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_read_buf(cx, buf),
            Self::Http(stream) => Pin::new(stream).poll_read_buf(cx, buf),
        }
    }

    fn poll_read(
        self: std::pin::Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut [u8],
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_read(cx, buf),
            Self::Http(stream) => Pin::new(stream).poll_read(cx, buf),
        }
    }
}

impl AsyncWrite for Stream {
    fn poll_write(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &[u8],
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_write(cx, buf),
            Self::Http(stream) => Pin::new(stream).poll_write(cx, buf),
        }
    }

    fn poll_write_buf<B: Buf>(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut B,
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_write_buf(cx, buf),
            Self::Http(stream) => Pin::new(stream).poll_write_buf(cx, buf),
        }
    }

    #[inline]
    fn poll_flush(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<std::io::Result<()>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_flush(cx),
            Self::Http(stream) => Pin::new(stream).poll_flush(cx),
        }
    }

    fn poll_shutdown(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<std::io::Result<()>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_shutdown(cx),
            Self::Http(stream) => Pin::new(stream).poll_shutdown(cx),
        }
    }
}

impl Connection for Stream {
    fn connected(&self) -> hyper::client::connect::Connected {
        match self {
            Stream::Unix(stream) => stream.connected(),
            Stream::Http(stream) => stream.connected(),
        }
    }
}

#[cfg(test)]
mod tests {
    use chrono::{Duration, Utc};
    use http::StatusCode;
    use mockito::mock;
    use serde_json::json;

    use super::{make_hyper_uri, workload, ApiError, Scheme, WorkloadError};
    use matches::assert_matches;

    #[test]
    fn it_makes_hyper_uri() {
        let scheme = Scheme::Unix("unix:///var/iotedge/workload.sock".into());
        let path = "/modules/$edgeHub/genid/12345678/certificate/server?api-version=2019-01-30";

        let uri = make_hyper_uri(&scheme, &path).unwrap();
        assert!(uri.to_string().ends_with(path));
    }

    #[tokio::test]
    async fn it_downloads_server_certificate() {
        let expiration = Utc::now() + Duration::days(90);
        let res = json!(
            {
                "privateKey": { "type": "key", "bytes": "PRIVATE KEY" },
                "certificate": "CERTIFICATE",
                "expiration": expiration.to_rfc3339()
            }
        );

        let _m = mock(
            "POST",
            "/modules/broker/genid/12345678/certificate/server?api-version=2019-01-30",
        )
        .with_status(200)
        .with_body(serde_json::to_string(&res).unwrap())
        .create();

        let client = workload(&mockito::server_url()).expect("client");
        let res = client
            .create_server_cert("broker", "12345678", "localhost", expiration)
            .await
            .unwrap();

        assert_eq!(res.private_key.bytes.as_deref(), Some("PRIVATE KEY"));
        assert_eq!(res.certificate, "CERTIFICATE");
        assert_eq!(res.expiration, expiration.to_rfc3339());
    }

    #[tokio::test]
    async fn it_handles_incorrect_status_for_create_server_cert() {
        let expiration = Utc::now() + Duration::days(90);
        let _m = mock(
            "POST",
            "/modules/broker/genid/12345678/certificate/server?api-version=2019-01-30",
        )
        .with_status(400)
        .with_body(r#"{"message":"Something went wrong"}"#)
        .create();

        let client = workload(&mockito::server_url()).expect("client");
        let res = client
            .create_server_cert("broker", "12345678", "locahost", expiration)
            .await
            .unwrap_err();

        assert_matches!(
            res,
            WorkloadError::Api(ApiError::UnsuccessfulResponse(StatusCode::BAD_REQUEST))
        )
    }
}
