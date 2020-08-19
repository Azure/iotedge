use crate::error;
use crate::store::*;
use crate::config::Principal;
use crate::util::*;

use std::future::Future;
use std::pin::Pin;
use std::str::FromStr;
use std::task::{Context, Poll};

use hyper::{Body, Method, Request, Response};
use hyper::body::to_bytes;
use hyper::http;
use hyper::service::Service;

#[inline]
async fn read_request(req: Request<Body>) -> Result<Body, hyper::Error> {
    Ok(
        serde_json::from_str(
            &String::from_utf8(
                to_bytes(req).await?
                    .to_vec()
            )?
        )?
    )
}

pub(crate) struct Server<'a, T: StoreBackend> {
    principals: &'a Vec<Principal>,
    store: &'a Store<T>
}

impl<'a, T: StoreBackend> Server<'a, T> {
    pub fn new(store: &'a Store<T>, principals: &'a Vec<Principal>) -> Self {
        Self {
            principals: principals,
            store: store
        }
    }
}

impl<'a, T: StoreBackend> Service<Request<Body>> for Server<'a, T> {
    type Response = Response<Body>;
    type Error = http::Error;
    type Future = Pin<Box<dyn Future<Output = Result<Self::Response, Self::Error>>>>;

    fn poll_ready(&mut self, cx: &mut Context<'_>) -> Poll<Result<(), Self::Error>> {
        Poll::Ready(Ok(()))
    }

    fn call(&mut self, req: Request<Body>) -> Self::Future {
        Box::pin(async {
            Ok(Response::new(Body::empty()))
        })
    }
}
