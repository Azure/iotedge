// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::similar_names,
    clippy::use_self,
    clippy::too_many_lines
)]

mod json_connector;

pub mod cert;
pub mod identity;
pub mod module;

pub use crate::json_connector::JsonConnector;
