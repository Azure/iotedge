// Copyright (c) Microsoft. All rights reserved.

mod cert;
mod decrypt;
mod encrypt;
mod sign;
mod trust_bundle;

use edgelet_core::{
    Authenticator, CreateCertificate, Decrypt, Encrypt, GetTrustBundle, KeyStore, Module,
    ModuleRuntime, ModuleRuntimeErrorReason, Policy, WorkloadConfig,
};
use edgelet_http::authentication::Authentication;
use edgelet_http::authorization::Authorization;
use edgelet_http::route::*;
use edgelet_http::{router, Version};
use edgelet_http_mgmt::ListModules;
use failure::{Compat, Fail, ResultExt};
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Request};
use serde::Serialize;

use self::cert::{IdentityCertHandler, ServerCertHandler};
use self::decrypt::DecryptHandler;
use self::encrypt::EncryptHandler;
use self::sign::SignHandler;
use self::trust_bundle::TrustBundleHandler;
use crate::error::{Error, ErrorKind};

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
        M: ModuleRuntime + Authenticator<Request = Request<Body>> + Clone + Send + Sync + 'static,
        for<'r> &'r <M as ModuleRuntime>::Error: Into<ModuleRuntimeErrorReason>,
        <M::Module as Module>::Config: Serialize,
        M::Logs: Into<Body>,
        W: WorkloadConfig + Clone + Send + Sync + 'static,
        <M::AuthenticateFuture as Future>::Error: Fail,
    {
        let router = router!(
            get   Version2018_06_28 runtime Policy::Anonymous => "/modules" => ListModules::new(runtime.clone()),
            post  Version2018_06_28 runtime Policy::Caller =>    "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/sign"     => SignHandler::new(key_store.clone()),
            post  Version2018_06_28 runtime Policy::Caller =>    "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/decrypt"  => DecryptHandler::new(hsm.clone()),
            post  Version2018_06_28 runtime Policy::Caller =>    "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/encrypt"  => EncryptHandler::new(hsm.clone()),
            post  Version2018_06_28 runtime Policy::Caller =>    "/modules/(?P<name>[^/]+)/certificate/identity"            => IdentityCertHandler::new(hsm.clone(), config.clone()),
            post  Version2018_06_28 runtime Policy::Caller =>    "/modules/(?P<name>[^/]+)/genid/(?P<genid>[^/]+)/certificate/server" => ServerCertHandler::new(hsm.clone(), config),

            get   Version2018_06_28 runtime Policy::Anonymous => "/trust-bundle" => TrustBundleHandler::new(hsm),
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
