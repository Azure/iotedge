use crate::constants::API_SURFACE;
use crate::store::*;
use crate::util::*;

use std::convert::Infallible;
use std::ops::Deref;
use std::sync::Arc;

use hyper::{Body, Method, Request, Response};
use regex::Regex;

async fn index<'a>(_store: &'a impl Store, _req: Request<Body>) -> BoxResult<'a, Response<Body>> {
    Ok(Response::builder()
        .body(API_SURFACE.to_string().into())?)
}

async fn get_secret<'a>(store: &'a impl Store, id: &'a str) -> BoxResult<'a, Response<Body>> {
    let secret = store.get_secret(id).await?;
    Ok(Response::builder()
        .body(secret.into())?)
}

pub async fn dispatch<'a>(store: Arc<impl StoreBackend>, req: Request<Body>) -> Result<Response<Body>, Infallible> {
    let store = store.deref();
    let secret_reg = Regex::new("/secret/(?P<id>[^/]*)").unwrap();
    let res = match (req.method(), req.uri().path()) {
        (&Method::GET, "/") => index(store, req).await,
        (&Method::GET, r) if secret_reg.is_match(r) => {
            let id = secret_reg.captures(r)
                .unwrap()
                .name("id")
                .unwrap()
                .as_str();
            get_secret(store, id).await
        },
        _ => Ok(Response::builder().status(404).body(Body::empty()).unwrap())
    };
    Ok(res.unwrap_or_else(|_| { Response::builder().status(500).body(Body::empty()).unwrap() }))
}