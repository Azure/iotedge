// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate config;
extern crate edgelet_core;
extern crate edgelet_docker;
extern crate env_logger;
#[macro_use]
extern crate failure;
extern crate hyper;
extern crate iothubservice;
extern crate log;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate url;

mod error;
pub mod logging;
pub mod settings;

pub use self::error::{Error, ErrorKind};
