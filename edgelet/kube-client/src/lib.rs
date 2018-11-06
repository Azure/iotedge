// Copyright (c) Microsoft. All rights reserved.

extern crate base64;
extern crate bytes;
extern crate dirs;
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate hyper_tls;
pub extern crate k8s_openapi;
extern crate log;
extern crate native_tls;
extern crate openssl;
extern crate serde;
#[macro_use]
extern crate serde_derive;
extern crate serde_yaml;
extern crate url;

pub mod client;
pub mod config;
pub mod error;
pub mod kube;

pub use self::client::Client;
pub use self::config::{get_config, Config, TokenSource, ValueToken};
pub use self::error::{Error, ErrorKind};
