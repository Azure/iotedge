// Copyright (c) Microsoft. All rights reserved.
//! Docker connector
//!
//! This module contains a wrapper type that can handle different hyper
//! transports for interfacing with the Docker daemon by inspecting the scheme
//! of the URL being used to connect. `DockerConnector` is an enum type that
//! selects an appropriate variant according to the URL scheme. It implements
//! hyper's `Service` trait so it can be used directly with its `Client` type.
//! The `Service` trait's `Response` associated type is a struct named
//! `StreamSelector` which is also defined in this module. `StreamSelector` is
//! an enumeration that switches between a `TcpStream` or a `UnixStream` (or
//! other kinds of streams in the future when we support more protocols) for
//! HTTP and Unix sockets respectively.

use std::io::{Error as IoError, Read, Result as IoResult, Write};
#[cfg(unix)]
use std::path::Path;

use futures::{Future, Poll};
use hyper::{Uri, client::{HttpConnector, Service}};
#[cfg(unix)]
use hyperlocal::{UnixConnector, Uri as HyperlocalUri};
use tokio_core::net::TcpStream;
use tokio_core::reactor::Handle;
use tokio_io::{AsyncRead, AsyncWrite};
#[cfg(unix)]
use tokio_uds::UnixStream;
use url::{ParseError, Url};

use error::{Error, ErrorKind, Result};

#[cfg(unix)]
const UNIX_SCHEME: &str = "unix";
const HTTP_SCHEME: &str = "http";

pub enum DockerConnector {
    Http(HttpConnector),
    #[cfg(unix)]
    Unix(UnixConnector),
}

impl DockerConnector {
    pub fn new(url: &Url, handle: &Handle) -> Result<DockerConnector> {
        match url.scheme() {
            #[cfg(unix)]
            UNIX_SCHEME => {
                if !Path::new(url.path()).exists() {
                    Err(ErrorKind::InvalidUdsUri(url.to_string()))?
                } else {
                    Ok(DockerConnector::Unix(UnixConnector::new(handle.clone())))
                }
            }
            HTTP_SCHEME => {
                // NOTE: We are defaulting to using 4 threads here. Is this a good
                //       default? This is what the "hyper" crate uses by default at
                //       this time.
                Ok(DockerConnector::Http(HttpConnector::new(4, handle)))
            }
            _ => Err(ErrorKind::InvalidDockerUri(url.to_string()))?,
        }
    }

    pub fn build_hyper_uri(scheme: &str, base_path: &str, path: &str) -> Result<Uri> {
        match scheme {
            #[cfg(unix)]
            UNIX_SCHEME => Ok(HyperlocalUri::new(base_path, path).into()),
            HTTP_SCHEME => Ok(Url::parse(base_path)
                .and_then(|base| base.join(path))
                .and_then(|url| url.as_str().parse().map_err(|_| ParseError::IdnaError))
                .map_err(Error::from)?),
            _ => Err(ErrorKind::UrlParse)?,
        }
    }
}

pub enum StreamSelector {
    Tcp(TcpStream),
    #[cfg(unix)]
    Unix(UnixStream),
}

impl Read for StreamSelector {
    fn read(&mut self, buf: &mut [u8]) -> IoResult<usize> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.read(buf),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => stream.read(buf),
        }
    }
}

impl Write for StreamSelector {
    fn write(&mut self, buf: &[u8]) -> IoResult<usize> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.write(buf),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => stream.write(buf),
        }
    }

    fn flush(&mut self) -> IoResult<()> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.flush(),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => stream.flush(),
        }
    }
}

impl AsyncRead for StreamSelector {}

impl AsyncWrite for StreamSelector {
    fn shutdown(&mut self) -> Poll<(), IoError> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => <&TcpStream>::shutdown(&mut &*stream),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => <&UnixStream>::shutdown(&mut &*stream),
        }
    }
}

impl Service for DockerConnector {
    type Request = Uri;
    type Response = StreamSelector;
    type Error = IoError;
    type Future = Box<Future<Item = Self::Response, Error = Self::Error>>;

    fn call(&self, uri: Uri) -> Self::Future {
        match *self {
            DockerConnector::Http(ref connector) => Box::new(
                connector
                    .call(uri)
                    .and_then(|tcp_stream| Ok(StreamSelector::Tcp(tcp_stream))),
            ) as Self::Future,

            #[cfg(unix)]
            DockerConnector::Unix(ref connector) => Box::new(
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

    use connector::DockerConnector;

    #[test]
    #[should_panic(expected = "Invalid docker URI")]
    fn invalid_url_scheme() {
        let core = Core::new().unwrap();
        let _connector = DockerConnector::new(
            &Url::parse("foo:///this/is/not/valid").unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[cfg(unix)]
    #[test]
    #[should_panic(expected = "Invalid unix domain socket URI")]
    fn invalid_uds_url() {
        let core = Core::new().unwrap();
        let _connector = DockerConnector::new(
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
        let _connector = DockerConnector::new(
            &Url::parse(&format!("unix://{}", file_path)).unwrap(),
            &core.handle(),
        ).unwrap();
    }

    #[test]
    fn create_http_succeeds() {
        let core = Core::new().unwrap();
        let _connector = DockerConnector::new(
            &Url::parse("http://localhost:2375").unwrap(),
            &core.handle(),
        ).unwrap();
    }
}
