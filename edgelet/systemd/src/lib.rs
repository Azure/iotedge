// Copyright (c) Microsoft. All rights reserved.

extern crate failure;
#[macro_use]
extern crate failure_derive;
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

pub use self::error::{Error, ErrorKind};

pub type Fd = i32;

#[derive(Clone, Debug, PartialEq)]
pub enum Socket {
    Inet(Fd, SocketAddr),
    Unix(Fd),
    Unknown,
}

#[cfg(target_os = "linux")]
pub use self::linux::{listener, listener_name, listeners_name};

#[cfg(not(target_os = "linux"))]
pub use self::other::{listener, listener_name, listeners_name};

#[cfg(not(target_os = "linux"))]
mod other {
    use super::*;

    pub fn listener(_num: i32) -> Result<Socket, Error> {
        Err(Error::from(ErrorKind::NotFound))
    }

    pub fn listener_name(_name: &str) -> Result<Socket, Error> {
        Err(Error::from(ErrorKind::NotFound))
    }

    pub fn listeners_name(_name: &str) -> Result<Vec<Socket>, Error> {
        Err(Error::from(ErrorKind::NotFound))
    }
}
