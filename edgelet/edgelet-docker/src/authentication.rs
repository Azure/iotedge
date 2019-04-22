// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]
#![allow(unused)] // todo remove

use crate::runtime::DockerModuleRuntime;
use crate::Error;
use edgelet_core::pid::Pid;
use edgelet_core::{AuthId, Authenticator};
use failure::{Compat, Fail};
use futures::future::Future;
use hyper::service::{NewService, Service};
use hyper::{Body, Request};

#[derive(Clone)]
pub struct AuthenticationService<S> {
    runtime: DockerModuleRuntime,
    inner: S,
}

impl<S> AuthenticationService<S> {
    pub fn new(runtime: DockerModuleRuntime, inner: S) -> Self {
        AuthenticationService { runtime, inner }
    }
}

impl<S> Service for AuthenticationService<S>
where
    S: Service<ReqBody = Body>,
    S::Future: Send + 'static,
{
    type ReqBody = S::ReqBody;
    type ResBody = S::ResBody;
    type Error = S::Error;
    type Future = Box<dyn Future<Item = <S::Future as Future>::Item, Error = Self::Error> + Send>;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let pid = req
            .extensions()
            .get::<Pid>()
            .cloned()
            .unwrap_or_else(|| Pid::None);

        let mut req = req;

        // todo Implement futures pipeline instead of waiting for response
        let auth_id = self.runtime.authenticate(pid).wait().unwrap();

        req.extensions_mut().insert(auth_id);
        Box::new(self.inner.call(req))
    }
}

impl<S> NewService for AuthenticationService<S>
where
    S: NewService,
    S::Future: Send + 'static,
    AuthenticationService<<S as NewService>::Service>: Service,
{
    type ReqBody = <AuthenticationService<<S as NewService>::Service> as Service>::ReqBody;
    type ResBody = <AuthenticationService<<S as NewService>::Service> as Service>::ResBody;
    type Error = <AuthenticationService<<S as NewService>::Service> as Service>::Error;
    type Service = AuthenticationService<<S as NewService>::Service>;
    type Future = Box<dyn Future<Item = Self::Service, Error = Self::InitError> + Send>;
    type InitError = <S as NewService>::InitError;

    fn new_service(&self) -> Self::Future {
        let runtime = self.runtime.clone();
        Box::new(
            self.inner
                .new_service()
                .map(|inner| AuthenticationService::new(runtime, inner)),
        )
    }
}
