// Copyright (c) Microsoft. All rights reserved.
//! This crate contains helpers to make building grpc
//! clients and servers easier. It includes an abstraction
//! for changing the underlying transport. For instance, tcp,
//! unix domain sockets, named pipes, etc.

#[macro_use]
extern crate failure;
#[macro_use]
extern crate futures;
extern crate http;
#[macro_use]
extern crate log;
extern crate tokio_core;
extern crate tokio_io;
extern crate tokio_uds;
extern crate tower;
extern crate tower_grpc;
extern crate tower_h2;
extern crate url;

use futures::{Async, Future, Poll};
use http::request::Request;
use http::response::Response;
use tokio_core::reactor::Handle;
use tower::Service;
use tower_h2::{BoxBody, RecvBody};
use tower_h2::client::{Connection as H2Connection, Handshake, ResponseFuture as H2ResponseFuture};
use url::Url;

mod connect;
mod error;
mod server;

pub use connect::{Connect, TcpConnector};
#[cfg(unix)]
pub use connect::unix::UnixConnector;

pub use error::{Error, ErrorKind};
pub use server::{Listener, TcpListener};
#[cfg(unix)]
pub use server::unix::UnixListener;

/// Represents a client side connection to a service using grpc.
/// This wraps the underlying H2 specific connection.
/// It is generic in Connect (i.e. connecting over tcp, unix, npipe, etc.)
/// Use the `connect` function below to get a Connection.
pub struct Connection<C>
where
    C: Connect,
{
    inner: H2Connection<C::Output, Handle, BoxBody>,
}

impl<C: Connect> Service for Connection<C> {
    type Request = Request<BoxBody>;
    type Response = Response<RecvBody>;
    type Error = Error;
    type Future = ResponseFuture;

    fn poll_ready(&mut self) -> Poll<(), Self::Error> {
        self.inner.poll_ready().map_err(Into::into)
    }

    fn call(&mut self, request: Self::Request) -> Self::Future {
        ResponseFuture {
            inner: self.inner.call(request),
        }
    }
}

impl<C: Connect> Into<H2Connection<C::Output, Handle, BoxBody>> for Connection<C> {
    fn into(self) -> H2Connection<C::Output, Handle, BoxBody> {
        self.inner
    }
}

/// A "future" that resolves to a Connection<C>
pub struct ConnectionFuture<C>
where
    C: Connect,
{
    state: State<C>,
    handle: Handle,
}

impl<C: Connect> ConnectionFuture<C> {
    pub fn new(future: <C as Connect>::Future, handle: Handle) -> ConnectionFuture<C> {
        ConnectionFuture {
            state: State::Connecting(future),
            handle,
        }
    }
}

enum State<C: Connect> {
    Connecting(<C as Connect>::Future),
    Connected(Handshake<C::Output, Handle, BoxBody>),
}

/// A connection is returned after the H2 handshake is completed.
/// Acquiring a connection is an async operation because of the handshake.
impl<C: Connect> Future for ConnectionFuture<C> {
    type Item = Connection<C>;
    type Error = Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        loop {
            let state: State<C>;
            match self.state {
                State::Connecting(ref mut connection) => {
                    let socket = try_ready!(connection.poll());
                    let handshake = H2Connection::handshake(socket, self.handle.clone());
                    state = State::Connected(handshake);
                }
                State::Connected(ref mut handshake) => {
                    let h2 = try_ready!(handshake.poll());
                    let connection = Connection { inner: h2 };
                    return Ok(Async::Ready(connection));
                }
            }
            self.state = state;
        }
    }
}

/// A future which will resolve to a grpc response.
/// This wraps the H2 response future.
pub struct ResponseFuture {
    inner: H2ResponseFuture,
}

impl Future for ResponseFuture {
    type Item = Response<RecvBody>;
    type Error = Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        self.inner.poll().map_err(Into::into)
    }
}

/// Returns a future that will resolve to a Connection to a grpc service.
/// Requires a Connect, address, and handle.
pub fn connect<C: Connect>(mut connect: C, address: Url, handle: Handle) -> ConnectionFuture<C> {
    ConnectionFuture::new(connect.connect(address), handle)
}
