use crate::store::*;
use crate::util::*;

use std::str::FromStr;

use hyper::{Body, Method, Request, Response};
use hyper::body::to_bytes;

#[inline]
async fn read_request<'a>(req: Request<Body>) -> BoxResult<'a, String> {
    Ok(
        serde_json::from_str(
            &String::from_utf8(
                to_bytes(req).await?
                    .to_vec()
            )?
        )?
    )
}

// TODO: API versioning
// TODO: Swagger specification
pub(crate) async fn dispatch<'a, T: StoreBackend>(store: &'a Store<T>, req: Request<Body>) -> BoxResult<'a, Response<Body>> {
    let id = String::from_str(&req.uri().path()[1 ..]).unwrap();
    match req.method() {
        // TODO: add logic to 404 instead of 500 if resource not found
        &Method::GET => {
            Ok(Response::new(store.get_secret(id).await?.into()))
        },
        &Method::PUT => {
            let body = read_request(req).await?;

            store.set_secret(id, body).await?;
            Ok(Response::new(Body::empty()))
        },
        &Method::PATCH => {
            let body = read_request(req).await?;
            let lines = body.lines()
                .into_iter()
                .collect();

            store.pull_secrets(lines).await?;
            Ok(Response::new(Body::empty()))
        }
        _ => Ok(Response::builder().status(404).body(Body::empty())?)
    }
}