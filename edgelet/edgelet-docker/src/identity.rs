// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

use std::marker::PhantomData;

use crate::DockerModuleRuntime;
use edgelet_core::pid::Pid;
use edgelet_core::AuthId;
use edgelet_http::NewIdService;
use hyper::service::Service;
use hyper::{Body, Error as HyperError, Request};
use tokio::prelude::Stream;

#[derive(Clone)]
pub struct DockerIdService<S> {
    pid: Pid,
    runtime: DockerModuleRuntime,
    inner: S,
}

impl<S> DockerIdService<S> {
    pub fn new(pid: Pid, runtime: DockerModuleRuntime, inner: S) -> Self {
        DockerIdService {
            pid,
            runtime,
            inner,
        }
    }
}

impl<S> Service for DockerIdService<S>
where
    S: Service<ReqBody = Body>,
    <S as Service>::ResBody: Stream<Error = HyperError> + 'static,
    <<S as Service>::ResBody as Stream>::Item: AsRef<[u8]>,
{
    type ReqBody = S::ReqBody;
    type ResBody = S::ResBody;
    type Error = S::Error;
    type Future = S::Future;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        //        self.runtime.list_with_details()
        //            .map_err(|e| Error::from(e.context(ErrorKind::ModuleRuntime)))

        //        let auth_id = match self.pid {
        //            Pid::None => AuthId::None,
        //            Pid::Any => AuthId::Any,
        //            Pid::Value(pid) => match self.get_module_name(pid) {
        //                Some(name) => AuthId::Value(name),
        //                _ => AuthId::None,
        //            },
        //        };
        //

        let auth_id = AuthId::None;

        let mut req = req;
        req.extensions_mut().insert(auth_id);

        self.inner.call(req)
    }
}

#[derive(Clone)]
pub struct DockerNewIdService<S> {
    phantom: PhantomData<S>,
    runtime: DockerModuleRuntime,
}

impl<S> DockerNewIdService<S> {
    pub fn new(runtime: DockerModuleRuntime) -> DockerNewIdService<S> {
        DockerNewIdService {
            phantom: PhantomData,
            runtime,
        }
    }
}

impl<S> NewIdService for DockerNewIdService<S>
where
    S: Service<ReqBody = Body>,
    <S as Service>::ResBody: Stream<Error = HyperError> + 'static,
    <<S as Service>::ResBody as Stream>::Item: AsRef<[u8]>,
{
    type IdService = DockerIdService<S>;
    type InnerService = S;

    fn new_service(&self, pid: Pid, inner: Self::InnerService) -> Self::IdService {
        DockerIdService::new(pid, self.runtime.clone(), inner)
    }
}
