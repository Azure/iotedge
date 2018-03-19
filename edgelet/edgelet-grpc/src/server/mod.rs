// Copyright (c) Microsoft. All rights reserved.
//! Server side
//!
//! This modules contains server side helpers for grpc.
//! It's main concepts are a Listener and Server.
//! A Listener represents an item that can accept connections.
//! Calling bind on a Listener associates a Service with an address and
//! returns a Server. A Server can be started by calling `run`. This operation
//! blocks the current thread until the server is closed.

use std::io;
use std::net::{SocketAddr, ToSocketAddrs};

use failure::ResultExt;
use futures::{Future, Stream};
use http::request::Request;
use http::response::Response;
use tokio_core::net::{Incoming, TcpListener as TokioTcpListener, TcpStream};
use tokio_core::reactor::Core;
use tokio_io::{AsyncRead, AsyncWrite};
use tower::NewService;
use tower_h2::{Body, RecvBody, Server as H2Server};
use url::Url;

use error::{Error, ErrorKind};

#[cfg(unix)]
pub mod unix;

/// Represents an item that listens for connections.
/// A connection is `AsyncRead + AsyncWrite`.
pub trait Listener
where
    Self: Sized,
{
    type Addr;
    type Socket: AsyncRead + AsyncWrite + 'static;
    type Connections: Stream<Item = (Self::Socket, Self::Addr), Error = io::Error>;

    /// Returns a `Server` which can later be started by calling `run`.
    fn bind<S, B>(address: Url, new_service: S) -> Result<Server<Self, S, B>, io::Error>
    where
        S: NewService<Request = Request<RecvBody>, Response = Response<B>> + 'static,
        B: Body + 'static;

    /// Returns an asynchronous stream of connections. The connections
    /// represent clients connecting to the server.
    fn incoming(self) -> Self::Connections;
}

pub struct Server<L, S, B>
where
    L: Listener,
    S: NewService<Request = Request<RecvBody>, Response = Response<B>> + 'static,
    B: Body + 'static,
{
    new_service: S,
    core: Core,
    listener: L,
}

impl<L, S, B> Server<L, S, B>
where
    L: Listener,
    S: NewService<Request = Request<RecvBody>, Response = Response<B>> + 'static,
    B: Body + 'static,
{
    pub fn run(mut self) -> Result<(), Error> {
        let handle = self.core.handle();
        let h2 = H2Server::new(self.new_service, Default::default(), handle.clone());
        let server = self.listener.incoming().for_each(|(sock, _)| {
            let serve = h2.serve(sock);
            handle.spawn(serve.map_err(|_| error!("h2 error")));
            Ok(())
        });
        self.core
            .run(server)
            .context::<ErrorKind>(ErrorKind::Io)
            .map_err(From::from)
    }
}

/// A Listener that accepts connections over TCP.
pub struct TcpListener {
    inner: TokioTcpListener,
}

impl TcpListener {
    fn new(inner: TokioTcpListener) -> TcpListener {
        TcpListener { inner }
    }
}

impl Listener for TcpListener {
    type Socket = TcpStream;
    type Addr = SocketAddr;
    type Connections = Incoming;

    fn bind<S, B>(address: Url, new_service: S) -> Result<Server<Self, S, B>, io::Error>
    where
        S: NewService<Request = Request<RecvBody>, Response = Response<B>> + 'static,
        B: Body + 'static,
    {
        let core = Core::new()?;
        let handle = core.handle();

        let addr = address.to_socket_addrs()?.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::Other, format!("Invalid URL: {}", address))
        })?;
        let listener = TokioTcpListener::bind(&addr, &handle).map(TcpListener::new)?;

        Ok(Server {
            new_service,
            core,
            listener,
        })
    }

    fn incoming(self) -> Self::Connections {
        self.inner.incoming()
    }
}
