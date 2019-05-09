// Copyright (c) Microsoft. All rights reserved.

use std::io;
#[cfg(unix)]
use std::sync::Mutex;

use futures::{Poll, Stream};
#[cfg(unix)]
use tokio::io::{Error as TokioIoError, ErrorKind as TokioIoErrorKind};
#[cfg(windows)]
use tokio::net::TcpListener;
#[cfg(unix)]
use tokio::net::{TcpListener, TcpStream};
#[cfg(unix)]
use tokio::prelude::*;
#[cfg(unix)]
use tokio_tls::{Accept, TlsAcceptor};
#[cfg(unix)]
use tokio_uds::UnixListener;
#[cfg(windows)]
use tokio_uds_windows::UnixListener;

use crate::util::{IncomingSocketAddr, StreamSelector};

pub enum Incoming {
    Tcp(TcpListener),
    #[cfg(unix)]
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
            #[cfg(unix)]
            Incoming::Tls(ref mut listener, ref mut acceptor, ref mut connections) => {
                // check if we have a tcp connection and if we do then kick off TLS handshake
                // and store the future representing that operation in "connections"
                if let Err(err) = listener.poll_accept().map(|val| {
                    if let Async::Ready((tcp_stream, addr)) = val {
                        connections
                            .lock()
                            .expect("Unable to lock the connections mutex")
                            .push((acceptor.accept(tcp_stream), IncomingSocketAddr::Tcp(addr)));
                    }
                }) {
                    return Err(err);
                }

                let mut connections = connections
                    .lock()
                    .expect("Unable to lock the connections mutex");

                // Look through the connections list for the first connection that is either ready to be
                // passed to the stream selector or has failed and needs the error bubbled up.
                // Return a tuple containing the index and state.
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
                                #[cfg(not(windows))]
                                {
                                    Async::Ready(Some((StreamSelector::Tls(tls_stream), addr)))
                                }
                                #[cfg(windows)]
                                {
                                    Async::Ready(Some(
                                        (Box::new(StreamSelector::Tls(tls_stream), addr)),
                                    ))
                                }
                            }
                            // The prior block included a filter that specifically asked for is_ready state,
                            // so this line is unreachable.
                            Ok(_) => unreachable!(),
                            Err(err) => {
                                return Err(TokioIoError::new(TokioIoErrorKind::Other, err))
                            }
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
