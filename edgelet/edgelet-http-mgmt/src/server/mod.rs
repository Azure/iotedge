// Copyright (c) Microsoft. All rights reserved.

mod identity;
mod module;
mod system_info;

use edgelet_core::{IdentityManager, Module, ModuleRuntime, Policy};
use edgelet_http::authorization::Authorization;
use edgelet_http::route::*;
use failure::{Compat, ResultExt};
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Request};
use serde::de::DeserializeOwned;
use serde::Serialize;

use self::identity::*;
pub use self::module::*;
use self::system_info::*;
use edgelet_http::Version;
use error::{Error, ErrorKind};

lazy_static! {
    static ref AGENT_NAME: String = "edgeAgent".to_string();
}

#[derive(Clone)]
pub struct ManagementService {
    inner: RouterService<RegexRecognizer>,
}

impl ManagementService {
    pub fn new<M, I>(runtime: &M, identity: &I) -> impl Future<Item = Self, Error = Error>
    where
        M: 'static + ModuleRuntime + Clone + Send + Sync,
        <M::Module as Module>::Config: DeserializeOwned + Serialize,
        M::Logs: Into<Body>,
        I: 'static + IdentityManager + Clone + Send + Sync,
        I::Identity: Serialize,
    {
        let router = router!(
            get     Version2018_06_28,  "/modules"                         => Authorization::new(ListModules::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
            post    Version2018_06_28,  "/modules"                         => Authorization::new(CreateModule::new(runtime.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            get     Version2018_06_28,  "/modules/(?P<name>[^/]+)"         => Authorization::new(GetModule, Policy::Anonymous, runtime.clone()),
            put     Version2018_06_28,  "/modules/(?P<name>[^/]+)"         => Authorization::new(UpdateModule::new(runtime.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            post    Version2019_01_30,  "/modules/(?P<name>[^/]+)/prepareupdate"        => Authorization::new(PrepareUpdateModule::new(runtime.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            delete  Version2018_06_28,  "/modules/(?P<name>[^/]+)"         => Authorization::new(DeleteModule::new(runtime.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            post    Version2018_06_28,  "/modules/(?P<name>[^/]+)/start"   => Authorization::new(StartModule::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
            post    Version2018_06_28,  "/modules/(?P<name>[^/]+)/stop"    => Authorization::new(StopModule::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
            post    Version2018_06_28,  "/modules/(?P<name>[^/]+)/restart" => Authorization::new(RestartModule::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
            get     Version2018_06_28,  "/modules/(?P<name>[^/]+)/logs"    => Authorization::new(ModuleLogs::new(runtime.clone()), Policy::Anonymous, runtime.clone()),

            get     Version2018_06_28,  "/identities"                      => Authorization::new(ListIdentities::new(identity.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            post    Version2018_06_28,  "/identities"                      => Authorization::new(CreateIdentity::new(identity.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            put     Version2018_06_28,  "/identities/(?P<name>[^/]+)"      => Authorization::new(UpdateIdentity::new(identity.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),
            delete  Version2018_06_28,  "/identities/(?P<name>[^/]+)"      => Authorization::new(DeleteIdentity::new(identity.clone()), Policy::Module(&*AGENT_NAME), runtime.clone()),

            get     Version2018_06_28,  "/systeminfo"                      => Authorization::new(GetSystemInfo::new(runtime.clone()), Policy::Anonymous, runtime.clone()),
        );

        router.new_service().then(|inner| {
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
