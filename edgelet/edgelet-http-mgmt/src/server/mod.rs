// Copyright (c) Microsoft. All rights reserved.

mod identity;
mod module;
mod system_info;

use std::io;

use edgelet_core::{Error as CoreError, IdentityManager, Module, ModuleRegistry, ModuleRuntime,
                   Policy};
use edgelet_http::authorization::Authorization;
use edgelet_http::route::*;
use http::{Request, Response};
use hyper::server::{NewService, Service};
use hyper::{Body, Error as HyperError};
use serde::de::DeserializeOwned;
use serde::Serialize;

use self::identity::*;
use self::module::*;
use self::system_info::*;

use IntoResponse;

lazy_static! {
    static ref AGENT_NAME: String = "$edgeAgent".to_string();
}

#[derive(Clone)]
pub struct ManagementService {
    inner: RouterService<RegexRecognizer>,
}

impl ManagementService {
    pub fn new<M, I>(runtime: &M, identity: &I) -> Result<Self, HyperError>
    where
        M: 'static + ModuleRuntime + Clone,
        <M::Module as Module>::Config: DeserializeOwned + Serialize,
        M::Error: IntoResponse,
        M::Error: Into<CoreError>,
        <M::Module as Module>::Error: Into<CoreError>,
        M::Logs: Into<Body>,
        <M::ModuleRegistry as ModuleRegistry>::Error: IntoResponse,
        I: 'static + IdentityManager + Clone,
        I::Identity: Serialize,
        I::Error: IntoResponse,
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
        let inner = router.new_service()?;
        let service = ManagementService { inner };
        Ok(service)
    }
}

impl Service for ManagementService {
    type Request = Request<Body>;
    type Response = Response<Body>;
    type Error = HyperError;
    type Future = BoxFuture<Self::Response, HyperError>;

    fn call(&self, req: Request<Body>) -> Self::Future {
        self.inner.call(req)
    }
}

impl NewService for ManagementService {
    type Request = Request<Body>;
    type Response = Response<Body>;
    type Error = HyperError;
    type Instance = Self;

    fn new_service(&self) -> io::Result<Self::Instance> {
        Ok(self.clone())
    }
}
