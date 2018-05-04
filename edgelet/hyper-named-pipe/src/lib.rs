// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]
#![cfg(windows)]

#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate tokio_core;
extern crate tokio_named_pipe;
extern crate tokio_service;

pub mod error;

use std::io;
use std::path::{Path, PathBuf};

use futures::IntoFuture;
use futures::future::FutureResult;
use hyper::Uri as HyperUri;
use tokio_core::reactor::Handle;
use tokio_service::Service;

use tokio_named_pipe::PipeStream;

use error::{ErrorKind, Result};

const NAMED_PIPE_SCHEME: &str = "npipe";

pub struct PipeConnector(Handle);

impl PipeConnector {
    pub fn new(handle: Handle) -> Self {
        PipeConnector(handle)
    }
}

impl Service for PipeConnector {
    type Request = HyperUri;
    type Response = PipeStream;
    type Error = io::Error;
    type Future = FutureResult<PipeStream, io::Error>;

    fn call(&self, uri: HyperUri) -> Self::Future {
        parse_path(&uri)
            .map_err(|_err| {
                io::Error::new(io::ErrorKind::InvalidInput, format!("Invalid uri {}", uri))
            })
            .and_then(|path| PipeStream::connect(path, &self.0))
            .into_future()
    }
}

fn parse_path(url: &HyperUri) -> Result<PathBuf> {
    if url.scheme().unwrap_or("invalid") != NAMED_PIPE_SCHEME {
        Err(ErrorKind::InvalidUrlScheme)?
    } else if url.host().map(|h| h.trim()).unwrap_or("") == "" {
        Err(ErrorKind::MissingUrlHost)?
    } else {
        Ok(Path::new(&format!(
            r"\\{}{}",
            url.host().unwrap(),
            url.path().replace("/", "\\")
        )).to_path_buf())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use tokio_core::reactor::Core;

    #[test]
    fn no_scheme_in_path_fails() {
        let mut core = Core::new().unwrap();
        let connector = PipeConnector::new(core.handle());
        let task = connector.call("boo".parse::<HyperUri>().unwrap());
        assert!(core.run(task).is_err())
    }

    #[test]
    fn invalid_scheme_in_path_fails() {
        let mut core = Core::new().unwrap();
        let connector = PipeConnector::new(core.handle());
        let task = connector.call("bad.scheme://boo".parse::<HyperUri>().unwrap());
        assert!(core.run(task).is_err())
    }

    #[test]
    fn missing_host_in_path_fails() {
        let mut core = Core::new().unwrap();
        let connector = PipeConnector::new(core.handle());
        let task = connector.call("npipe://   /boo".parse::<HyperUri>().unwrap());
        assert!(core.run(task).is_err())
    }
}
