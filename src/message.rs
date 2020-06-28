use crate::constants::{API_SURFACE, LOST};
use crate::store::*;

use std::future::Future;
use std::pin::Pin;
use std::task::{Context, Poll};

use hyper::{Body, Method, Request, Response};
use hyper::http::Error as HttpError;
use hyper::service::Service;
// use zeroize::Zeroize;

type ServiceResponse = Pin<Box<dyn Future<Output = Result<Response<Body>, HttpError>> + Send + Sync>>;
pub(crate) struct MessageService<T>(Store<T>)
    where
        T: StoreBackend,
        T::Error: std::error::Error;

impl<T> MessageService<T>
    where
        T: StoreBackend,
        T::Error: std::error::Error
{
    pub fn new(backend: T) -> Self {
        MessageService(Store(backend))
    }

    fn index(&self, req: Request<Body>) -> ServiceResponse {
        Box::pin(async {
            Response::builder()
                .body(API_SURFACE.to_string().into())
        })
    }

    fn get_secret(&self, req: Request<Body>) -> ServiceResponse {
        Box::pin(async {
            Response::builder()
                .body("FOO".into())
        })
    }
}

impl<T> Service<Request<Body>> for MessageService<T>
    where
        T: StoreBackend,
        T::Error: std::error::Error
{
    type Response = Response<Body>;
    type Error = HttpError;
    type Future = ServiceResponse;

    fn poll_ready(&mut self, _: &mut Context) -> Poll<Result<(), Self::Error>> {
        // NOTE: could be used to communicate database locking status
        //       if one store instance is handling all requests
        Poll::Ready(Ok(()))
    }

    fn call(&mut self, req: Request<Body>) -> Self::Future {
        match (req.method(), req.uri().path()) {
            (&Method::GET, "/") => self.index(req),
            (&Method::GET, "/secret") => self.get_secret(req),
            _ => Box::pin(async { Response::builder().status(404).body(LOST.into()) })
        }
    }
}
