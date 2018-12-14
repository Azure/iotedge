// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
// Remove this when clippy stops warning about old-style `allow()`,
// which can only be silenced by enabling a feature and thus requires nightly
//
// Ref: https://github.com/rust-lang-nursery/rust-clippy/issues/3159#issuecomment-420530386
#![allow(renamed_and_removed_lints)]
#![cfg_attr(feature = "cargo-clippy", deny(clippy, clippy_pedantic))]
#![cfg_attr(feature = "cargo-clippy", allow(stutter, use_self))]

extern crate failure;
#[cfg(target_os = "linux")]
#[cfg(test)]
#[macro_use]
extern crate lazy_static;
#[cfg(target_os = "linux")]
#[macro_use]
extern crate log;
#[cfg(target_os = "linux")]
extern crate nix;

use std::net::SocketAddr;

mod error;
#[cfg(target_os = "linux")]
mod linux;

pub use self::error::{Error, ErrorKind, SocketLookupType};

pub type Fd = i32;

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum Socket {
    Inet(Fd, SocketAddr),
    Unix(Fd),
    Unknown,
}

#[cfg(target_os = "linux")]
pub use self::linux::{listener, listener_name, listeners_name};
