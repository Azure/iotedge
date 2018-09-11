// Copyright (c) Microsoft. All rights reserved.

use std::fmt;

use handlebars::TemplateRenderError;
use hyper_tls::Error as HyperTlsError;
use serde_json::Error as SerdeJsonError;
use snitcher::error::Error as SnitcherError;
use url::ParseError as ParseUrlError;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub enum Error {
    Env(String),
    SerdeJson(SerdeJsonError),
    Handlebars(TemplateRenderError),
    HyperTls(HyperTlsError),
    ParseUrl(ParseUrlError),
    Snitcher(SnitcherError),
    NoReportJsonFound,
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

impl From<ParseUrlError> for Error {
    fn from(err: ParseUrlError) -> Error {
        Error::ParseUrl(err)
    }
}

impl From<HyperTlsError> for Error {
    fn from(err: HyperTlsError) -> Error {
        Error::HyperTls(err)
    }
}

impl From<SnitcherError> for Error {
    fn from(err: SnitcherError) -> Error {
        Error::Snitcher(err)
    }
}
