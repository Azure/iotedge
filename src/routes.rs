use crate::config::{Configuration, Principal};
use crate::error::{Error, ErrorKind};
use crate::store::{Store, StoreBackend};

use std::str::FromStr;
use std::sync::Arc;

use hyper::{Body, Method, Request, Response};
use hyper::body::to_bytes;
use hyper::service::{service_fn, Service};
use tokio::net::UnixStream;

#[inline]
async fn read_request(req: Request<Body>) -> Result<String, Error> {
    Ok(
        serde_json::from_str(
                &String::from_utf8(
                        to_bytes(req).await
                            .map_err(|_| ErrorKind::Hyper)?
                            .to_vec()
                    )
                    .map_err(|_| ErrorKind::CorruptData)?
            )
            .map_err(|_| ErrorKind::CorruptData)?
    )
}

// TODO: API versioning
// TODO: Swagger specification
pub(crate) async fn dispatch<'a, T: StoreBackend>(store: &'a Store<T>, req: Request<Body>) -> Result<Response<Body>, Error> {
    println!("{:?}", req.extensions().get::<tokio::net::unix::UCred>());
    let id = String::from_str(&req.uri().path()[1 ..]).unwrap();
    match req.method() {
        &Method::GET => {
            let value = store.get_secret(id).await?;

            Ok(Response::new(serde_json::to_string(&value).unwrap().into()))
        },
        &Method::PUT => {
            let body = read_request(req).await?;

            store.set_secret(id, body).await?;
            Ok(Response::builder().status(204).body(Body::empty()).unwrap())
        },
        &Method::POST => {
            let body = read_request(req).await?;

            store.pull_secret(id, body).await?;
            Ok(Response::builder().status(204).body(Body::empty()).unwrap())
        },
        &Method::PATCH => {
            Ok(Response::builder().status(204).body(Body::empty()).unwrap())
        },
        &Method::DELETE => {
            store.delete_secret(id).await?;

            Ok(Response::builder().status(204).body(Body::empty()).unwrap())
        },
        _ => Err(Error::from(ErrorKind::NotFound))
    }
}