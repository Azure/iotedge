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
use std::path::Path;

use futures::{future, Future};
use hyper::client::connect::{Connect, Connected, Destination};
use hyper::client::HttpConnector;
use hyper::Uri;
#[cfg(windows)]
use hyper_named_pipe::{PipeConnector, Uri as PipeUri};
use hyperlocal::{UnixConnector, Uri as HyperlocalUri};
use url::{ParseError, Url};

use error::{Error, ErrorKind};
use util::StreamSelector;
use UrlExt;

const UNIX_SCHEME: &str = "unix";
#[cfg(windows)]
const PIPE_SCHEME: &str = "npipe";
const HTTP_SCHEME: &str = "http";

pub enum UrlConnector {
    Http(HttpConnector),
    #[cfg(windows)]
    Pipe(PipeConnector),
    Unix(UnixConnector),
}

fn socket_file_exists(path: &Path) -> bool {
    if cfg!(windows) {
        use std::fs;
        // Unix domain socket files in Windows are reparse points, so path.exists()
        // (which calls fs::metadata(path)) won't work. Use fs::symlink_metadata()
        // instead.
        fs::symlink_metadata(path).is_ok()
    } else {
        path.exists()
    }
}

impl UrlConnector {
    pub fn new(url: &Url) -> Result<UrlConnector, Error> {
        match url.scheme() {
            #[cfg(windows)]
            PIPE_SCHEME => Ok(UrlConnector::Pipe(PipeConnector)),

            UNIX_SCHEME => {
                let file_path = url.to_uds_file_path()?;
                if !socket_file_exists(&file_path) {
                    Err(ErrorKind::InvalidUri(url.to_string()))?
                } else {
                    Ok(UrlConnector::Unix(UnixConnector::new()))
                }
            }

            HTTP_SCHEME => {
                // NOTE: We are defaulting to using 4 threads here. Is this a good
                //       default? This is what the "hyper" crate uses by default at
                //       this time.
                Ok(UrlConnector::Http(HttpConnector::new(4)))
            }
            _ => Err(ErrorKind::InvalidUri(url.to_string()))?,
        }
    }

    pub fn build_hyper_uri(scheme: &str, base_path: &str, path: &str) -> Result<Uri, Error> {
        match scheme {
            #[cfg(windows)]
            PIPE_SCHEME => Ok(PipeUri::new(base_path, path)?.into()),
            UNIX_SCHEME => Ok(HyperlocalUri::new(base_path, path).into()),
            HTTP_SCHEME => Ok(Url::parse(base_path)
                .and_then(|base| base.join(path))
                .and_then(|url| url.as_str().parse().map_err(|_| ParseError::IdnaError))?),
            _ => Err(ErrorKind::UrlParse)?,
        }
    }
}

impl Connect for UrlConnector {
    type Transport = StreamSelector;
    type Error = io::Error;
    type Future = Box<Future<Item = (Self::Transport, Connected), Error = Self::Error> + Send>;

    fn connect(&self, dst: Destination) -> Self::Future {
        match (self, dst.scheme()) {
            (UrlConnector::Http(_), HTTP_SCHEME) => (),

            #[cfg(windows)]
            (UrlConnector::Pipe(_), PIPE_SCHEME) => (),

            (UrlConnector::Unix(_), UNIX_SCHEME) => (),

            (_, scheme) => {
                return Box::new(future::err(io::Error::new(
                    io::ErrorKind::Other,
                    format!("Invalid scheme {}", scheme),
                ))) as Self::Future
            }
        };

        match self {
            UrlConnector::Http(connector) => {
                Box::new(connector.connect(dst).and_then(|(tcp_stream, connected)| {
                    Ok((StreamSelector::Tcp(tcp_stream), connected))
                })) as Self::Future
            }

            #[cfg(windows)]
            UrlConnector::Pipe(connector) => {
                Box::new(connector.connect(dst).and_then(|(pipe_stream, connected)| {
                    Ok((StreamSelector::Pipe(pipe_stream), connected))
                })) as Self::Future
            }

            UrlConnector::Unix(connector) => {
                Box::new(connector.connect(dst).and_then(|(unix_stream, connected)| {
                    Ok((StreamSelector::Unix(unix_stream), connected))
                })) as Self::Future
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use tempfile::NamedTempFile;
    use url::Url;

    use super::*;

    #[test]
    #[should_panic(expected = "Invalid uri")]
    fn invalid_url_scheme() {
        let _connector =
            UrlConnector::new(&Url::parse("foo:///this/is/not/valid").unwrap()).unwrap();
    }

    #[test]
    #[should_panic(expected = "Invalid uri")]
    fn invalid_uds_url() {
        let _connector =
            UrlConnector::new(&Url::parse("unix:///this/file/does/not/exist").unwrap()).unwrap();
    }

    #[test]
    fn create_uds_succeeds() {
        let file = NamedTempFile::new().unwrap();
        let mut url = Url::from_file_path(file.path()).unwrap();
        let _ = url.set_scheme("unix").unwrap();
        let _connector = UrlConnector::new(&url).unwrap();
    }

    #[test]
    fn create_http_succeeds() {
        let _connector = UrlConnector::new(&Url::parse("http://localhost:2375").unwrap()).unwrap();
    }

    #[cfg(windows)]
    #[test]
    fn create_pipe_succeeds() {
        let _connector = UrlConnector::new(&Url::parse("npipe://./pipe/boo").unwrap()).unwrap();
    }
}
