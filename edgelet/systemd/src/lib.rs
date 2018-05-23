// Copyright (c) Microsoft. All rights reserved.

extern crate failure;
#[macro_use]
extern crate failure_derive;
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
pub use self::linux::{listener, listeners};

#[cfg(not(target_os = "linux"))]
pub use self::other::{listener, listeners};

#[cfg(not(target_os = "linux"))]
mod other {
    use super::*;

    pub fn listener(_name: &str) -> Result<Socket, Error> {
        Err(Error::from(ErrorKind::NotFound))
    }

    pub fn listeners(_name: &str) -> Result<Vec<Socket>, Error> {
        Err(Error::from(ErrorKind::NotFound))
    }
}
