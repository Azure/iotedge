// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

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

mod ser_de;

pub use ser_de::string_or_struct;
