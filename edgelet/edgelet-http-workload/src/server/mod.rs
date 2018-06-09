// Copyright (c) Microsoft. All rights reserved.

mod cert;
mod decrypt;
mod encrypt;
mod sign;
mod trust_bundle;

use std::io;

use edgelet_core::{CreateCertificate, Decrypt, Encrypt, Error as CoreError, GetTrustBundle,
                   KeyStore, Module, ModuleRuntime, Policy};
use edgelet_http::authorization::Authorization;
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
    pub fn new<K, H, M>(key_store: &K, hsm: H, runtime: &M) -> Result<Self, HyperError>
    where
        K: 'static + KeyStore + Clone,
        H: 'static + CreateCertificate + Decrypt + Encrypt + GetTrustBundle + Clone,
        M: 'static + ModuleRuntime + Clone,
        M::Error: Into<CoreError>,
        <M::Module as Module>::Error: Into<CoreError>,
        M::Logs: Into<Body>,
    {
        let router = router!(
            post   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/sign" => Authorization::new(SignHandler::new(key_store.clone()), Policy::Caller, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/decrypt" => Authorization::new(DecryptHandler::new(hsm.clone()), Policy::Caller, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/encrypt" => Authorization::new(EncryptHandler::new(hsm.clone()), Policy::Caller, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/certificate/identity" => Authorization::new(IdentityCertHandler, Policy::Caller, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/certificate/server" => Authorization::new(ServerCertHandler::new(hsm.clone()), Policy::Caller, runtime.clone()),

            get    "/trust-bundle" => Authorization::new(TrustBundleHandler::new(hsm), Policy::Anonymous, runtime.clone()),
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
