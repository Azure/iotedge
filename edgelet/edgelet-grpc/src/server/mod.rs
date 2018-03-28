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
use std::fmt::{self, Debug};
use std::net::{SocketAddr, ToSocketAddrs};

use futures::{future, Future, Poll, Stream};
use http::request::Request;
use http::response::Response;
use tokio_core::net::{Incoming, TcpListener as TokioTcpListener, TcpStream};
use tokio_core::reactor::{Core, Handle};
use tokio_io::{AsyncRead, AsyncWrite};
use tower::NewService;
use tower_h2::{Body, RecvBody, Server as H2Server};
use url::Url;

use error::Error;

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
    type Connections: Stream<Item = (Self::Socket, Self::Addr), Error = io::Error> + 'static;

    /// Returns a `Server` which can later be started by calling `run`.
    fn bind<S, B>(address: Url, new_service: S) -> Result<Server<Self, S, B>, io::Error>
    where
        S: NewService<Request = Request<RecvBody>, Response = Response<B>> + Debug + 'static,
        S::InitError: Debug,
        S::Error: Debug,
        B: Body + 'static,
    {
        let core = Core::new()?;
        let handle = core.handle();
        Self::bind_handle(address, &handle, new_service)
    }

    fn bind_handle<S, B>(
        address: Url,
        handle: &Handle,
        new_service: S,
    ) -> Result<Server<Self, S, B>, io::Error>
    where
        S: NewService<Request = Request<RecvBody>, Response = Response<B>> + Debug + 'static,
        S::InitError: Debug,
        S::Error: Debug,
        B: Body + 'static;

    /// Returns an asynchronous stream of connections. The connections
    /// represent clients connecting to the server.
    fn incoming(self) -> Self::Connections;
}

#[derive(Debug)]
pub struct Server<L, S, B>
where
    L: Listener,
    S: NewService<Request = Request<RecvBody>, Response = Response<B>> + Debug + 'static,
    S::InitError: Debug,
    S::Error: Debug,
    B: Body + 'static,
{
    new_service: S,
    handle: Handle,
    listener: L,
}

pub struct Run(Box<Future<Item = (), Error = Error> + 'static>);

impl fmt::Debug for Run {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        f.debug_struct("Run").finish()
    }
}

impl Future for Run {
    type Item = ();
    type Error = Error;

    fn poll(&mut self) -> Poll<(), Error> {
        self.0.poll()
    }
}

impl<L, S, B> Server<L, S, B>
where
    L: Listener,
    S: NewService<Request = Request<RecvBody>, Response = Response<B>> + Debug + 'static,
    S::InitError: Debug,
    S::Error: Debug,
    B: Body + 'static,
{
    pub fn run(self) -> Run {
        self.run_until(future::empty())
    }

    pub fn run_until<F>(self, shutdown_signal: F) -> Run
    where
        F: Future<Item = (), Error = ()> + 'static,
    {
        let Server {
            new_service,
            handle,
            listener,
        } = self;
        let h2 = H2Server::new(new_service, Default::default(), handle.clone());

        // Setup the listening server
        let server = listener.incoming().for_each(move |(sock, _)| {
            let serve = h2.serve(sock)
                .map_err(|e| error!("error serving client connection: {:?}", e));
            handle.spawn(serve);
            Ok(())
        });

        // We don't care if the shut_down signal errors.
        // Swallow the error.
        let shutdown_signal = shutdown_signal.then(|_| Ok(()));

        // Main execution.
        // Use select to wait for either `incoming` or `f` to resolve.
        let main_execution = shutdown_signal
            .select(server)
            .then(move |result| match result {
                Ok(((), _incoming)) => future::ok(()),
                Err((e, _other)) => future::err(e.into()),
            });
        Run(Box::new(main_execution))
    }
}

/// A Listener that accepts connections over TCP.
#[derive(Debug)]
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

    fn bind_handle<S, B>(
        address: Url,
        handle: &Handle,
        new_service: S,
    ) -> Result<Server<Self, S, B>, io::Error>
    where
        S: NewService<Request = Request<RecvBody>, Response = Response<B>> + Debug + 'static,
        S::InitError: Debug,
        S::Error: Debug,
        B: Body + 'static,
    {
        let addr = address.to_socket_addrs()?.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::Other, format!("Invalid URL: {}", address))
        })?;
        let listener = TokioTcpListener::bind(&addr, &handle).map(TcpListener::new)?;

        Ok(Server {
            new_service,
            handle: handle.clone(),
            listener,
        })
    }

    fn incoming(self) -> Self::Connections {
        self.inner.incoming()
    }
}
