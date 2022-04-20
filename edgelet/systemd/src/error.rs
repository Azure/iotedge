// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

#[cfg(target_os = "linux")]
use nix::unistd::Pid;

use crate::Fd;

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[cfg(target_os = "linux")]
    #[error("{0} syscall for socket failed.")]
    Syscall(&'static str),

    #[error("File descriptor not found.")]
    FdNotFound,

    #[error(
        "The number of file descriptors {0} does not match the number of file descriptor names {1}.",
    )]
    NumFdsDoesNotMatchNumFdNames(usize, usize),

    #[error("File descriptor {0} is invalid.")]
    InvalidFd(Fd),

    #[error(
        "Number of file descriptors {1} from environment variable {0} is not a valid value.",
    )]
    InvalidNumFds(String, Fd),

    #[error("Environment variable {0} is set to an invalid value.")]
    InvalidVar(String),

    #[error(
        "Could not parse process ID from environment variable {0}.",
    )]
    ParsePid(String),

    #[error("Socket corresponding to {0} not found.")]
    SocketNotFound(SocketLookupType),

    #[cfg(target_os = "linux")]
    #[error(
        "Based on the environment variable {0}, other environment variables meant for a different process (PID {1}).",
    )]
    WrongProcess(String, Pid),
}

#[derive(Debug)]
pub enum SocketLookupType {
    Fd(Fd),
    Name(String),
}

impl Display for SocketLookupType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SocketLookupType::Fd(fd) => write!(f, "file descriptor {}", fd),
            SocketLookupType::Name(name) => write!(f, "name {}", name),
        }
    }
}
