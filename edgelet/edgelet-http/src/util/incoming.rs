// Copyright (c) Microsoft. All rights reserved.

use std::io;

use futures::{Poll, Stream};
use tokio::net::TcpListener;
#[cfg(unix)]
use tokio_uds::UnixListener;
#[cfg(windows)]
use tokio_uds_windows::UnixListener;

use crate::util::{IncomingSocketAddr, StreamSelector};

pub enum Incoming {
    Tcp(TcpListener),
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
