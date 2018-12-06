// Copyright (c) Microsoft. All rights reserved.

#![cfg(windows)]
#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]
#![cfg_attr(feature = "cargo-clippy", allow(stutter, use_self))]

extern crate edgelet_utils;
#[macro_use]
extern crate failure;
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
            })
            .and_then(|path| PipeStream::connect(path, None))
            .map(|stream| (stream, Connected::new()))
            .into_future()
    }
}
