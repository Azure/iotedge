// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self
)]

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
pub use self::linux::{listener, listener_name, listeners_name, LISTEN_FDS_START};
