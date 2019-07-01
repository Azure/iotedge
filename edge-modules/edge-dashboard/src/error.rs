// Copyright (c) Microsoft. All rights reserved.

use std::io::Error as IoError;

use edgelet_config::LoadSettingsError;
use edgelet_http_mgmt::*;
use failure::Fail;
use url::ParseError;

#[derive(Fail, Debug)]
pub enum Error {
    #[fail(display = "Config error: {}", _0)]
    Config(LoadSettingsError),

    #[fail(display = "I/O error: {}", _0)]
    Io(IoError),

    #[fail(display = "Invalid request error: {}", _0)]
    InvalidRequest(edgelet_http_mgmt::Error),
}

impl From<LoadSettingsError> for Error {
    fn from(err: LoadSettingsError) -> Self {
        Error::Config(err)
    }
}

impl From<IoError> for Error {
    fn from(err: IoError) -> Self {
        Error::Io(err)
    }
}

impl From<edgelet_http_mgmt::Error> for Error {
    fn from(err: edgelet_http_mgmt::Error) -> Self {
        Error::InvalidRequest(err)
    }
}
