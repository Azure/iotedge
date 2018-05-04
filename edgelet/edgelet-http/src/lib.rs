// Copyright (c) Microsoft. All rights reserved.

extern crate chrono;
extern crate failure;
#[macro_use]
extern crate failure_derive;
#[macro_use]
extern crate futures;
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

use futures::{future, Future, Poll, Stream};
use hyper::Error as HyperError;
use hyper::server::{NewService, Request, Response, Serve};
use tokio_core::reactor::Handle;
use tokio_io::{AsyncRead, AsyncWrite};

mod error;
pub mod logging;
pub mod route;
mod version;

pub use error::{Error, ErrorKind};
pub use version::{ApiVersionService, API_VERSION};

pub trait IntoResponse {
    fn into_response(self) -> Response;
}

impl IntoResponse for Response {
    fn into_response(self) -> Response {
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

pub trait Runnable {
    fn run(self, handle: &Handle) -> Run;

    fn run_until<F>(self, handle: &Handle, shutdown_signal: F) -> Run
    where
        F: Future<Item = (), Error = ()> + 'static;
}

impl<I, S, B> Runnable for Serve<I, S>
where
    I: 'static + Stream<Error = io::Error>,
    I::Item: AsyncRead + AsyncWrite,
    S: 'static + NewService<Request = Request, Response = Response<B>, Error = HyperError>,
    B: 'static + Stream<Error = HyperError>,
    B::Item: AsRef<[u8]>,
{
    fn run(self, handle: &Handle) -> Run {
        self.run_until(handle, future::empty())
    }

    fn run_until<F>(self, handle: &Handle, shutdown_signal: F) -> Run
    where
        F: Future<Item = (), Error = ()> + 'static,
    {
        // setup the listening server
        let h2 = handle.clone();
        let server = self.for_each(move |conn| {
            debug!("accepted new connection");
            let serve = conn.map(|_| ())
                .map_err(|err| error!("serve error: {:?}", err));
            h2.spawn(serve);
            Ok(())
        });

        // We don't care if the shut_down signal errors.
        // Swallow the error.
        let shutdown_signal = shutdown_signal.then(|_| Ok(()));

        // Main execution
        // Use select to wait for either `incoming` or `f` to resolve.
        let main_execution = shutdown_signal
            .select(server)
            .then(move |result| match result {
                Ok(((), _incoming)) => future::ok(()),
                Err((e, _other)) => future::err(e),
            });

        Run(Box::new(main_execution))
    }
}
