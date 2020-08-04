#![allow(bare_trait_objects, dead_code, unused_imports, unused_mut)]

#[macro_use]
extern crate serde_derive;

extern crate hyper;
extern crate serde;
extern crate serde_json;
extern crate futures;
extern crate url;

pub mod apis;
pub mod models;
