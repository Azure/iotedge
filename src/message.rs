use crate::constants::{API_SURFACE, LOST};
use crate::store::*;

use std::error::Error as StdError;
use std::sync::Arc;

use hyper::{Body, Method, Request, Response};
use hyper::http::Error as HttpError;
// use zeroize::Zeroize;

async fn index<T>(store: Store<T>, req: Request<Body>) -> Result<Response<Body>, HttpError>
    where
        T: StoreBackend,
        T::Error: StdError
{
    Response::builder()
        .body(API_SURFACE.to_string().into())
}

async fn get_secret<T>(store: Store<T>, req: Request<Body>) -> Result<Response<Body>, HttpError>
    where
        T: StoreBackend,
        T::Error: StdError
{
    Response::builder()
        .body("FOO".into())
}

pub async fn dispatch<T>(backend: Arc<T>, req: Request<Body>) -> Result<Response<Body>, HttpError>
    where
        T: StoreBackend,
        T::Error: StdError
{
    let store = Store(backend);
    match (req.method(), req.uri().path()) {
        (&Method::GET, "/") => index(store, req).await,
        (&Method::GET, "/secrets") => get_secret(store, req).await,
        _ => Response::builder().status(404).body(LOST.into())
    }
}