// Copyright (c) Microsoft. All rights reserved.

use std::io;

use edgelet_core::{CreateCertificate, KeyStore};
use edgelet_http::route::*;
use http::{Request, Response};
use hyper::{Body, Error as HyperError};
use hyper::server::{NewService, Service};

use self::cert::{IdentityCertHandler, ServerCertHandler};
use self::sign::SignHandler;

mod cert;
mod sign;

#[derive(Clone)]
pub struct WorkloadService {
    inner: RouterService<RegexRecognizer>,
}

impl WorkloadService {
    pub fn new<K, H>(key_store: &K, hsm: H) -> Result<Self, HyperError>
    where
        K: 'static + KeyStore + Clone,
        H: 'static + CreateCertificate + Clone,
    {
        let router = router!(
            post   "/modules/(?P<name>[^/]+)/sign" => SignHandler::new(key_store.clone()),
            post   "/modules/(?P<name>[^/]+)/certificate/identity" => IdentityCertHandler,
            post   "/modules/(?P<name>[^/]+)/certificate/server" => ServerCertHandler::new(hsm),
        );
        let inner = router.new_service()?;
        let service = WorkloadService { inner };
        Ok(service)
    }
}

impl Service for WorkloadService {
    type Request = Request<Body>;
    type Response = Response<Body>;
    type Error = HyperError;
    type Future = BoxFuture<Self::Response, HyperError>;

    fn call(&self, req: Request<Body>) -> Self::Future {
        self.inner.call(req)
    }
}

impl NewService for WorkloadService {
    type Request = Request<Body>;
    type Response = Response<Body>;
    type Error = HyperError;
    type Instance = Self;

    fn new_service(&self) -> io::Result<Self::Instance> {
        Ok(self.clone())
    }
}
