// Copyright (c) Microsoft. All rights reserved.

use failure::{Context, ResultExt};
use futures::{future, Future};
use hyper::server::{Request, Response};
use hyper::Error as HyperError;
use serde::Serialize;
use serde_json;

use edgelet_core::{Module, ModuleRuntime};
use edgelet_http::route::{BoxFuture, Handler, Parameters};
use management::models::*;

use error::{Error, ErrorKind};

pub struct ListModules<M>
where
    M: 'static + ModuleRuntime,
    <M::Module as Module>::Config: Serialize,
{
    runtime: M,
}

impl<M> ListModules<M>
where
    M: 'static + ModuleRuntime,
    <M::Module as Module>::Config: Serialize,
{
    pub fn new(runtime: M) -> Self {
        ListModules { runtime }
    }
}

impl<M> Handler<Parameters> for ListModules<M>
where
    M: 'static + ModuleRuntime,
    <M::Module as Module>::Config: Serialize,
{
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = self.runtime
            .list()
            .then(|result| match result.context(ErrorKind::ModuleRuntime) {
                Ok(mods) => {
                    let futures = mods.into_iter().map(core_to_details);
                    let response = future::join_all(futures).map(|details| {
                        let body = ModuleList::new(details);
                        serde_json::to_string(&body)
                            .context(ErrorKind::Serde)
                            .map(|b| Response::new().with_body(b))
                            .unwrap_or_else(from_context)
                    });
                    future::Either::A(response)
                }
                Err(e) => future::Either::B(future::ok(from_context(e))),
            })
            .map_err(From::from);
        Box::new(response)
    }
}

fn from_context(context: Context<ErrorKind>) -> Response {
    let error: Error = context.into();
    error.into()
}

fn core_to_details<M>(module: M) -> Box<Future<Item = ModuleDetails, Error = Error>>
where
    M: 'static + Module,
    M::Config: Serialize,
{
    let details = module
        .runtime_state()
        .then(move |result| {
            result.context(ErrorKind::ModuleRuntime).and_then(|state| {
                serde_json::to_value(module.config())
                    .context(ErrorKind::Serde)
                    .and_then(|settings| {
                        let config = Config::new(settings).with_env(Vec::new());
                        let mut runtime_status = RuntimeStatus::new(state.status().to_string());
                        state
                            .status_description()
                            .map(|d| runtime_status.set_description(d.to_string()));
                        let mut status = Status::new(runtime_status);
                        state
                            .started_at()
                            .map(|s| status.set_start_time(s.to_rfc3339()));
                        state.exit_code().and_then(|code| {
                            state.finished_at().map(|f| {
                                status.set_exit_status(ExitStatus::new(
                                    f.to_rfc3339(),
                                    code.to_string(),
                                ))
                            })
                        });

                        Ok(ModuleDetails::new(
                            "id".to_string(),
                            module.name().to_string(),
                            module.type_().to_string(),
                            config,
                            status,
                        ))
                    })
            })
        })
        .map_err(From::from);
    Box::new(details)
}

pub struct CreateModule;

impl Handler<Parameters> for CreateModule {
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = Response::new();
        Box::new(future::ok(response))
    }
}

pub struct GetModule;

impl Handler<Parameters> for GetModule {
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = Response::new();
        Box::new(future::ok(response))
    }
}

pub struct UpdateModule;

impl Handler<Parameters> for UpdateModule {
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = Response::new();
        Box::new(future::ok(response))
    }
}

pub struct DeleteModule;

impl Handler<Parameters> for DeleteModule {
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = Response::new();
        Box::new(future::ok(response))
    }
}

pub struct StartModule;

impl Handler<Parameters> for StartModule {
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = Response::new();
        Box::new(future::ok(response))
    }
}

pub struct StopModule;

impl Handler<Parameters> for StopModule {
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = Response::new();
        Box::new(future::ok(response))
    }
}

pub struct RestartModule;

impl Handler<Parameters> for RestartModule {
    fn handle(&self, _req: Request, _params: Parameters) -> BoxFuture<Response, HyperError> {
        let response = Response::new();
        Box::new(future::ok(response))
    }
}
