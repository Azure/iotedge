// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate base64;
extern crate config;
extern crate edgelet_core;
extern crate edgelet_docker;
extern crate env_logger;
#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate hyper_tls;
extern crate iothubservice;
#[macro_use]
extern crate log;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_json;
extern crate tokio_core;
extern crate tokio_signal;
extern crate url;

mod error;
pub mod logging;
pub mod settings;
pub mod signal;

pub use self::error::{Error, ErrorKind};
