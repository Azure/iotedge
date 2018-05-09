// Copyright (c) Microsoft. All rights reserved.

//! Http connector
//!
//! This module contains a wrapper type that can handle different hyper
//! transports for interfacing with Http services by inspecting the scheme
//! of the URL being used to connect. `UrlConnector` is an enum type that
//! selects an appropriate variant according to the URL scheme. It implements
//! hyper's `Service` trait so it can be used directly with its `Client` type.
//! The `Service` trait's `Response` associated type is a struct named
//! `StreamSelector` which is also defined in this module. `StreamSelector` is
//! an enumeration that switches between a `TcpStream` or a `UnixStream` (or
//! other kinds of streams in the future when we support more protocols) for
//! HTTP and Unix sockets respectively.

use std::io;
#[cfg(unix)]
use std::path::Path;

use futures::Future;
use hyper::Uri;
use hyper::client::{HttpConnector, Service};
#[cfg(windows)]
use hyper_named_pipe::{PipeConnector, Uri as PipeUri};
#[cfg(unix)]
use hyperlocal::{UnixConnector, Uri as HyperlocalUri};
use tokio_core::reactor::Handle;
use url::{ParseError, Url};

use error::{Error, ErrorKind};
use util::StreamSelector;

#[cfg(unix)]
const UNIX_SCHEME: &str = "unix";
#[cfg(windows)]
const PIPE_SCHEME: &str = "npipe";
const HTTP_SCHEME: &str = "http";

pub enum UrlConnector {
    Http(HttpConnector),
    #[cfg(windows)]
    Pipe(PipeConnector),
    #[cfg(unix)]
    Unix(UnixConnector),
}

impl UrlConnector {
    pub fn new(url: &Url, handle: &Handle) -> Result<UrlConnector, Error> {
        match url.scheme() {
            #[cfg(windows)]
            PIPE_SCHEME => Ok(UrlConnector::Pipe(PipeConnector::new(handle.clone()))),

            #[cfg(unix)]
            UNIX_SCHEME => {
                if !Path::new(url.path()).exists() {
                    Err(ErrorKind::InvalidUri(url.to_string()))?
                } else {
                    Ok(UrlConnector::Unix(UnixConnector::new(handle.clone())))
                }
            }

            HTTP_SCHEME => {
                // NOTE: We are defaulting to using 4 threads here. Is this a good
                //       default? This is what the "hyper" crate uses by default at
                //       this time.
                Ok(UrlConnector::Http(HttpConnector::new(4, handle)))
            }
            _ => Err(ErrorKind::InvalidUri(url.to_string()))?,
        }
    }

    pub fn build_hyper_uri(scheme: &str, base_path: &str, path: &str) -> Result<Uri, Error> {
        match scheme {
            #[cfg(windows)]
            PIPE_SCHEME => Ok(PipeUri::new(base_path, path)?.into()),
            #[cfg(unix)]
            UNIX_SCHEME => Ok(HyperlocalUri::new(base_path, path).into()),
            HTTP_SCHEME => Ok(Url::parse(base_path)
                .and_then(|base| base.join(path))
                .and_then(|url| url.as_str().parse().map_err(|_| ParseError::IdnaError))?),
            _ => Err(ErrorKind::UrlParse)?,
        }
    }
}

impl Service for UrlConnector {
    type Request = Uri;
    type Response = StreamSelector;
    type Error = io::Error;
    type Future = Box<Future<Item = Self::Response, Error = Self::Error>>;

    fn call(&self, uri: Uri) -> Self::Future {
        match *self {
            UrlConnector::Http(ref connector) => Box::new(
                connector
                    .call(uri)
                    .and_then(|tcp_stream| Ok(StreamSelector::Tcp(tcp_stream))),
            ) as Self::Future,

            #[cfg(windows)]
            UrlConnector::Pipe(ref connector) => Box::new(
                connector
                    .call(uri)
                    .and_then(|pipe_stream| Ok(StreamSelector::Pipe(pipe_stream))),
            ) as Self::Future,

            #[cfg(unix)]
            UrlConnector::Unix(ref connector) => Box::new(
                connector
                    .call(uri)
                    .and_then(|unix_stream| Ok(StreamSelector::Unix(unix_stream))),
            ) as Self::Future,
        }
    }
}

#[cfg(test)]
mod tests {
    #[cfg(unix)]
    use tempfile::NamedTempFile;
    use tokio_core::reactor::Core;
    use url::Url;

    use super::*;

    #[test]
    #[should_panic(expected = "Invalid uri")]
    fn invalid_url_scheme() {
        let core = Core::new().unwrap();
        let _connector = UrlConnector::new(
            &Url::parse("foo:///this/is/not/valid").unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[cfg(unix)]
    #[test]
    #[should_panic(expected = "Invalid uri")]
    fn invalid_uds_url() {
        let core = Core::new().unwrap();
        let _connector = UrlConnector::new(
            &Url::parse("unix:///this/file/does/not/exist").unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[cfg(unix)]
    #[test]
    fn create_uds_succeeds() {
        let core = Core::new().unwrap();
        let file = NamedTempFile::new().unwrap();
        let file_path = file.path().to_str().unwrap();
        let _connector = UrlConnector::new(
            &Url::parse(&format!("unix://{}", file_path)).unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[test]
    fn create_http_succeeds() {
        let core = Core::new().unwrap();
        let _connector = UrlConnector::new(
            &Url::parse("http://localhost:2375").unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[cfg(windows)]
    #[test]
    fn create_pipe_succeeds() {
        let core = Core::new().unwrap();
        let _connector =
            UrlConnector::new(&Url::parse("npipe://./pipe/boo").unwrap(), &core.handle()).unwrap();
    }
}
