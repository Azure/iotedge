// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]

extern crate bytes;
extern crate chrono;
extern crate edgelet_core;
extern crate failure;
#[macro_use]
extern crate failure_derive;
#[macro_use]
extern crate futures;
extern crate http;
extern crate hyper;
#[cfg(windows)]
extern crate hyper_named_pipe;
extern crate hyper_proxy;
extern crate hyper_tls;
#[cfg(unix)]
extern crate hyperlocal;
#[cfg(target_os = "linux")]
#[cfg(unix)]
extern crate libc;
#[macro_use]
extern crate log;
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
#[cfg(unix)]
#[cfg(test)]
extern crate tempfile;
extern crate tokio;
#[cfg(windows)]
extern crate tokio_named_pipe;
#[cfg(unix)]
extern crate tokio_uds;
extern crate typed_headers;
extern crate url;

#[macro_use]
extern crate edgelet_utils;

use std::io;
#[cfg(unix)]
use std::net;
use std::net::ToSocketAddrs;
#[cfg(unix)]
use std::os::unix::io::FromRawFd;
use std::sync::Arc;

use futures::{future, Future, Poll, Stream};
use hyper::server::conn::Http;
use hyper::service::{NewService, Service};
use hyper::{Body, Error as HyperError, Response};
#[cfg(unix)]
use systemd::Socket;
use tokio::net::TcpListener;
#[cfg(unix)]
use tokio_uds::UnixListener;
use url::Url;

pub mod authorization;
pub mod client;
pub mod error;
pub mod logging;
mod pid;
pub mod route;
mod unix;
mod util;
mod version;

pub use self::error::{Error, ErrorKind};
pub use self::util::proxy::MaybeProxyClient;
pub use self::util::UrlConnector;
pub use self::version::{ApiVersionService, API_VERSION};

use self::pid::PidService;
use self::util::incoming::Incoming;

const HTTP_SCHEME: &str = "http";
const TCP_SCHEME: &str = "tcp";
#[cfg(unix)]
const UNIX_SCHEME: &str = "unix";
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

pub struct Run(Box<Future<Item = (), Error = failure::Error> + Send + 'static>);

impl Future for Run {
    type Item = ();
    type Error = failure::Error;

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
    S: NewService<ReqBody = Body, ResBody = Body, Error = HyperError> + Send + 'static,
    <S as NewService>::Future: Send,
    <S as NewService>::Service: Send,
    <S as NewService>::InitError: std::fmt::Display,
    <<S as NewService>::Service as Service>::Future: Send,
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
                        error!("server connection error: ({}) {}", addr, err);
                        Err(())
                    }
                }).and_then(move |(srv, addr)| {
                    let service = PidService::new(pid, srv);
                    protocol
                        .serve_connection(socket, service)
                        .then(move |result| match result {
                            Ok(_) => Ok(()),
                            Err(err) => {
                                error!("server connection error: ({}) {}", addr, err);
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
                Ok(((), _incoming)) => future::ok(()),
                Err((e, _other)) => future::err(e.into()),
            });

        Run(Box::new(main_execution))
    }
}

pub trait HyperExt {
    fn bind_url<S>(&self, url: Url, new_service: S) -> Result<Server<S>, Error>
    where
        S: NewService<ReqBody = Body> + 'static;
}

impl HyperExt for Http {
    fn bind_url<S>(&self, url: Url, new_service: S) -> Result<Server<S>, Error>
    where
        S: NewService<ReqBody = Body> + 'static,
    {
        let incoming = match url.scheme() {
            HTTP_SCHEME | TCP_SCHEME => {
                let addr = url.to_socket_addrs()?.next().ok_or_else(|| {
                    io::Error::new(io::ErrorKind::Other, format!("Invalid url: {}", url))
                })?;

                let listener = TcpListener::bind(&addr)?;
                Incoming::Tcp(listener)
            }
            #[cfg(unix)]
            UNIX_SCHEME => {
                let path = url.path();
                unix::listener(path)?
            }
            #[cfg(unix)]
            FD_SCHEME => {
                let host = url
                    .host_str()
                    .ok_or_else(|| Error::from(ErrorKind::InvalidUri(url.to_string())))?;
                let socket = host
                    .parse::<usize>()
                    .map_err(Error::from)
                    .and_then(|num| systemd::listener(num).map_err(Error::from))
                    .or_else(|_| systemd::listener_name(host))?;

                match socket {
                    Socket::Inet(fd, _addr) => {
                        let l = unsafe { net::TcpListener::from_raw_fd(fd) };
                        Incoming::Tcp(TcpListener::from_std(l, &Default::default())?)
                    }
                    Socket::Unix(fd) => {
                        let l = unsafe { ::std::os::unix::net::UnixListener::from_raw_fd(fd) };
                        Incoming::Unix(UnixListener::from_std(l, &Default::default())?)
                    }
                    _ => Err(Error::from(ErrorKind::InvalidUri(url.to_string())))?,
                }
            }
            _ => Err(Error::from(ErrorKind::InvalidUri(url.to_string())))?,
        };

        Ok(Server {
            protocol: self.clone(),
            new_service,
            incoming,
        })
    }
}
