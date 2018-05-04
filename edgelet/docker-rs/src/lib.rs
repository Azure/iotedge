#![allow(unused_imports, non_snake_case, unused_mut, dead_code, unknown_lints)]
#![allow(clippy, clippy_pedantic)]

#[macro_use]
extern crate serde_derive;

#[macro_use]
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate serde;
extern crate serde_json;
extern crate url;

pub mod apis;
pub mod models;
pub mod utils;
