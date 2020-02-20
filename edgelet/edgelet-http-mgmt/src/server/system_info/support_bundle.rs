// Copyright (c) Microsoft. All rights reserved.

use failure::ResultExt;
use futures::Future;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use serde::Serialize;
use serde_json;

use edgelet_core::{Module, ModuleRuntime, RuntimeOperation};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct GetSupportBundle<M> {
    runtime: M,
}

impl<M> GetSupportBundle<M> {
    pub fn new(runtime: M) -> Self {
        GetSupportBundle { runtime }
    }
}

impl<M> Handler<Parameters> for GetSupportBundle<M>
where
    M: 'static + ModuleRuntime + Send,
    <M::Module as Module>::Config: Serialize,
{
    fn handle(
        &self,
        _req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        debug!("Get Support Bundle");

        let mut check = ShellCommand::new(iotedge);
        check.arg("check").args(&["-o", "json"]);

        if let Some(host_name) = state.iothub_hostname.clone() {
            check.args(&["--iothub-hostname", &host_name]);
        }
        let check = check
            .output()
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        Box::new(response)
    }
}
