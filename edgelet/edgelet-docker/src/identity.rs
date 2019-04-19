// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions, clippy::use_self)]
#![allow(unused)] // todo remove

use std::marker::PhantomData;

use crate::runtime::DockerModuleRuntime;
use edgelet_core::pid::Pid;
use edgelet_core::{AuthId, Error, ErrorKind, Module, ModuleRuntime};
use edgelet_http::NewIdService;
use failure::Fail;
use futures::future::Either;
use futures::{future, Future};
use hyper::service::Service;
use hyper::{Body, Error as HyperError, Request};
use log::info;
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
    S: Service<ReqBody = Body> + Sync,
    <S as Service>::ResBody: Stream<Error = HyperError> + 'static,
    <<S as Service>::ResBody as Stream>::Item: AsRef<[u8]>,
    S::Future: Send + 'static,
{
    type ReqBody = S::ReqBody;
    type ResBody = S::ResBody;
    type Error = S::Error;
    type Future = S::Future;

    fn call(&mut self, req: Request<Self::ReqBody>) -> Self::Future {
        let mut req = req;

        let auth_id = match self.pid {
            Pid::None => AuthId::None,
            Pid::Any => AuthId::Any,
            Pid::Value(pid) => {
                self.runtime
                    .list_with_details()
                    .map_err(|e| Error::from(e.context(ErrorKind::ModuleRuntime)))
                    .filter_map(move |(m, rs)| {
                        rs.process_ids()
                            .filter(|process_ids| process_ids.contains(&&pid))
                            .map(|_| m)
                    })
                    .into_future()
                    .then(move |result| match result {
                        Ok((Some(m), _)) => Ok(AuthId::Value(m.name().to_string())),
                        Ok((None, _)) => {
                            info!("Unable to find a module for caller pid: {}", pid);
                            Ok(AuthId::None)
                        }
                        Err((err, _)) => Err(err),
                    })
                    .wait()
                    .unwrap() // todo what should do in case of error?
            }
        };

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
    S: Service<ReqBody = Body> + Sync,
    S::ResBody: Stream<Error = HyperError> + 'static,
    <S::ResBody as Stream>::Item: AsRef<[u8]>,
    S::Future: Send + 'static,
{
    type IdService = DockerIdService<S>;
    type InnerService = S;

    fn new_service(&self, pid: Pid, inner: Self::InnerService) -> Self::IdService {
        DockerIdService::new(pid, self.runtime.clone(), inner)
    }
}
