use crate::store::*;
use crate::util::*;

use std::convert::Infallible;
use std::ops::Deref;
use std::str::FromStr;
use std::sync::Arc;

use hyper::{Body, Method, Request, Response};
use hyper::body::to_bytes;

pub(crate) async fn dispatch<'a, T: StoreBackend>(store: Arc<Store<T>>, req: Request<Body>) -> Result<Response<Body>, Infallible> {
    let store = store.deref();
    let id = String::from_str(&req.uri().path()[1 ..]).unwrap();
    let res = match req.method() {
        &Method::GET => {
            store.get_secret(id).await
                .map(|secret| Response::builder().body(secret.into()).unwrap())
        },
        &Method::PUT => {
            let body = to_bytes(req).await
                .unwrap()
                .to_vec();
            match String::from_utf8(body) {
                Ok(val) => store.set_secret(id, val).await
                    .map(|_| Response::builder().body(Body::empty()).unwrap()),
                Err(e) => <BoxResult<'_, Response<Body>>>::Err(Box::new(e))
            }
        },
        &Method::PATCH => {
            let body = to_bytes(req).await
                .unwrap()
                .to_vec();
            match String::from_utf8(body) {
                Ok(val) => store.pull_secrets(val.lines().into_iter().collect()).await
                    .map(|_| Response::builder().body(Body::empty()).unwrap()),
                Err(e) => <BoxResult<'_, Response<Body>>>::Err(Box::new(e))
            }
        }
        _ => Ok(Response::builder().status(404).body(Body::empty()).unwrap())
    };
    Ok(res.unwrap_or_else(|e| Response::builder().status(500).body(format!("{:?}", e).into()).unwrap()))
}