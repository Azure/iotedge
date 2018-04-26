// Copyright (c) Microsoft. All rights reserved.

#[macro_use]
extern crate clap;
extern crate edgelet_core;
extern crate failure;
#[macro_use]
extern crate failure_derive;
extern crate futures;

use futures::Future;

mod error;
mod list;
mod unknown;
mod version;

pub use error::{Error, ErrorKind};
pub use list::List;
pub use unknown::Unknown;
pub use version::Version;

pub trait Command {
    type Future: Future<Item = (), Error = Error>;

    fn execute(&mut self) -> Self::Future;
}
