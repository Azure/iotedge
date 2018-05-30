// Copyright (c) Microsoft. All rights reserved.

mod identity;
mod module;

use std::io;

use edgelet_core::{IdentityManager, Module, ModuleRegistry, ModuleRuntime};
use edgelet_http::route::*;
use http::{Request, Response};
use hyper::server::{NewService, Service};
use hyper::{Body, Error as HyperError};
use serde::de::DeserializeOwned;
use serde::Serialize;

use self::identity::*;
use self::module::*;
use IntoResponse;

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
        <M::ModuleRegistry as ModuleRegistry>::Error: IntoResponse,
        I: 'static + IdentityManager + Clone,
        I::Identity: Serialize,
        I::Error: IntoResponse,
    {
        let router = router!(
            get    "/modules"                         => ListModules::new(runtime.clone()),
            post   "/modules"                         => CreateModule::new(runtime.clone()),
            get    "/modules/(?P<name>[^/]+)"         => GetModule,
            put    "/modules/(?P<name>[^/]+)"         => UpdateModule::new(runtime.clone()),
            delete "/modules/(?P<name>[^/]+)"         => DeleteModule::new(runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/start"   => StartModule::new(runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/stop"    => StopModule::new(runtime.clone()),
            post   "/modules/(?P<name>[^/]+)/restart" => RestartModule::new(runtime.clone()),

            get    "/identities"                      => ListIdentities::new(identity.clone()),
            post   "/identities"                      => CreateIdentity::new(identity.clone()),
            put    "/identities/(?P<name>[^/]+)"      => UpdateIdentity::new(identity.clone()),
            delete "/identities/(?P<name>[^/]+)"      => DeleteIdentity::new(identity.clone()),
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
