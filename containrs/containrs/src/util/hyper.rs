use async_trait::async_trait;
use hyper::Body;
use serde::de::DeserializeOwned;

use crate::Result;

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
        let slice = self.bytes().await?;
        let val = serde_json::from_slice::<T>(&slice);
        Ok(val?)
    }
}
