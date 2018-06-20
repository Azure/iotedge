// Copyright (c) Microsoft. All rights reserved.

extern crate bytes;
extern crate chrono;
extern crate chrono_humanize;
#[macro_use]
extern crate clap;
extern crate edgelet_core;
extern crate edgelet_http_mgmt;
extern crate failure;
#[macro_use]
extern crate failure_derive;
#[macro_use]
extern crate futures;
extern crate tabwriter;
extern crate tokio_io;
extern crate url;

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
    type Future: Future<Item = (), Error = Error>;

    fn execute(&mut self) -> Self::Future;
}
