// Copyright (c) Microsoft. All rights reserved.

mod cert;
mod decrypt;
mod encrypt;
mod sign;
mod trust_bundle;

use std::io;

use edgelet_core::{CreateCertificate, Decrypt, Encrypt, GetTrustBundle, KeyStore};
use edgelet_http::route::*;
use http::{Request, Response};
use hyper::server::{NewService, Service};
use hyper::{Body, Error as HyperError};

use self::cert::{IdentityCertHandler, ServerCertHandler};
use self::decrypt::DecryptHandler;
use self::encrypt::EncryptHandler;
use self::sign::SignHandler;
use self::trust_bundle::TrustBundleHandler;

#[derive(Clone)]
pub struct WorkloadService {
    inner: RouterService<RegexRecognizer>,
}

impl WorkloadService {
    pub fn new<K, H>(key_store: &K, hsm: H) -> Result<Self, HyperError>
    where
        K: 'static + KeyStore + Clone,
        H: 'static + CreateCertificate + Decrypt + Encrypt + GetTrustBundle + Clone,
    {
        let router = router!(
            post   "/modules/(?P<name>[^/]+)/sign" => SignHandler::new(key_store.clone()),
            post   "/modules/(?P<name>[^/]+)/decrypt" => DecryptHandler::new(hsm.clone()),
            post   "/modules/(?P<name>[^/]+)/encrypt" => EncryptHandler::new(hsm.clone()),
            post   "/modules/(?P<name>[^/]+)/certificate/identity" => IdentityCertHandler,
            post   "/modules/(?P<name>[^/]+)/certificate/server" => ServerCertHandler::new(hsm.clone()),

            get    "/trust-bundle" => TrustBundleHandler::new(hsm),
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
