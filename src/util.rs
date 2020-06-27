use crate::constants::{ENCODE_CHARS, HSM_SERVER};

use std::io::{Error, ErrorKind};

use bytes::buf::BufExt;
use hyper::{Body, Client, Method, Request, Response, Uri};
use hyper::body::to_bytes;
use percent_encoding::percent_encode;
use serde::{Deserialize, Serialize};
use serde_json::{to_string, Deserializer};

pub type BoxedResult<'a, T> = Result<T, Box<dyn std::error::Error + 'a>>;

pub async fn call(method: Method, resource: &str, payload: Option<impl Serialize>) -> BoxedResult<'_, Response<Body>> {
    let client = Client::new();
    let uri = format!("{}{}", HSM_SERVER, percent_encode(resource.as_bytes(), ENCODE_CHARS));

    let req = Request::builder()
        .uri(uri.parse::<Uri>()?)
        .method(method)
        .body(match payload {
            Some(v) => Body::from(to_string(&v).unwrap()),
            None => Body::empty()
        })?;

    Ok(client.request(req).await?)
}

pub async fn slurp_json<'a, 'de, T: Deserialize<'de>>(res: Response<Body>) -> BoxedResult<'a, T> {
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
