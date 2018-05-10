// Copyright (c) Microsoft. All rights reserved.

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
#[cfg(unix)]
extern crate hyperlocal;
#[cfg(unix)]
extern crate libc;
#[macro_use]
extern crate log;
extern crate percent_encoding;
extern crate regex;
extern crate serde;
#[macro_use]
extern crate serde_json;
#[cfg(test)]
extern crate tempfile;
#[macro_use]
extern crate tokio_core;
extern crate tokio_io;
#[cfg(windows)]
extern crate tokio_named_pipe;
#[cfg(unix)]
extern crate tokio_uds;
extern crate url;

#[macro_use]
extern crate edgelet_utils;

#[cfg(unix)]
use std::fs;
use std::io;
use std::net::ToSocketAddrs;
#[cfg(unix)]
use std::path::Path;

use futures::{future, Future, Poll, Stream};
use http::{Request, Response};
use hyper::server::{Http, NewService};
use hyper::{Body, Error as HyperError};
use tokio_core::net::TcpListener;
use tokio_core::reactor::Handle;
#[cfg(unix)]
use tokio_uds::UnixListener;
use url::Url;

pub mod client;
mod compat;
pub mod error;
pub mod logging;
mod pid;
pub mod route;
mod util;
mod version;

pub use self::error::{Error, ErrorKind};
pub use self::util::UrlConnector;
pub use self::version::{ApiVersionService, API_VERSION};

use self::pid::PidService;
use self::util::incoming::Incoming;

const HTTP_SCHEME: &str = "http";
const TCP_SCHEME: &str = "tcp";
#[cfg(unix)]
const UNIX_SCHEME: &str = "unix";

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}

impl IntoResponse for Response<Body> {
    fn into_response(self) -> Response<Body> {
        self
    }
}

pub struct Run(Box<Future<Item = (), Error = HyperError> + 'static>);

impl Future for Run {
    type Item = ();
    type Error = HyperError;

    fn poll(&mut self) -> Poll<(), Self::Error> {
        self.0.poll()
    }
}

pub struct Server<S, B>
where
    B: Stream<Error = HyperError>,
    B::Item: AsRef<[u8]>,
{
    protocol: Http<B::Item>,
    new_service: S,
    handle: Handle,
    incoming: Incoming,
}

impl<S, B> Server<S, B>
where
    S: NewService<Request = Request<Body>, Response = Response<B>, Error = HyperError> + 'static,
    B: Stream<Error = HyperError> + 'static,
    B::Item: AsRef<[u8]>,
{
    pub fn run(self) -> Run {
        self.run_until(future::empty())
    }

    pub fn run_until<F>(self, shutdown_signal: F) -> Run
    where
        F: Future<Item = (), Error = ()> + 'static,
    {
        let Server {
            protocol,
            new_service,
            handle,
            incoming,
        } = self;

        let srv = incoming.for_each(move |(socket, addr)| {
            debug!("accepted new connection ({})", addr);
            let pid = socket.pid()?;
            let srv = new_service.new_service()?;
            let service = PidService::new(pid, srv);
            let fut = protocol
                .serve_connection(socket, self::compat::service(service))
                .map(|_| ())
                .map_err(move |err| error!("server connection error: ({}) {}", addr, err));
            handle.spawn(fut);
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

pub trait HyperExt<B: AsRef<[u8]> + 'static> {
    fn bind_handle<S, Bd>(
        &self,
        url: Url,
        handle: Handle,
        new_service: S,
    ) -> Result<Server<S, Bd>, Error>
    where
        S: NewService<Request = Request<Body>, Response = Response<Bd>, Error = HyperError>
            + 'static,
        Bd: Stream<Item = B, Error = HyperError>;
}

impl<B: AsRef<[u8]> + 'static> HyperExt<B> for Http<B> {
    fn bind_handle<S, Bd>(
        &self,
        url: Url,
        handle: Handle,
        new_service: S,
    ) -> Result<Server<S, Bd>, Error>
    where
        S: NewService<Request = Request<Body>, Response = Response<Bd>, Error = HyperError>
            + 'static,
        Bd: Stream<Item = B, Error = HyperError>,
    {
        let incoming = match url.scheme() {
            HTTP_SCHEME | TCP_SCHEME => {
                let addr = url.to_socket_addrs()?.next().ok_or_else(|| {
                    io::Error::new(io::ErrorKind::Other, format!("Invalid url: {}", url))
                })?;

                let listener = TcpListener::bind(&addr, &handle)?;
                Incoming::Tcp(listener)
            }
            #[cfg(unix)]
            UNIX_SCHEME => {
                let path = url.path();
                if Path::new(path).exists() {
                    fs::remove_file(path)?;
                }
                let listener = UnixListener::bind(path, &handle)?;
                Incoming::Unix(listener)
            }
            _ => Err(Error::from(ErrorKind::InvalidUri(url.to_string())))?,
        };

        Ok(Server {
            protocol: self.clone(),
            new_service,
            handle,
            incoming,
        })
    }
}
