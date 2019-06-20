// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
#[cfg(target_os = "linux")]
use nix::unistd::Pid;

use crate::Fd;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[cfg(target_os = "linux")]
    #[fail(display = "{} syscall for socket failed.", _0)]
    Syscall(&'static str),

    #[fail(display = "File descriptor not found.")]
    FdNotFound,

    #[fail(
        display = "The number of file descriptors {} does not match the number of file descriptor names {}.",
        _0, _1
    )]
    NumFdsDoesNotMatchNumFdNames(usize, usize),

    #[fail(display = "File descriptor {} is invalid.", _0)]
    InvalidFd(Fd),

    #[fail(
        display = "Number of file descriptors {} from environment variable {} is not a valid value.",
        _1, _0
    )]
    InvalidNumFds(String, Fd),

    #[fail(display = "Environment variable {} is set to an invalid value.", _0)]
    InvalidVar(String),

    #[fail(
        display = "Could not parse process ID from environment variable {}.",
        _0
    )]
    ParsePid(String),

    #[fail(display = "Socket corresponding to {} not found.", _0)]
    SocketNotFound(SocketLookupType),

    #[cfg(target_os = "linux")]
    #[fail(
        display = "Based on the environment variable {}, other environment variables meant for a different process (PID {}).",
        _0, _1
    )]
    WrongProcess(String, Pid),
}

impl Fail for Error {
    fn cause(&self) -> Option<&dyn Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        Display::fmt(&self.inner, f)
    }
}

impl Error {
    pub fn new(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }

    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }
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
