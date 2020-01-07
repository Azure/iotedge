// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    clippy::type_complexity,
    clippy::use_self
)]

use futures::Future;
use serde_derive::Deserialize;

mod check;
mod error;
mod list;
mod logs;
mod restart;
mod support_bundle;
mod unknown;
mod version;

pub use crate::check::{Check, OutputFormat};
pub use crate::error::{Error, ErrorKind, FetchLatestVersionsReason};
pub use crate::list::List;
pub use crate::logs::Logs;
pub use crate::restart::Restart;
pub use crate::support_bundle::SupportBundle;
pub use crate::unknown::Unknown;
pub use crate::version::Version;

pub trait Command {
    type Future: Future<Item = ()> + Send;

    fn execute(self) -> Self::Future;
}

#[derive(Debug, Deserialize)]
pub struct LatestVersions {
    pub iotedged: String,
}
