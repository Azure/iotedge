// Copyright (c) Microsoft. All rights reserved.

#![cfg(windows)]
#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self
)]

pub mod error;
pub mod uri;

use std::io;

use futures::future::FutureResult;
use futures::IntoFuture;
use hyper::client::connect::{Connect, Connected, Destination};

use tokio_named_pipe::PipeStream;

pub use crate::error::{Error, ErrorKind};
pub use crate::uri::Uri;

pub const NAMED_PIPE_SCHEME: &str = "npipe";

#[derive(Clone)]
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
            })
            .and_then(|path| PipeStream::connect(path, None))
            .map(|stream| (stream, Connected::new()))
            .into_future()
    }
}
