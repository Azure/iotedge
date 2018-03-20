// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

#[macro_use]
extern crate failure;
extern crate futures;
extern crate serde;

// Need serde_derive only for unit tests.
#[cfg(test)]
#[macro_use]
extern crate serde_derive;

// Need macros from serde_json for unit tests.
#[cfg(test)]
#[macro_use]
extern crate serde_json;

// Need stuff other than macros from serde_json for non-test code.
#[cfg(not(test))]
extern crate serde_json;

mod error;
pub mod macros;
mod ser_de;

pub use error::{Error, ErrorKind};
pub use ser_de::string_or_struct;
