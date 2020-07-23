// use std::future::Future;
use std::io::{Error, ErrorKind};
// use std::pin::Pin;

use bytes::buf::BufExt;
use hyper::{Body, Response};
use hyper::body::to_bytes;
use serde::de::DeserializeOwned;
use serde_json::Deserializer;

pub type BoxResult<'a, T> = Result<T, Box<dyn std::error::Error + 'a>>;
// pub type BoxFuture<'a, T> = Pin<Box<dyn Future<Output = BoxResult<'a, T>> + Send + 'a>>;

pub async fn slurp_json<'a, T: DeserializeOwned>(res: Response<Body>) -> BoxResult<'a, T> {
    let status = res.status();
    let body = to_bytes(res).await?;

    if status.is_success() {
        let mut de = Deserializer::from_reader(body.reader());
        Ok(T::deserialize(&mut de)?)
    }
    else {
        Err(Box::new(Error::new(ErrorKind::Other, String::from_utf8(body.to_vec())?)))
    }
}
