// Copyright (c) Microsoft. All rights reserved.

mod identity;
mod module;

use std::io;

use edgelet_core::{Module, ModuleRuntime};
use edgelet_http::route::*;
use hyper::Error as HyperError;
use hyper::server::{NewService, Request, Response, Service};
use serde::Serialize;

use self::module::*;

#[derive(Clone)]
pub struct ManagementService {
    inner: RouterService<RegexRecognizer>,
}

impl ManagementService {
    pub fn new<M>(modules: M) -> Result<Self, HyperError>
    where
        M: 'static + ModuleRuntime,
        <M::Module as Module>::Config: Serialize,
    {
        let router = router!(
            get    "/modules"                         => ListModules::new(modules),
            post   "/modules"                         => CreateModule,
            get    "/modules/(?P<name>[^/]+)"         => GetModule,
            put    "/modules/(?P<name>[^/]+)"         => UpdateModule,
            delete "/modules/(?P<name>[^/]+)"         => DeleteModule,
            post   "/modules/(?P<name>[^/]+)/start"   => StartModule,
            post   "/modules/(?P<name>[^/]+)/stop"    => StopModule,
            post   "/modules/(?P<name>[^/]+)/restart" => RestartModule,
        );
        let inner = router.new_service()?;
        let service = ManagementService { inner };
        Ok(service)
    }
}

impl Service for ManagementService {
    type Request = Request;
    type Response = Response;
    type Error = HyperError;
    type Future = BoxFuture<Response, HyperError>;

    fn call(&self, req: Request) -> Self::Future {
        self.inner.call(req)
    }
}

impl NewService for ManagementService {
    type Request = Request;
    type Response = Response;
    type Error = HyperError;
    type Instance = Self;

    fn new_service(&self) -> io::Result<Self::Instance> {
        Ok(self.clone())
    }
}
