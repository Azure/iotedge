// Copyright (c) Microsoft. All rights reserved.

extern crate chrono;
extern crate failure;
#[macro_use]
extern crate failure_derive;
#[macro_use]
extern crate futures;
extern crate http;
extern crate hyper;
#[macro_use]
extern crate log;
extern crate regex;
#[macro_use]
extern crate serde_json;
extern crate tokio_core;
extern crate tokio_io;
extern crate url;

use std::io;
use std::net::ToSocketAddrs;

use futures::{future, Future, Poll, Stream};
use http::{Request, Response};
use hyper::{Body, Error as HyperError};
use hyper::server::{Http, NewService};
use tokio_core::net::TcpListener;
use tokio_core::reactor::Handle;
use url::Url;

mod compat;
mod error;
pub mod logging;
pub mod route;
mod version;

pub use error::{Error, ErrorKind};
pub use version::{ApiVersionService, API_VERSION};

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
    listener: TcpListener,
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
            listener,
        } = self;

        let srv = listener.incoming().for_each(move |(socket, addr)| {
            debug!("accepted new connection ({})", addr);
            let service = new_service.new_service()?;
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
    ) -> Result<Server<S, Bd>, HyperError>
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
    ) -> Result<Server<S, Bd>, HyperError>
    where
        S: NewService<Request = Request<Body>, Response = Response<Bd>, Error = HyperError>
            + 'static,
        Bd: Stream<Item = B, Error = HyperError>,
    {
        let addr = url.to_socket_addrs()?
            .next()
            .ok_or_else(|| io::Error::new(io::ErrorKind::Other, format!("Invalid url: {}", url)))?;

        let listener = TcpListener::bind(&addr, &handle)?;

        Ok(Server {
            protocol: self.clone(),
            new_service,
            handle,
            listener,
        })
    }
}
