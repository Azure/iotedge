mod connect;
mod workload;

pub use connect::Connector;
pub use workload::*;

use std::{convert::TryInto, error::Error as StdError, fmt::Display};

use http::{uri::InvalidUri, Uri};
use hyper::{client::HttpConnector, Client};
use hyperlocal::UnixConnector;
use reqwest::Url;

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
    Ok(WorkloadClient::new(client, scheme))
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

pub(crate) enum Scheme {
    Unix(String),
    Http(String),
}

#[derive(Debug, thiserror::Error)]
pub enum ApiError {
    #[error("could not construct request URL")]
    ConstructRequestUrl(#[source] Box<dyn StdError + Send + Sync>),

    #[error("could not construct request")]
    ConstructRequest(#[source] http::Error),

    #[error("could not construct request")]
    ExecuteRequest(#[source] hyper::Error),

    #[error("response has status code {0}")]
    UnsuccessfulResponse(http::StatusCode),

    #[error("could not read response")]
    ReadResponse(#[source] hyper::Error),

    #[error("could not deserialize response")]
    ParseResponseBody(#[source] serde_json::Error),

    #[error("could not serialize request")]
    SerializeRequestBody(#[source] serde_json::Error),
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("could not parse URL: {0}. {1}")]
    ParseUrl(String, #[source] InvalidUri),

    #[error("unrecognized scheme {0}")]
    UnrecognizedUrlScheme(String),
}
