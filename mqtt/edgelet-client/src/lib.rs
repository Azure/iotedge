#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::must_use_candidate,
    clippy::missing_errors_doc
)]
mod connect;
mod workload;

pub use connect::Connector;
pub use workload::{
    CertificateResponse, IdentityCertificateRequest, ServerCertificateRequest, SignRequest,
    SignResponse, TrustBundleResponse, WorkloadClient, WorkloadError,
};

use std::error::Error as StdError;

use http::Uri;
use hyper::{client::HttpConnector, Client};
#[cfg(unix)]
use hyperlocal::UnixConnector;
use url::{ParseError, Url};

pub fn workload(url: &str) -> Result<WorkloadClient, Error> {
    let url = Url::parse(url).map_err(|e| Error::ParseUrl(url.to_string(), e))?;

    let (connector, scheme) = match url.scheme() {
        #[cfg(unix)]
        "unix" => (
            Connector::Unix(UnixConnector),
            Scheme::Unix(url.path().to_string()),
        ),
        "http" => (
            Connector::Http(HttpConnector::new()),
            Scheme::Http(url.to_string()),
        ),
        _ => return Err(Error::UnrecognizedUrlScheme(url.to_string())),
    };

    let client = Client::builder().build(connector);
    Ok(WorkloadClient::new(client, scheme))
}

fn make_hyper_uri(scheme: &Scheme, path: &str) -> Result<Uri, Box<dyn StdError + Send + Sync>> {
    match scheme {
        #[cfg(unix)]
        Scheme::Unix(base) => Ok(hyperlocal::Uri::new(base, path).into()),
        Scheme::Http(base) => {
            let base = Url::parse(base)?;
            let url = base.join(path)?;
            let url = url.as_str().parse()?;
            Ok(url)
        }
    }
}

#[derive(Debug)]
pub(crate) enum Scheme {
    #[cfg(unix)]
    Unix(String),
    Http(String),
}

#[derive(Debug, thiserror::Error)]
pub enum ApiError {
    #[error("could not construct URL")]
    ConstructRequestUrl(#[source] Box<dyn StdError + Send + Sync>),

    #[error("could not construct request")]
    ConstructRequest(#[source] http::Error),

    #[error("could not make HTTP request")]
    ExecuteRequest(#[source] hyper::Error),

    #[error("response has status code {0} and body {1}")]
    UnsuccessfulResponse(http::StatusCode, String),

    #[error("could not read response")]
    ReadResponse(#[source] Box<dyn StdError + Send + Sync>),

    #[error("could not deserialize response")]
    ParseResponseBody(#[source] serde_json::Error),

    #[error("could not serialize request")]
    SerializeRequestBody(#[source] serde_json::Error),
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("could not parse URL: {0}. {1}")]
    ParseUrl(String, #[source] ParseError),

    #[error("unrecognized scheme {0}")]
    UnrecognizedUrlScheme(String),
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;

    use super::workload;

    #[test]
    fn it_creates_workload_client_for_http() {
        let client = workload("http://127.0.0.1:8000");
        assert_matches!(client, Ok(_));
    }

    #[test]
    #[cfg(unix)]
    fn it_creates_workload_client_for_unix() {
        let client = workload("unix:///var/lib/iotedge/workload.sock");
        assert_matches!(client, Ok(_));
    }
}
