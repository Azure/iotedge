// Copyright (c) Microsoft. All rights reserved.
//! Client side
//!
//! This module contains client-side helpers for grpc.
//! It's main concepts are the Connect trait and the various Connectors.
//! The Connect trait is essentialy an async function from Url to Connection.
//! A Connection in this case is anything that is `AsyncRead` + `AsyncWrite`.
//! This allows the underlying transport to be swapped out for a client.
//! It is the client-side version of the server-side `Listener`.

use std::net::ToSocketAddrs;

use futures::{Async, Future, Poll};
use tokio_core::net::{TcpStream, TcpStreamNew};
use tokio_core::reactor::Handle;
use tokio_io::{AsyncRead, AsyncWrite};
use tower::Service;
use url::{SocketAddrs, Url};

use error::Error;

#[cfg(unix)]
pub mod unix;

// Inspired by the connect handling in hyper
// (https://github.com/hyperium/hyper/blob/master/src/client/connect.rs)
/// A connector creates an Io to a remote address...
///
/// This trait is not implemented directly, and only exists to make
/// the intent clearer. A connnector should implement `Service` with
/// `Request=Uri` and `Response=Io` instead.
pub trait Connect: Service<Request = Url, Error = Error> + 'static {
    /// The connected Io Stream.
    type Output: AsyncRead + AsyncWrite + 'static;
    /// A Future that will resolve to the connected Stream.
    type Future: Future<Item = Self::Output, Error = Error> + 'static;
    /// Connect to a remote address
    fn connect(&mut self, Url) -> <Self as Connect>::Future;
}

impl<T> Connect for T
where
    T: Service<Request = Url, Error = Error> + 'static,
    T::Response: AsyncRead + AsyncWrite,
    T::Future: Future<Error = Error>,
{
    type Output = T::Response;
    type Future = T::Future;

    fn connect(&mut self, url: Url) -> <Self as Connect>::Future {
        self.call(url)
    }
}

/// A Connector using TCP as the underlying transport.
#[derive(Debug)]
pub struct TcpConnector {
    handle: Handle,
}

impl TcpConnector {
    pub fn new(handle: &Handle) -> TcpConnector {
        TcpConnector {
            handle: handle.clone(),
        }
    }
}

impl Service for TcpConnector {
    type Request = Url;
    type Response = TcpStream;
    type Error = Error;
    type Future = TcpConnecting;

    fn poll_ready(&mut self) -> Poll<(), Self::Error> {
        Ok(().into())
    }

    fn call(&mut self, url: Self::Request) -> Self::Future {
        trace!("Tcp::connect({:?})", url);
        TcpConnecting {
            state: State::Init(url),
            handle: self.handle.clone(),
        }
    }
}

pub struct TcpConnecting {
    state: State,
    handle: Handle,
}

enum State {
    Init(Url),
    Connecting(SocketAddrConnecting),
    Error(Option<Error>),
}

impl Future for TcpConnecting {
    type Item = TcpStream;
    type Error = Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        loop {
            let state;
            match self.state {
                State::Init(ref url) => {
                    state = url.to_socket_addrs()
                        .map(|addrs| {
                            State::Connecting(SocketAddrConnecting {
                                addrs,
                                current: None,
                            })
                        })
                        .unwrap_or_else(|err| State::Error(Some(err.into())));
                }
                State::Connecting(ref mut sac) => {
                    let sock = try_ready!(sac.poll(&self.handle));
                    return Ok(Async::Ready(sock));
                }
                State::Error(ref mut e) => return Err(e.take().expect("polled more than once")),
            }
            self.state = state;
        }
    }
}

struct SocketAddrConnecting {
    addrs: SocketAddrs,
    current: Option<TcpStreamNew>,
}

impl SocketAddrConnecting {
    fn poll(&mut self, handle: &Handle) -> Poll<TcpStream, Error> {
        let mut err = None;
        loop {
            if let Some(ref mut current) = self.current {
                match current.poll() {
                    Ok(ok) => return Ok(ok),
                    Err(e) => {
                        trace!("connect error: {:?}", e);
                        err = Some(e);
                        if let Some(addr) = self.addrs.next() {
                            debug!("connecting to {}", addr);
                            *current = TcpStream::connect(&addr, handle);
                            continue;
                        }
                    }
                }
            } else if let Some(addr) = self.addrs.next() {
                debug!("connecting to {}", addr);
                self.current = Some(TcpStream::connect(&addr, handle));
                continue;
            }

            return Err(err.take().expect("missing connect error").into());
        }
    }
}
