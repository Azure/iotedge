use std::io::{Error, ErrorKind};

use bytes::buf::BufExt;
use hyper::{Body, Client, Method, Request, Response, Uri};
use hyper::body::aggregate;
use serde::{Deserialize, Serialize};
use serde_json::{to_string, Deserializer, Value};
// use zeroize::Zeroize;

pub type BoxedResult<T> = Result<T, Box<dyn std::error::Error>>;

pub async fn call(method: Method, uri: String, payload: Option<impl Serialize>) -> BoxedResult<Response<Body>> {
    let client = Client::new();

    let req = Request::builder()
        .uri(uri.parse::<Uri>()?)
        .method(method)
        .body(match payload {
            Some(v) => Body::from(to_string(&v).unwrap()),
            None => Body::empty()
        })?;

    Ok(client.request(req).await?)
}

pub async fn slurp_json<'de, T: Deserialize<'de>>(res: Response<Body>) -> BoxedResult<T> {
    let status = res.status();
    let body = aggregate(res).await?;

    if status.is_success() {
        let mut de = Deserializer::from_reader(body.reader());
        Ok(T::deserialize(&mut de)?)
    }
    else {
        Err(Box::new(Error::new(ErrorKind::Other, "FOO")))
    }
}
