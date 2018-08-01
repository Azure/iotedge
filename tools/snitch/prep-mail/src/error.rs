// Copyright (c) Microsoft. All rights reserved.

use std::fmt;

use handlebars::TemplateRenderError;
use serde_json::Error as SerdeJsonError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub enum Error {
    Env(String),
    SerdeJson(SerdeJsonError),
    Handlebars(TemplateRenderError),
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        write!(f, "{:?}", self)
    }
}

impl From<SerdeJsonError> for Error {
    fn from(err: SerdeJsonError) -> Error {
        Error::SerdeJson(err)
    }
}

impl From<TemplateRenderError> for Error {
    fn from(err: TemplateRenderError) -> Error {
        Error::Handlebars(err)
    }
}
