// Copyright (c) Microsoft. All rights reserved.

#![cfg(windows)]
#![deny(unused_extern_crates, warnings)]

#[macro_use]
extern crate failure;
// NOTE: For some reason if the extern crate statement for edgelet_utils is moved
// above the one for "failure" above then things stop compiling.
#[macro_use]
extern crate edgelet_utils;
extern crate futures;
extern crate hex;
extern crate hyper;
extern crate tokio_named_pipe;
extern crate url;

pub mod error;
pub mod uri;

use std::io;

use futures::future::FutureResult;
use futures::IntoFuture;
use hyper::client::connect::{Connect, Connected, Destination};

use tokio_named_pipe::PipeStream;

pub use error::{Error, ErrorKind};
pub use uri::Uri;

pub const NAMED_PIPE_SCHEME: &str = "npipe";

pub struct PipeConnector;

impl Connect for PipeConnector {
    type Transport = PipeStream;
    type Error = io::Error;
    type Future = FutureResult<(Self::Transport, Connected), Self::Error>;

    fn connect(&self, dst: Destination) -> Self::Future {
        Uri::get_pipe_path(&dst)
            .map_err(|_err| {
                io::Error::new(
                    io::ErrorKind::InvalidInput,
                    format!("Invalid destination {:?}", dst),
                )
            }).and_then(|path| PipeStream::connect(path, None))
            .map(|stream| (stream, Connected::new()))
            .into_future()
    }
}
