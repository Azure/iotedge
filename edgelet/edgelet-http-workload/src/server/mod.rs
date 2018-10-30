// Copyright (c) Microsoft. All rights reserved.

mod cert;
mod decrypt;
mod encrypt;
mod sign;
mod trust_bundle;

use std::error::Error as StdError;

use edgelet_core::{
    CreateCertificate, Decrypt, Encrypt, Error as CoreError, GetTrustBundle, KeyStore, Module,
    ModuleRuntime, Policy,
};
use edgelet_http::authorization::Authorization;
use edgelet_http::route::*;
use edgelet_http_mgmt::ListModules;
use failure;
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Error as HyperError, Request, Response};
use serde::Serialize;

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
    // clippy bug: https://github.com/rust-lang-nursery/rust-clippy/issues/3220
    #[cfg_attr(feature = "cargo-clippy", allow(new_ret_no_self))]
    pub fn new<K, H, M>(
        key_store: &K,
        hsm: H,
        runtime: &M,
    ) -> impl Future<Item = Self, Error = failure::Error>
    where
        K: 'static + KeyStore + Clone + Send + Sync,
        H: 'static + CreateCertificate + Decrypt + Encrypt + GetTrustBundle + Clone + Send + Sync,
        M: 'static + ModuleRuntime + Clone + Send + Sync,
        M::Error: Into<CoreError>,
        <M::Module as Module>::Config: Serialize,
        <M::Module as Module>::Error: Into<CoreError>,
        M::Logs: Into<Body>,
    {
        let router = router!(
            get    "/modules" => Authorization::new(ListModules::new(runtime.clone()), Policy::Caller, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/sign" => Authorization::new(SignHandler::new(key_store.clone()), Policy::Caller, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/decrypt" => Authorization::new(DecryptHandler::new(hsm.clone()), Policy::Caller, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/encrypt" => Authorization::new(EncryptHandler::new(hsm.clone()), Policy::Caller, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/certificate/identity" => Authorization::new(IdentityCertHandler, Policy::Caller, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/certificate/server" => Authorization::new(ServerCertHandler::new(hsm.clone()), Policy::Caller, runtime.clone()),

            get    "/trust-bundle" => Authorization::new(TrustBundleHandler::new(hsm), Policy::Anonymous, runtime.clone()),
        );

        router
            .new_service()
            .map(|inner| WorkloadService { inner })
            .map_err(failure::Error::from_boxed_compat)
    }
}

impl Service for WorkloadService {
    type ReqBody = Body;
    type ResBody = Body;
    type Error = HyperError;
    type Future = Box<Future<Item = Response<Self::ResBody>, Error = Self::Error> + Send>;

    fn call(&mut self, req: Request<Body>) -> Self::Future {
        self.inner.call(req)
    }
}

impl NewService for WorkloadService {
    type ReqBody = <Self::Service as Service>::ReqBody;
    type ResBody = <Self::Service as Service>::ResBody;
    type Error = <Self::Service as Service>::Error;
    type Service = Self;
    type Future = future::FutureResult<Self::Service, Self::InitError>;
    type InitError = Box<StdError + Send + Sync>;

    fn new_service(&self) -> Self::Future {
        future::ok(self.clone())
    }
}
