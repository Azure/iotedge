// Copyright (c) Microsoft. All rights reserved.

mod identity;
mod module;
mod system_info;

use edgelet_core::{
    IdentityManager, Module, ModuleRuntime, Policy,
};
use edgelet_http::authorization::Authorization;
use edgelet_http::route::*;
use failure::{Compat, ResultExt};
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Request};
use serde::de::DeserializeOwned;
use serde::Serialize;

use error::{Error, ErrorKind};
use self::identity::*;
pub use self::module::*;
use self::system_info::*;

lazy_static! {
    static ref AGENT_NAME: String = "edgeAgent".to_string();
}

#[derive(Clone)]
pub struct ManagementService {
    inner: RouterService<RegexRecognizer>,
}

impl ManagementService {
    // clippy bug: https://github.com/rust-lang-nursery/rust-clippy/issues/3220
    #[cfg_attr(feature = "cargo-clippy", allow(new_ret_no_self))]
    pub fn new<M, I>(runtime: &M, identity: &I) -> impl Future<Item = Self, Error = Error>
    where
        M: 'static + ModuleRuntime + Clone + Send + Sync,
        <M::Module as Module>::Config: DeserializeOwned + Serialize,
        M::Logs: Into<Body>,
        I: 'static + IdentityManager + Clone + Send + Sync,
        I::Identity: Serialize,
    {
        let router = router!(
            get    "/modules"                         => Authorization::new(ListModules::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
            post   "/modules"                         => Authorization::new(CreateModule::new(runtime.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            get    "/modules/(?P<name>[^/]+)"         => Authorization::new(GetModule, Policy::Anonymous, runtime.clone()),
            put    "/modules/(?P<name>[^/]+)"         => Authorization::new(UpdateModule::new(runtime.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            delete "/modules/(?P<name>[^/]+)"         => Authorization::new(DeleteModule::new(runtime.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/start"   => Authorization::new(StartModule::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/stop"    => Authorization::new(StopModule::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/restart" => Authorization::new(RestartModule::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
            get    "/modules/(?P<name>[^/]+)/logs"    => Authorization::new(ModuleLogs::new(runtime.clone()), Policy::Anonymous, runtime.clone()),

            get    "/identities"                      => Authorization::new(ListIdentities::new(identity.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            post   "/identities"                      => Authorization::new(CreateIdentity::new(identity.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            put    "/identities/(?P<name>[^/]+)"      => Authorization::new(UpdateIdentity::new(identity.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            delete "/identities/(?P<name>[^/]+)"      => Authorization::new(DeleteIdentity::new(identity.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),

            get    "/systeminfo"                      => Authorization::new(GetSystemInfo::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
        );

        router
            .new_service()
            .then(|inner| {
                let inner = inner.context(ErrorKind::StartService)?;
                Ok(ManagementService { inner })
            })
    }
}

impl Service for ManagementService {
    type ReqBody = <RouterService<RegexRecognizer> as Service>::ReqBody;
    type ResBody = <RouterService<RegexRecognizer> as Service>::ResBody;
    type Error = <RouterService<RegexRecognizer> as Service>::Error;
    type Future = <RouterService<RegexRecognizer> as Service>::Future;

    fn call(&mut self, req: Request<Body>) -> Self::Future {
        self.inner.call(req)
    }
}

impl NewService for ManagementService {
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
