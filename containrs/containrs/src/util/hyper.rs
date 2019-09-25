use async_trait::async_trait;
use failure::ResultExt;
use hyper::{Body, Response};
use log::*;
use serde::de::DeserializeOwned;

use crate::error::*;

/// Utility methods for working with [hyper::Body]
#[async_trait]
pub(crate) trait BodyExt {
    /// Return Body as a Vec<u8>
    async fn bytes(mut self) -> hyper::Result<Vec<u8>>;
    /// Deserialize body from JSON
    async fn json<T: DeserializeOwned>(mut self) -> Result<T>;
}

#[async_trait]
impl BodyExt for Body {
    /// Read body into Vec<u8>
    async fn bytes(mut self) -> hyper::Result<Vec<u8>> {
        let mut data: Vec<u8> = Vec::new();
        while let Some(next) = self.next().await {
            data.extend(next?.as_ref());
        }
        Ok(data)
    }

    /// Deserialize body as JSON
    async fn json<T: DeserializeOwned>(mut self) -> Result<T> {
        let slice = self.bytes().await.context(ErrorKind::UtilHyperBodyJSON)?;
        let val = serde_json::from_slice::<T>(&slice);
        match val {
            Ok(val) => Ok(val),
            Err(e) => {
                debug!("Malformed JSON: {}", String::from_utf8_lossy(&slice));
                Err(e).context(ErrorKind::UtilHyperBodyJSON)?
            }
        }
    }
}

#[async_trait]
pub(crate) trait ResponseExt {
    /// Finish downloading the response body, and dump it to log::debug!
    /// If the body could not be downloaded, an empty String is returned.
    async fn dump_to_debug(mut self);
}

#[async_trait]
impl<T: Send + BodyExt> ResponseExt for Response<T> {
    async fn dump_to_debug(mut self) {
        let (parts, body) = self.into_parts();
        let body = body.bytes().await.unwrap_or_default();
        let res = Response::from_parts(parts, String::from_utf8_lossy(&body).to_string());
        debug!("Contents of unexpected request: {:#?}", res);
    }
}
