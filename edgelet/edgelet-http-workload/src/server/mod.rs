// Copyright (c) Microsoft. All rights reserved.

mod cert;
mod decrypt;
mod encrypt;
mod sign;
mod trust_bundle;

use edgelet_core::{
    CreateCertificate, Decrypt, Encrypt, GetTrustBundle, KeyStore, Module, ModuleRuntime, Policy,
    WorkloadConfig,
};
use edgelet_http::authorization::Authorization;
use edgelet_http::route::*;
use edgelet_http_mgmt::ListModules;
use failure::{Compat, ResultExt};
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Request};
use serde::Serialize;

use self::cert::{IdentityCertHandler, ServerCertHandler};
use self::decrypt::DecryptHandler;
use self::encrypt::EncryptHandler;
use self::sign::SignHandler;
use self::trust_bundle::TrustBundleHandler;
use edgelet_http::Version;
use error::{Error, ErrorKind};

#[derive(Clone)]
pub struct WorkloadService {
    inner: RouterService<RegexRecognizer>,
}

impl WorkloadService {
    pub fn new<K, H, M, W>(
        key_store: &K,
        hsm: H,
        runtime: &M,
        config: W,
    ) -> impl Future<Item = Self, Error = Error>
    where
        K: KeyStore + Clone + Send + Sync + 'static,
        H: CreateCertificate + Decrypt + Encrypt + GetTrustBundle + Clone + Send + Sync + 'static,
        M: ModuleRuntime + Clone + Send + Sync + 'static,
        <M::Module as Module>::Config: Serialize,
        M::Logs: Into<Body>,
        W: WorkloadConfig + Clone + Send + Sync + 'static,
    {
        let router = router!(
            get   Version2018_06_28,   "/modules" => Authorization::new(ListModules::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
            post  Version2018_06_28,   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/sign" => Authorization::new(SignHandler::new(key_store.clone()), Policy::Caller, runtime.clone()),
            post  Version2018_06_28,   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/decrypt" => Authorization::new(DecryptHandler::new(hsm.clone()), Policy::Caller, runtime.clone()),
            post  Version2018_06_28,   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/encrypt" => Authorization::new(EncryptHandler::new(hsm.clone()), Policy::Caller, runtime.clone()),
            post  Version2018_06_28,   "/modules/(?P<name>[^/]+)/certificate/identity" => Authorization::new(IdentityCertHandler::new(hsm.clone(), config.clone()), Policy::Caller, runtime.clone()),
            post  Version2018_06_28,   "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/certificate/server" => Authorization::new(ServerCertHandler::new(hsm.clone(), config), Policy::Caller, runtime.clone()),

            get   Version2018_06_28,   "/trust-bundle" => Authorization::new(TrustBundleHandler::new(hsm), Policy::Anonymous, runtime.clone()),
        );

        router.new_service().then(|inner| {
            let inner = inner.context(ErrorKind::StartService)?;
            Ok(WorkloadService { inner })
        })
    }
}

impl Service for WorkloadService {
    type ReqBody = <RouterService<RegexRecognizer> as Service>::ReqBody;
    type ResBody = <RouterService<RegexRecognizer> as Service>::ResBody;
    type Error = <RouterService<RegexRecognizer> as Service>::Error;
    type Future = <RouterService<RegexRecognizer> as Service>::Future;

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
    type InitError = Compat<Error>;

    fn new_service(&self) -> Self::Future {
        future::ok(self.clone())
    }
}
