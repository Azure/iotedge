use crate::constants::{API_SURFACE, LOST};
use crate::store::Store;

use hyper::{Body, Method, Request, Response};
use hyper::http::Error as HttpError;
// use zeroize::Zeroize;

pub struct MessageService<'a>(Store<'a>);

async fn index(store: Store<'_>, req: Request<Body>) -> Result<Response<Body>, HttpError> {
    Response::builder()
        .body(API_SURFACE.to_string().into())
}

async fn get_secret(store: Store<'_>, req: Request<Body>) -> Result<Response<Body>, HttpError>  {
    Response::builder()
        .body("FOO".into())
}

pub async fn call(store: Store<'_>, req: Request<Body>) -> Result<Response<Body>, HttpError> {
    Ok(match (req.method(), req.uri().path()) {
        (&Method::GET, "/") => index(store, req).await?,
        (&Method::GET, "/secret") => get_secret(store, req).await?,
        _ => Response::builder().status(404).body(LOST.into())?
    })
}
