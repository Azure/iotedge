// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions)]

mod error;
mod runtime_util;
mod shell_util;
mod support_bundle;

pub use crate::error::{Error, ErrorKind};
pub use crate::runtime_util::write_logs;
pub use crate::support_bundle::{make_bundle, OutputLocation};
