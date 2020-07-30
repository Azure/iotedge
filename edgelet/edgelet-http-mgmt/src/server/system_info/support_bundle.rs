// Copyright (c) Microsoft. All rights reserved.

use std::io::Read;

use futures::Future;
use hyper::header::{CONTENT_ENCODING, CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use serde::Serialize;
use url::form_urlencoded;

use edgelet_core::{LogOptions, Module, ModuleRuntime, RuntimeOperation};
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;
use support_bundle::{make_bundle, OutputLocation};

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct GetSupportBundle<M> {
    runtime: M,
}

impl<M> GetSupportBundle<M> {
    pub fn new(runtime: M) -> Self {
        Self { runtime }
    }
}

impl<M> Handler<Parameters> for GetSupportBundle<M>
where
    M: 'static + ModuleRuntime + Send + Clone + Sync,
    <M::Module as Module>::Config: Serialize,
{
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        debug!("Get Support Bundle");

        let query = req.uri().query().unwrap_or("");
        let _parse: Vec<_> = form_urlencoded::parse(query.as_bytes()).collect();

        let response = make_bundle(
            OutputLocation::Memory,
            LogOptions::default(),
            false,
            false,
            None,
            self.runtime.clone(),
        )
        .map_err(|_| Error::from(ErrorKind::SupportBundle))
        .and_then(|_bundle: Box<dyn Read + Send>| {
            Response::builder()
                .status(StatusCode::OK)
                .header(CONTENT_TYPE, "application/zip")
                .header(CONTENT_ENCODING, "zip")
                .header(CONTENT_LENGTH, 6.to_string().as_str())
                .body("bundle".into())
                .map_err(|_err| {
                    Error::from(ErrorKind::RuntimeOperation(
                        RuntimeOperation::GetModuleLogs("".to_owned()),
                    ))
                })
        })
        .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

// fn get_bundle(query: &str) -> Result<String, Error> {
//     println!("\n\n\nQuery: {}", query);
//     let parse: Vec<_> = form_urlencoded::parse(query.as_bytes()).collect();

//     let mut command = Command::new("iotedge");
//     if let Some((_, host)) = parse.iter().find(|&(ref key, _)| key == "host") {
//         command.args(&["-H", host]);
//     }
//     command.arg("support-bundle");
//     command.args(&["-o", "-"]);
//     if let Some((_, since)) = parse.iter().find(|&(ref key, _)| key == "since") {
//         command.args(&["--since", since]);
//     }

//     let response = command.output().context(ErrorKind::RuntimeOperation(
//         RuntimeOperation::GetSupportBundle("".to_owned()),
//     ))?;

//     str::from_utf8(&response.stdout)
//         .map(std::borrow::ToOwned::to_owned)
//         .map_err(|_err| {
//             Error::from(ErrorKind::RuntimeOperation(
//                 RuntimeOperation::GetModuleLogs("".to_owned()),
//             ))
//         })
// }
