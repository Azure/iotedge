// Copyright (c) Microsoft. All rights reserved.

use std::io;
use std::sync::Mutex;

use futures::{Poll, Stream};
use tokio::io::{Error as TokioIoError, ErrorKind as TokioIoErrorKind};
use tokio::net::{TcpListener, TcpStream};
use tokio::prelude::*;
use tokio_tls::Accept;
#[cfg(unix)]
use tokio_uds::UnixListener;
#[cfg(windows)]
use tokio_uds_windows::UnixListener;

use crate::util::{IncomingSocketAddr, StreamSelector};

use tokio_tls::TlsAcceptor;

pub enum Incoming {
    Tcp(TcpListener),
    Tls(
        TcpListener,
        TlsAcceptor,
        Mutex<Vec<(Accept<TcpStream>, IncomingSocketAddr)>>,
    ),
    Unix(UnixListener),
}

impl Stream for Incoming {
    type Item = (StreamSelector, IncomingSocketAddr);
    type Error = io::Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        Ok(match *self {
            Incoming::Tcp(ref mut listener) => {
                let accept = match listener.poll_accept() {
                    Ok(accept) => accept,
                    Err(ref e) if e.kind() == io::ErrorKind::WouldBlock => {
                        return Ok(::futures::Async::NotReady);
                    }
                    Err(e) => return Err(e),
                };
                accept.map(|(stream, addr)| {
                    Some((StreamSelector::Tcp(stream), IncomingSocketAddr::Tcp(addr)))
                })
            }
            Incoming::Tls(ref mut listener, ref mut acceptor, ref mut connections) => {
                // check if we have a tcp connection and if we do then kick off TLS handshake
                // and store the future representing that operation in "connections"
                if let Err(err) = listener.poll_accept().map(|val| {
                    if let Async::Ready((tcp_stream, addr)) = val {
                        connections
                            .lock()
                            .expect("Unable to lock the connection mutex")
                            .push((acceptor.accept(tcp_stream), IncomingSocketAddr::Tcp(addr)));
                    }
                }) {
                    return Err(err);
                }

                // Take a read lock on our pending connection list returning a tuple representing an index
                // + the state on the poll
                let mut connections = connections.lock().expect("Unable to lock the connection mutex");
                let val = connections
                    .iter_mut()
                    .map(|(fut, _)| fut.poll())
                    .enumerate()
                    .find(|(_, result)| match result {
                        Ok(v) => v.is_ready(),
                        Err(_) => true,
                    });

                // Validate that the poll is ready, and remove that value. Then return the connection.
                // If no connections are available in connection manager, return Async::NotReady
                match val {
                    Some((i, result)) => {
                        let (_, addr) = connections.remove(i);
                        match result {
                            Ok(Async::Ready(tls_stream)) => {
                                Async::Ready(Some((StreamSelector::TlsConnected(tls_stream), addr)))
                            }
                            Ok(_) => unreachable!(),
                            Err(err) => return Err(TokioIoError::new(TokioIoErrorKind::Other, err)),
                        }
                    }
                    None => Async::NotReady,
                }
            }
            Incoming::Unix(ref mut listener) => {
                let accept = match listener.poll_accept() {
                    Ok(accept) => accept,
                    Err(ref e) if e.kind() == io::ErrorKind::WouldBlock => {
                        return Ok(::futures::Async::NotReady);
                    }
                    Err(e) => return Err(e),
                };
                accept.map(|(stream, addr)| {
                    Some((StreamSelector::Unix(stream), IncomingSocketAddr::Unix(addr)))
                })
            }
        })
    }
}
