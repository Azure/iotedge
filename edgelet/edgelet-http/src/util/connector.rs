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

use failure::ResultExt;
use futures::{future, Future};
use hyper::client::connect::{Connect, Connected, Destination};
use hyper::client::HttpConnector;
use hyper::Uri;
#[cfg(windows)]
use hyper_named_pipe::{PipeConnector, Uri as PipeUri};
#[cfg(unix)]
use hyperlocal::{UnixConnector, Uri as HyperlocalUri};
#[cfg(windows)]
use hyperlocal_windows::{UnixConnector, Uri as HyperlocalUri};
use url::{ParseError, Url};

use edgelet_core::UrlExt;

use crate::error::{Error, ErrorKind, InvalidUrlReason};
use crate::util::{socket_file_exists, StreamSelector};
#[cfg(windows)]
use crate::PIPE_SCHEME;
use crate::{HTTP_SCHEME, UNIX_SCHEME};

#[derive(Clone)]
pub enum UrlConnector {
    Http(HttpConnector),
    #[cfg(windows)]
    Pipe(PipeConnector),
    Unix(UnixConnector),
}

impl UrlConnector {
    pub fn new(url: &Url) -> Result<Self, Error> {
        match url.scheme() {
            #[cfg(windows)]
            PIPE_SCHEME => Ok(UrlConnector::Pipe(PipeConnector)),

            UNIX_SCHEME => {
                let file_path = url
                    .to_uds_file_path()
                    .map_err(|_| ErrorKind::InvalidUrl(url.to_string()))?;
                if socket_file_exists(&file_path) {
                    Ok(UrlConnector::Unix(UnixConnector::new()))
                } else {
                    Err(ErrorKind::InvalidUrlWithReason(
                        url.to_string(),
                        InvalidUrlReason::FileNotFound,
                    )
                    .into())
                }
            }

            HTTP_SCHEME => {
                // NOTE: We are defaulting to using 4 threads here. Is this a good
                //       default? This is what the "hyper" crate uses by default at
                //       this time.
                Ok(UrlConnector::Http(HttpConnector::new(4)))
            }
            _ => Err(ErrorKind::InvalidUrlWithReason(
                url.to_string(),
                InvalidUrlReason::InvalidScheme,
            )
            .into()),
        }
    }

    pub fn build_hyper_uri(scheme: &str, base_path: &str, path: &str) -> Result<Uri, Error> {
        match &*scheme {
            #[cfg(windows)]
            PIPE_SCHEME => Ok(PipeUri::new(base_path, &path)
                .with_context(|_| ErrorKind::MalformedUrl {
                    scheme: scheme.to_string(),
                    base_path: base_path.to_string(),
                    path: path.to_string(),
                })?
                .into()),
            UNIX_SCHEME => Ok(HyperlocalUri::new(base_path, &path).into()),
            HTTP_SCHEME => Ok(Url::parse(base_path)
                .and_then(|base| base.join(path))
                .and_then(|url| url.as_str().parse().map_err(|_| ParseError::IdnaError))
                .with_context(|_| ErrorKind::MalformedUrl {
                    scheme: scheme.to_string(),
                    base_path: base_path.to_string(),
                    path: path.to_string(),
                })?),
            _ => Err(ErrorKind::MalformedUrl {
                scheme: scheme.to_string(),
                base_path: base_path.to_string(),
                path: path.to_string(),
            }
            .into()),
        }
    }
}

impl std::fmt::Debug for UrlConnector {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            UrlConnector::Http(_) => f.debug_struct("Http").finish(),
            #[cfg(windows)]
            UrlConnector::Pipe(_) => f.debug_struct("Pipe").finish(),
            UrlConnector::Unix(_) => f.debug_struct("UnixConnector").finish(),
        }
    }
}

impl Connect for UrlConnector {
    type Transport = StreamSelector;
    type Error = io::Error;
    type Future = Box<dyn Future<Item = (Self::Transport, Connected), Error = Self::Error> + Send>;

    fn connect(&self, dst: Destination) -> Self::Future {
        #[allow(clippy::match_same_arms)]
        match (self, dst.scheme()) {
            (UrlConnector::Http(_), HTTP_SCHEME) => (),

            #[cfg(windows)]
            (UrlConnector::Pipe(_), PIPE_SCHEME) => (),

            (UrlConnector::Unix(_), UNIX_SCHEME) => (),

            (_, scheme) => {
                return Box::new(future::err(io::Error::new(
                    io::ErrorKind::Other,
                    format!("Invalid scheme {}", scheme),
                ))) as Self::Future;
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
    fn invalid_url_scheme() {
        let err = UrlConnector::new(&Url::parse("foo:///this/is/not/valid").unwrap()).unwrap_err();
        assert!(failure::Fail::iter_chain(&err).any(|err| err
            .to_string()
            .contains("URL does not have a recognized scheme")));
    }

    #[test]
    fn invalid_uds_url() {
        let err = match UrlConnector::new(&Url::parse("unix:///this/file/does/not/exist").unwrap())
        {
            Ok(_) => panic!("Expected UrlConnector::new to fail"),
            Err(err) => err,
        };
        if cfg!(windows) {
            assert!(err.to_string().contains("Invalid URL"));
        } else {
            assert!(err.to_string().contains("Socket file could not be found"));
        }
    }

    #[test]
    fn create_uds_succeeds() {
        let file = NamedTempFile::new().unwrap();
        let mut url = Url::from_file_path(file.path()).unwrap();
        url.set_scheme("unix").unwrap();
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
