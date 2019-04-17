// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]

use std::marker::PhantomData;

mod client;
mod config;
mod error;
mod module;
mod runtime;

pub use config::DockerConfig;
pub use error::{Error, ErrorKind};
pub use module::{DockerModule, MODULE_TYPE};

use edgelet_core::pid::Pid;
use edgelet_http::NewIdService;
use hyper::service::Service;
use hyper::{Body, Error as HyperError, Request};
pub use runtime::DockerModuleRuntime;
use tokio::prelude::Stream;

#[derive(Clone)]
pub struct PidService<T> {
    pid: Pid,
    inner: T,
}

impl<T> PidService<T> {
    pub fn new(pid: Pid, inner: T) -> Self {
        PidService { pid, inner }
    }
}

impl<T> Service for PidService<T>
where
    T: Service<ReqBody = Body>,
    <T as Service>::ResBody: Stream<Error = HyperError> + 'static,
    <<T as Service>::ResBody as Stream>::Item: AsRef<[u8]>,
{
    type ReqBody = T::ReqBody;
    type ResBody = T::ResBody;
    type Error = T::Error;
    type Future = T::Future;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let mut req = req;
        req.extensions_mut().insert(self.pid);
        self.inner.call(req)
    }
}

#[derive(Clone)]
pub struct DockerNewIdService<T> {
    phantom: PhantomData<T>,
}

impl<T> DockerNewIdService<T> {
    pub fn new() -> DockerNewIdService<T> {
        DockerNewIdService {
            phantom: PhantomData,
        }
    }
}

impl<T> NewIdService for DockerNewIdService<T>
where
    T: Service<ReqBody = Body>,
    <T as Service>::ResBody: Stream<Error = HyperError> + 'static,
    <<T as Service>::ResBody as Stream>::Item: AsRef<[u8]>,
{
    type IdService = PidService<T>;
    type InnerService = T;

    fn new_service(&self, pid: Pid, inner: Self::InnerService) -> Self::IdService {
        PidService::new(pid, inner)
    }
}
