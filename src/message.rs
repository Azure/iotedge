use crate::constants::*;
use crate::util::*;
use crate::store;

use hyper::{Body, Method, Request, Response};
use hyper::http::Error as HttpError;

fn index(req: Request<Body>) -> Result<Response<Body>, HttpError> {
    Response::builder()
        .body(API_SURFACE.to_string().into())
}

fn get_secret(req: Request<Body>) -> Result<Response<Body>, HttpError> {
    Ok(Response::new(Body::empty()))
}

pub async fn service(req: Request<Body>) -> Result<Response<Body>, HttpError> {
    Ok(match (req.method(), req.uri().path()) {
        (&Method::GET, "/") => index(req)?,
        (&Method::GET, "/secret") => get_secret(req)?,
        _ => Response::builder().status(404).body(LOST.into())?
    })
}