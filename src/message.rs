use crate::constants::{API_SURFACE, LOST};
use crate::store::*;

use std::ops::Deref;
use std::sync::Arc;

use hyper::{Body, Method, Request, Response};
use hyper::http::Error as HttpError;
// use zeroize::Zeroize;

async fn index(_store: &impl Store, _req: Request<Body>) -> Result<Response<Body>, HttpError> {
    Response::builder()
        .body(API_SURFACE.to_string().into())
}

async fn get_secret(store: &impl Store, req: Request<Body>) -> Result<Response<Body>, HttpError> {
    Response::builder()
        .body("FOO".into())
}

pub async fn dispatch<T: StoreBackend>(backend: Arc<T>, req: Request<Body>) -> Result<Response<Body>, HttpError> {
    match (req.method(), req.uri().path()) {
        (&Method::GET, "/") => index(backend.deref(), req).await,
        (&Method::GET, "/secrets") => get_secret(backend.deref(), req).await,
        _ => Response::builder().status(404).body(LOST.into())
    }
}