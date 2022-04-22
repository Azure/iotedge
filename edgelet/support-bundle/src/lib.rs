// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::module_name_repetitions)]

mod error;
mod logs;
mod support_bundle;

pub use crate::error::Error;
pub use crate::logs::pull_logs;
pub use crate::support_bundle::{make_bundle, OutputLocation};
