use bytes::buf::BufExt;
use hyper::{Body, Client, Method, Request, Response, Uri};
use hyper::body::aggregate;
use serde::Serialize;
use serde::de::DeserializeOwned;
use serde_json::{from_reader, to_string};
// use zeroize::Zeroize;

pub type BoxedResult<T> = Result<T, Box<dyn std::error::Error>>;

pub async fn call(method: Method, uri: &str, payload: &impl Serialize) -> BoxedResult<Response<Body>> {
    let client = Client::new();

    let req = Request::builder()
        .uri(uri.parse::<Uri>()?)
        .method(method)
        .body(Body::from(to_string(payload)?))?;

    Ok(client.request(req).await?)
}

#[inline]
pub async fn slurp_json<T: DeserializeOwned>(res: Response<Body>) -> BoxedResult<T> {
    let body = aggregate(res).await?;
    Ok(from_reader(body.reader())?)
}
