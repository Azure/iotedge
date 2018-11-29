// Copyright (c) Microsoft. All rights reserved.

use std::ffi::OsString;
use std::io;
use std::os::windows::io::{FromRawHandle, IntoRawHandle};

use futures::{Async, Future, Poll, Stream};
use hyper::body::Payload;
use hyper::server::conn::{Connection, Http};
use hyper::service::{service_fn, NewService, Service};
use hyper::{Body, Request, Response};
use mio::Ready;
use mio_named_pipes::NamedPipe;
use miow::pipe::NamedPipeBuilder;
use tokio::reactor::{Handle, PollEvented2};

pub fn run_pipe_server<F, R>(
    addr: OsString,
    handler: F,
) -> impl Future<Item = (), Error = io::Error>
where
    F: 'static + Fn(Request<Body>) -> R + Clone + Send + Sync,
    R: 'static + Future<Item = Response<Body>, Error = io::Error> + Send,
{
    let listener = NamedPipe::new(&addr).expect("couldn't create named pipe listener");
    let handle = Default::default();
    let io = PollEvented2::new_with_handle(listener, &handle)
        .expect("couldn't create named pipe listener");

    let serve = Serve {
        incoming: Incoming { addr, handle, io },
        new_service: move || service_fn(handler.clone()),
        protocol: Http::new(),
    };

    serve.for_each(|connecting| {
        connecting
            .then(|connection| {
                let connection = connection.unwrap();
                Ok::<_, hyper::Error>(connection)
            }).flatten()
            .map_err(|e| {
                io::Error::new(
                    io::ErrorKind::Other,
                    format!("failed to serve connection: {}", e),
                )
            })
    })
}

struct Incoming {
    addr: OsString,
    handle: Handle,
    io: PollEvented2<NamedPipe>,
}

impl Stream for Incoming {
    type Item = PollEvented2<NamedPipe>;
    type Error = io::Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        match self.io.get_ref().connect() {
            Ok(()) => {
                let new_listener = NamedPipeBuilder::new(&self.addr).first(false).create()?;
                let new_listener =
                    unsafe { NamedPipe::from_raw_handle(new_listener.into_raw_handle()) };
                let new_io = PollEvented2::new_with_handle(new_listener, &self.handle)?;
                let connected_io = std::mem::replace(&mut self.io, new_io);
                Ok(Async::Ready(Some(connected_io)))
            }

            Err(ref err) if err.kind() == io::ErrorKind::WouldBlock => {
                self.io.clear_read_ready(Ready::readable())?;
                Ok(Async::NotReady)
            }

            Err(err) => Err(err),
        }
    }
}

struct Serve<S> {
    incoming: Incoming,
    new_service: S,
    protocol: Http,
}

impl<S> Stream for Serve<S>
where
    S: NewService<ReqBody = Body>,
{
    type Item = Connecting<S::Future>;
    type Error = <Incoming as Stream>::Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        match self.incoming.poll()? {
            Async::Ready(Some(stream)) => {
                let service_future = self.new_service.new_service();
                Ok(Async::Ready(Some(Connecting {
                    service_future,
                    stream: Some(stream),
                    protocol: self.protocol.clone(),
                })))
            }
            Async::Ready(None) => Ok(Async::Ready(None)),
            Async::NotReady => Ok(Async::NotReady),
        }
    }
}

struct Connecting<F> {
    service_future: F,
    stream: Option<PollEvented2<NamedPipe>>,
    protocol: Http,
}

impl<F> Future for Connecting<F>
where
    F: Future,
    F::Item: Service<ReqBody = Body>,
    <F::Item as Service>::ResBody: Payload,
    <F::Item as Service>::Future: Send + 'static,
{
    type Item = Connection<PollEvented2<NamedPipe>, F::Item>;
    type Error = F::Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        let service = match self.service_future.poll()? {
            Async::Ready(service) => service,
            Async::NotReady => return Ok(Async::NotReady),
        };
        let stream = self.stream.take().expect("polled after complete");
        Ok(Async::Ready(
            self.protocol.serve_connection(stream, service),
        ))
    }
}
