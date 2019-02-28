// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::module_name_repetitions,
    clippy::similar_names,
    clippy::use_self
)]

extern crate bytes;
extern crate chrono;
extern crate edgelet_core;
extern crate failure;
extern crate futures;
extern crate hyper;
#[cfg(windows)]
extern crate hyper_named_pipe;
extern crate hyper_proxy;
extern crate hyper_tls;
#[cfg(unix)]
extern crate hyperlocal;
#[cfg(windows)]
extern crate hyperlocal_windows;
#[cfg(target_os = "linux")]
#[cfg(unix)]
extern crate libc;
#[macro_use]
extern crate log;
#[cfg(windows)]
extern crate mio_uds_windows;
#[cfg(unix)]
extern crate nix;
extern crate percent_encoding;
extern crate regex;
#[cfg(unix)]
#[macro_use]
extern crate scopeguard;
extern crate serde;
#[macro_use]
extern crate serde_json;
extern crate systemd;
#[cfg(test)]
#[cfg(windows)]
extern crate tempdir;
#[cfg(test)]
extern crate tempfile;
extern crate tokio;
#[cfg(windows)]
extern crate tokio_named_pipe;
#[cfg(unix)]
extern crate tokio_uds;
#[cfg(windows)]
extern crate tokio_uds_windows;
extern crate typed_headers;
extern crate url;
#[cfg(windows)]
extern crate winapi;

extern crate edgelet_utils;

#[cfg(unix)]
use std::net;
use std::net::ToSocketAddrs;
#[cfg(unix)]
use std::os::unix::io::FromRawFd;
use std::sync::Arc;

use failure::{Fail, ResultExt};
use futures::{future, Future, Poll, Stream};
use hyper::server::conn::Http;
use hyper::service::{NewService, Service};
use hyper::{Body, Response};
use log::Level;
#[cfg(unix)]
use systemd::Socket;
use tokio::net::TcpListener;
#[cfg(unix)]
use tokio_uds::UnixListener;
use url::Url;

use edgelet_core::{UrlExt, UNIX_SCHEME};
use edgelet_utils::log_failure;

pub mod authorization;
pub mod client;
pub mod error;
pub mod logging;
mod pid;
pub mod route;
mod unix;
mod util;
mod version;

pub use self::error::{BindListenerType, Error, ErrorKind, InvalidUrlReason};
pub use self::util::proxy::MaybeProxyClient;
pub use self::util::UrlConnector;
pub use self::version::{Version, API_VERSION};

use self::pid::PidService;
use self::util::incoming::Incoming;

const HTTP_SCHEME: &str = "http";
#[cfg(windows)]
const PIPE_SCHEME: &str = "npipe";
const TCP_SCHEME: &str = "tcp";
#[cfg(unix)]
const FD_SCHEME: &str = "fd";

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}

impl IntoResponse for Response<Body> {
    fn into_response(self) -> Response<Body> {
        self
    }
}

pub struct Run(Box<Future<Item = (), Error = Error> + Send + 'static>);

impl Future for Run {
    type Item = ();
    type Error = Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        self.0.poll()
    }
}

pub struct Server<S> {
    protocol: Http,
    new_service: S,
    incoming: Incoming,
}

impl<S> Server<S>
where
    S: NewService<ReqBody = Body, ResBody = Body> + Send + 'static,
    <S as NewService>::Future: Send + 'static,
    <S as NewService>::Service: Send + 'static,
    // <S as NewService>::InitError: std::error::Error + Send + Sync + 'static,
    <S as NewService>::InitError: Fail,
    <<S as NewService>::Service as Service>::Future: Send + 'static,
{
    pub fn run(self) -> Run {
        self.run_until(future::empty())
    }

    pub fn run_until<F>(self, shutdown_signal: F) -> Run
    where
        F: Future<Item = (), Error = ()> + Send + 'static,
    {
        let Server {
            protocol,
            new_service,
            incoming,
        } = self;

        let protocol = Arc::new(protocol);

        let srv = incoming.for_each(move |(socket, addr)| {
            let protocol = protocol.clone();

            debug!("accepted new connection ({})", addr);
            let pid = socket.pid()?;
            let fut = new_service
                .new_service()
                .then(move |srv| match srv {
                    Ok(srv) => Ok((srv, addr)),
                    Err(err) => {
                        error!("server connection error: ({})", addr);
                        log_failure(Level::Error, &err);
                        Err(())
                    }
                })
                .and_then(move |(srv, addr)| {
                    let service = PidService::new(pid, srv);
                    protocol
                        .serve_connection(socket, service)
                        .then(move |result| match result {
                            Ok(_) => Ok(()),
                            Err(err) => {
                                error!("server connection error: ({})", addr);
                                log_failure(Level::Error, &err);
                                Err(())
                            }
                        })
                });
            tokio::spawn(fut);
            Ok(())
        });

        // We don't care if the shut_down signal errors.
        // Swallow the error.
        let shutdown_signal = shutdown_signal.then(|_| Ok(()));

        // Main execution
        // Use select to wait for either `incoming` or `f` to resolve.
        let main_execution = shutdown_signal
            .select(srv)
            .then(move |result| match result {
                Ok(((), _other)) => Ok(()),
                Err((e, _other)) => Err(Error::from(e.context(ErrorKind::ServiceError))),
            });

        Run(Box::new(main_execution))
    }
}

pub trait HyperExt {
    fn bind_url<S>(&self, url: Url, new_service: S) -> Result<Server<S>, Error>
    where
        S: NewService<ReqBody = Body>;
}

impl HyperExt for Http {
    fn bind_url<S>(&self, url: Url, new_service: S) -> Result<Server<S>, Error>
    where
        S: NewService<ReqBody = Body>,
    {
        let incoming = match url.scheme() {
            HTTP_SCHEME | TCP_SCHEME => {
                let addr = url
                    .to_socket_addrs()
                    .context(ErrorKind::InvalidUrl(url.to_string()))?
                    .next()
                    .ok_or_else(|| {
                        ErrorKind::InvalidUrlWithReason(
                            url.to_string(),
                            InvalidUrlReason::NoAddress,
                        )
                    })?;

                let listener = TcpListener::bind(&addr)
                    .with_context(|_| ErrorKind::BindListener(BindListenerType::Address(addr)))?;
                Incoming::Tcp(listener)
            }
            UNIX_SCHEME => {
                let path = url
                    .to_uds_file_path()
                    .map_err(|_| ErrorKind::InvalidUrl(url.to_string()))?;
                unix::listener(path)?
            }
            #[cfg(unix)]
            FD_SCHEME => {
                let host = url.host_str().ok_or_else(|| {
                    ErrorKind::InvalidUrlWithReason(url.to_string(), InvalidUrlReason::NoHost)
                })?;

                // Try to parse the host as an FD number, then as an FD name
                let socket = host
                    .parse()
                    .map_err(|_| ())
                    .and_then(|num| systemd::listener(num).map_err(|_| ()))
                    .or_else(|_| systemd::listener_name(host))
                    .with_context(|_| {
                        ErrorKind::InvalidUrlWithReason(
                            url.to_string(),
                            InvalidUrlReason::FdNeitherNumberNorName,
                        )
                    })?;

                match socket {
                    Socket::Inet(fd, _addr) => {
                        let l = unsafe { net::TcpListener::from_raw_fd(fd) };
                        Incoming::Tcp(
                            TcpListener::from_std(l, &Default::default()).with_context(|_| {
                                ErrorKind::BindListener(BindListenerType::Fd(fd))
                            })?,
                        )
                    }
                    Socket::Unix(fd) => {
                        let l = unsafe { ::std::os::unix::net::UnixListener::from_raw_fd(fd) };
                        Incoming::Unix(
                            UnixListener::from_std(l, &Default::default()).with_context(|_| {
                                ErrorKind::BindListener(BindListenerType::Fd(fd))
                            })?,
                        )
                    }
                    Socket::Unknown => Err(ErrorKind::InvalidUrlWithReason(
                        url.to_string(),
                        InvalidUrlReason::UnrecognizedSocket,
                    ))?,
                }
            }
            _ => Err(Error::from(ErrorKind::InvalidUrlWithReason(
                url.to_string(),
                InvalidUrlReason::InvalidScheme,
            )))?,
        };

        Ok(Server {
            protocol: self.clone(),
            new_service,
            incoming,
        })
    }
}
