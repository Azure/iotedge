// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]
#![allow(unused)] // todo remove

use edgelet_core::pid::Pid;
use edgelet_core::{AuthId, Authenticator};
use failure::{Compat, Fail};
use futures::{future, Future};
use hyper::service::{NewService, Service};
use hyper::{Body, Request, Response};

use crate::{Error, ErrorKind, IntoResponse};

#[derive(Clone)]
pub struct AuthenticationService<M, S> {
    runtime: M,
    inner: S,
}

impl<M, S> AuthenticationService<M, S> {
    pub fn new(runtime: M, inner: S) -> Self {
        AuthenticationService { runtime, inner }
    }
}

impl<M, S> Service for AuthenticationService<M, S>
where
    M: Authenticator<Request = Request<S::ReqBody>> + Send + Clone + 'static,
    M::AuthenticateFuture: Future<Item = AuthId> + Send + 'static,
    <M::AuthenticateFuture as Future>::Error: Fail,
    S: Service<ReqBody = Body, ResBody = Body> + Send + Clone + 'static,
    S::Future: Send + 'static,
    S::Error: Send,
{
    type ReqBody = S::ReqBody;
    type ResBody = S::ResBody;
    type Error = S::Error;
    type Future = Box<
        dyn Future<Item = <S::Future as Future>::Item, Error = <S::Future as Future>::Error> + Send,
    >;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let mut req = req;
        let mut inner = self.inner.clone();
        Box::new(
            self.runtime
                .authenticate(&req)
                .then(move |auth_id| match auth_id {
                    Ok(auth_id) => {
                        req.extensions_mut().insert(auth_id);
                        future::Either::A(inner.call(req))
                    }

                    Err(err) => future::Either::B(future::ok(
                        Error::from(err.context(ErrorKind::Authorization)).into_response(),
                    )),
                }),
        )
    }
}

impl<M, S> NewService for AuthenticationService<M, S>
where
    M: Authenticator + Send + Clone + 'static,
    S: NewService,
    S::Future: Send + 'static,
    AuthenticationService<M, <S as NewService>::Service>: Service,
{
    type ReqBody = <AuthenticationService<M, <S as NewService>::Service> as Service>::ReqBody;
    type ResBody = <AuthenticationService<M, <S as NewService>::Service> as Service>::ResBody;
    type Error = <AuthenticationService<M, <S as NewService>::Service> as Service>::Error;
    type Service = AuthenticationService<M, <S as NewService>::Service>;
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
