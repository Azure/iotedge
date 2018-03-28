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
extern crate tower_http;
extern crate url;

use std::str::FromStr;

use failure::ResultExt;
use futures::{Async, Future, Poll};
use http::request::Request;
use http::response::Response;
use http::uri::{Parts, PathAndQuery, Uri};
use tokio_core::reactor::Handle;
use tower::Service;
use tower_h2::{BoxBody, RecvBody};
use tower_h2::client::{Connection as H2Connection, Handshake, ResponseFuture as H2ResponseFuture};
use tower_http::{add_origin, AddOrigin};
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

const DEFAULT_ORIGIN: &'static str = "http://azure-devices.net";

/// Represents a client side connection to a service using grpc.
/// This wraps the underlying H2 specific connection.
/// It is generic in Connect (i.e. connecting over tcp, unix, npipe, etc.)
/// Use the `connect` function below to get a Connection.
pub struct Connection<C>
where
    C: Connect,
{
    inner: AddOrigin<H2Connection<C::Output, Handle, BoxBody>>,
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

impl<C: Connect> Into<AddOrigin<H2Connection<C::Output, Handle, BoxBody>>> for Connection<C> {
    fn into(self) -> AddOrigin<H2Connection<C::Output, Handle, BoxBody>> {
        self.inner
    }
}

/// A "future" that resolves to a Connection<C>
pub struct ConnectionFuture<C>
where
    C: Connect,
{
    url: Url,
    state: State<C>,
    handle: Handle,
}

impl<C: Connect> ConnectionFuture<C> {
    pub fn new(future: <C as Connect>::Future, url: Url, handle: Handle) -> ConnectionFuture<C> {
        ConnectionFuture {
            url,
            state: State::Connecting(future),
            handle,
        }
    }
}

enum State<C: Connect> {
    Connecting(<C as Connect>::Future),
    Connected(Handshake<C::Output, Handle, BoxBody>),
    Error(Option<Error>),
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

                    // Add the origin for each request
                    let add_origin = Uri::from_str(self.url.as_str())
                        .context(ErrorKind::Url)
                        .and_then(|uri| {
                            uri.scheme_part()
                                .and_then(|scheme| {
                                    uri.authority_part().map(move |authority| {
                                        let mut parts = Parts::default();
                                        parts.scheme = Some(scheme.clone());
                                        parts.authority = Some(authority.clone());
                                        parts.path_and_query = Some(PathAndQuery::from_static(""));
                                        Uri::from_parts(parts)
                                            .context(ErrorKind::Url)
                                    })
                                }).unwrap_or_else(|| Err(ErrorKind::Url.into()))
                        })
                        // Fallback to the default origin on any sort of failure
                        .or_else(|_| DEFAULT_ORIGIN.parse::<Uri>().context(ErrorKind::Url))
                        .and_then(|uri| {
                            add_origin::Builder::new()
                                .uri(uri)
                                .build(h2)
                                .map_err(|_| ErrorKind::Url.into())
                        });

                    state = match add_origin {
                        Ok(add_origin) => return Ok(Async::Ready(Connection { inner: add_origin })),
                        Err(e) => State::Error(Some(e.into())),
                    }
                }
                State::Error(ref mut e) => return Err(e.take().expect("polled more than once")),
            }
            self.state = state;
        }
    }
}

/// A future which will resolve to a grpc response.
/// This wraps the H2 response future.)
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
    let url = address.clone();
    ConnectionFuture::new(connect.connect(address), url, handle)
}
