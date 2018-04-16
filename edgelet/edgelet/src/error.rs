// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};

use config::ConfigError as SettingsError;
use serde_json::Error as JsonError;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail)]
pub enum ErrorKind {
    #[fail(display = "Invalid configuration file")]
    Settings(SettingsError),
    #[fail(display = "Invalid configuration json")]
    Json(JsonError),
}

impl Fail for Error {
    fn cause(&self) -> Option<&Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        Display::fmt(&self.inner, f)
    }
}

impl Error {}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Error {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Error {
        Error { inner }
    }
}

impl From<SettingsError> for Error {
    fn from(err: SettingsError) -> Error {
        Error::from(ErrorKind::Settings(err))
    }
}

impl From<JsonError> for Error {
    fn from(err: JsonError) -> Error {
        Error::from(ErrorKind::Json(err))
    }
}
