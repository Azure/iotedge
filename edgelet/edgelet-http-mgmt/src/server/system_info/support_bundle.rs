// Copyright (c) Microsoft. All rights reserved.
use std::io::Read;

use failure::ResultExt;
use futures::{Async, Future, Poll, Stream};
use hyper::header::{CONTENT_ENCODING, CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Request, Response, StatusCode};
use log::debug;
use serde::Serialize;
use url::form_urlencoded;

use edgelet_core::{parse_since, LogOptions, Module, ModuleRuntime, RuntimeOperation};
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

        let response = get_bundle(self.runtime.clone(), query)
            .and_then(|(bundle, size)| -> Result<_, Error> {
                let body = Body::wrap_stream(ReadStream(bundle));

                let response = Response::builder()
                    .status(StatusCode::OK)
                    .header(CONTENT_TYPE, "application/zip")
                    .header(CONTENT_ENCODING, "zip")
                    .header(CONTENT_LENGTH, size.to_string().as_str())
                    .body(body)
                    .context(ErrorKind::RuntimeOperation(
                        RuntimeOperation::GetSupportBundle,
                    ))?;
                Ok(response)
            })
            .or_else(|e| Ok(e.into_response()));

        Box::new(response)
    }
}

fn get_bundle<M>(
    runtime: M,
    query: &str,
) -> Box<dyn Future<Item = (Box<dyn Read + Send>, u64), Error = Error> + Send>
where
    M: 'static + ModuleRuntime + Send + Clone + Sync,
{
    let parse: Vec<_> = form_urlencoded::parse(query.as_bytes()).collect();

    let mut log_options = LogOptions::new();
    if let Some((_, since)) = parse.iter().find(|&(ref key, _)| key == "since") {
        if let Ok(since) = parse_since(since) {
            log_options = log_options.with_since(since);
        }
    }
    if let Some((_, until)) = parse.iter().find(|&(ref key, _)| key == "until") {
        if let Ok(until) = parse_since(until) {
            log_options = log_options.with_until(until);
        }
    }

    let iothub_hostname = parse.iter().find_map(|(ref key, iothub_hostname)| {
        if key == "iothub_hostname" {
            Some(iothub_hostname.to_string())
        } else {
            None
        }
    });

    let edge_runtime_only = parse
        .iter()
        .find_map(|(ref key, edge_runtime_only)| {
            if key == "edge_runtime_only" {
                Some(edge_runtime_only == "true" || edge_runtime_only == "True")
            } else {
                None
            }
        })
        .unwrap_or_default();

    let result = make_bundle(
        OutputLocation::Memory,
        log_options,
        edge_runtime_only,
        false,
        iothub_hostname,
        runtime,
    )
    .map_err(|_| Error::from(ErrorKind::SupportBundle));

    Box::new(result)
}

struct ReadStream(Box<dyn Read + Send>);

impl Stream for ReadStream {
    type Item = Vec<u8>;
    type Error = Box<dyn std::error::Error + Send + Sync>;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        let mut part: Vec<u8> = vec![0; 1024];
        let size = self.0.read(&mut part)?;
        if size > 0 {
            part.resize(size, 0);
            Ok(Async::Ready(Some(part)))
        } else {
            Ok(Async::Ready(None))
        }
    }
}
