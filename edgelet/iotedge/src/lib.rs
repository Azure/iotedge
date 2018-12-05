// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]
#![cfg_attr(feature = "cargo-clippy", allow(stutter, use_self))]

extern crate bytes;
extern crate chrono;
extern crate chrono_humanize;
#[macro_use]
extern crate clap;
extern crate edgelet_core;
extern crate failure;
#[macro_use]
extern crate futures;
extern crate tabwriter;
extern crate tokio;

use futures::Future;

mod error;
mod list;
mod logs;
mod restart;
mod unknown;
mod version;

pub use error::{Error, ErrorKind};
pub use list::List;
pub use logs::Logs;
pub use restart::Restart;
pub use unknown::Unknown;
pub use version::Version;

pub trait Command {
    type Future: Future<Item = (), Error = Error> + Send;

    fn execute(&mut self) -> Self::Future;
}
