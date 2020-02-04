// Copyright (c) Microsoft. All rights reserved.

use std::io::Error as IoError;

use edgelet_docker::LoadSettingsError;
use failure::Fail;

#[derive(Fail, Debug)]
pub enum Error {
    #[fail(display = "Config error: {}", _0)]
    Config(LoadSettingsError),

    #[fail(display = "I/O error: {}", _0)]
    Io(IoError),
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
