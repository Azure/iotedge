// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]
#![cfg(windows)]

#[macro_use]
extern crate failure;
// NOTE: For some reason if the extern crate statement for edgelet_utils is moved
// above the one for "failure" above then things stop compiling.
#[macro_use]
extern crate edgelet_utils;
extern crate futures;
extern crate hex;
extern crate hyper;
extern crate tokio_core;
extern crate tokio_named_pipe;
extern crate tokio_service;
extern crate url;

pub mod error;
pub mod uri;

use std::io;

use futures::future::FutureResult;
use futures::IntoFuture;
use hyper::Uri as HyperUri;
use tokio_core::reactor::Handle;
use tokio_service::Service;

use tokio_named_pipe::PipeStream;

pub use error::{Error, ErrorKind};
pub use uri::Uri;

pub const NAMED_PIPE_SCHEME: &str = "npipe";

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
        Uri::get_pipe_path(&uri)
            .map_err(|_err| {
                io::Error::new(io::ErrorKind::InvalidInput, format!("Invalid uri {}", uri))
            }).and_then(|path| PipeStream::connect(path, &self.0, None))
            .into_future()
    }
}
