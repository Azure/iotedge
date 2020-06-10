use crate::error::{Error, ErrorKind};
use edgelet_core::{Authenticator, ModuleRuntime, Policy, WorkloadConfig};
use edgelet_http::authentication::Authentication;
use edgelet_http::authorization::Authorization;
use edgelet_http::route::{Builder, RegexRecognizer, Router, RouterService};
use edgelet_http::{router, Version};
use failure::{Compat, Fail, ResultExt};
use futures::{future, Future};
use hyper::service::{NewService, Service};

use self::sign::SignHandler;
use edgelet_core::crypto::Sign;
use hyper::{Body, Request, Response};

mod error;
mod sign;

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}

impl IntoResponse for Response<Body> {
    fn into_response(self) -> Response<Body> {
        self
    }
}

#[derive(Clone)]
pub struct KeyService {
    inner: RouterService<RegexRecognizer>,
}

impl KeyService {
    pub fn new<M, W, K>(runtime: &M, _config: W, key: K) -> impl Future<Item = Self, Error = Error>
    where
        K: Sign + Clone + Send + Sync + 'static,
        M: ModuleRuntime + Authenticator<Request = Request<Body>> + Clone + Send + Sync + 'static,
        W: WorkloadConfig + Clone + Send + Sync + 'static,
        <M::AuthenticateFuture as Future>::Error: Fail,
    {
        let router = router!(
            post   Version2020_06_01 runtime Policy::Anonymous => "/sign" => SignHandler::new(key),
        );

        router.new_service().then(|inner| {
            let inner = inner.context(ErrorKind::StartService)?;
            Ok(KeyService { inner })
        })
    }
}

impl Service for KeyService {
    type ReqBody = <RouterService<RegexRecognizer> as Service>::ReqBody;
    type ResBody = <RouterService<RegexRecognizer> as Service>::ResBody;
    type Error = <RouterService<RegexRecognizer> as Service>::Error;
    type Future = <RouterService<RegexRecognizer> as Service>::Future;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        self.inner.call(req)
    }
}

impl NewService for KeyService {
    type ReqBody = <Self::Service as Service>::ReqBody;
    type ResBody = <Self::Service as Service>::ResBody;
    type Error = <Self::Service as Service>::Error;
    type Service = Self;
    type Future = future::FutureResult<Self::Service, Self::InitError>;
    type InitError = Compat<Error>;

    fn new_service(&self) -> Self::Future {
        future::ok(self.clone())
    }
}
