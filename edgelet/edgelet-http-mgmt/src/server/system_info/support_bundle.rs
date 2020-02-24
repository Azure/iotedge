// Copyright (c) Microsoft. All rights reserved.

use std::process::Command;
use std::str;

use failure::ResultExt;
use futures::{future, Future};
use hyper::header::{CONTENT_ENCODING, CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use url::form_urlencoded;

use edgelet_core::RuntimeOperation;
use edgelet_http::route::{Handler, Parameters};
use edgelet_http::Error as HttpError;

use crate::error::{Error, ErrorKind};
use crate::IntoResponse;

pub struct GetSupportBundle {}

impl GetSupportBundle {
    pub fn new() -> Self {
        GetSupportBundle {}
    }
}

impl Handler<Parameters> for GetSupportBundle {
    fn handle(
        &self,
        req: Request<Body>,
        _params: Parameters,
    ) -> Box<dyn Future<Item = Response<Body>, Error = HttpError> + Send> {
        debug!("Get Support Bundle");

        let query = req
        .uri()
        .query()
        .unwrap_or("");

        let response = get_bundle(query)
            .and_then(|bundle: String| {
                Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/zip")
                    .header(CONTENT_ENCODING, "zip")
                    .header(CONTENT_LENGTH, bundle.len().to_string().as_str())
                    .body(bundle.into())
                    .map_err(|_err| {
                        Error::from(ErrorKind::RuntimeOperation(
                            RuntimeOperation::GetModuleLogs("".to_owned()),
                        ))
                    })
            })
            .or_else(|e| Ok(e.into_response()));

        let response = future::result(response);

        Box::new(response)
    }
}

fn get_bundle(query: &str) -> Result<String, Error> {
    let parse: Vec<_> = form_urlencoded::parse(query.as_bytes()).collect();

    let mut command = Command::new("iotedge");
    if let Some((_, host)) = parse.iter().find(|&(ref key, _)| key == "host") {
        command.args(&["-H", host]);
    }
    command.arg("support-bundle");
    command.args(&["-o", "-"]);
    if let Some((_, since)) = parse.iter().find(|&(ref key, _)| key == "since") {
        command.args(&["--since", since]);
    }

    let response = command.output().context(ErrorKind::RuntimeOperation(
        RuntimeOperation::GetModuleLogs("".to_owned()),
    ))?;

    str::from_utf8(&response.stdout)
        .map(std::borrow::ToOwned::to_owned)
        .map_err(|_err| {
            Error::from(ErrorKind::RuntimeOperation(
                RuntimeOperation::GetModuleLogs("".to_owned()),
            ))
        })
}
