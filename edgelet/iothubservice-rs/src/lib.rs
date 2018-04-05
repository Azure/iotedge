#![allow(unused_imports, non_snake_case, unused_mut, dead_code)] //CHANGES: Skipping Warnings.

#[macro_use]
extern crate serde_derive;

#[macro_use]
extern crate failure; //CHANGES: Added failure and put in alphabetic order.
extern crate futures;
extern crate hyper;
extern crate serde;
extern crate serde_json;
extern crate url;

pub mod apis;
pub mod models;
