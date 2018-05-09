// Copyright (c) Microsoft. All rights reserved.

use std::io;

use futures::{Async, Poll, Stream};
use tokio_core::net::TcpListener;
#[cfg(unix)]
use tokio_uds::UnixListener;

use util::{IncomingSocketAddr, StreamSelector};

pub enum Incoming {
    Tcp(TcpListener),
    #[cfg(unix)]
    Unix(UnixListener),
}

impl Stream for Incoming {
    type Item = (StreamSelector, IncomingSocketAddr);
    type Error = io::Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        match *self {
            Incoming::Tcp(ref mut listener) => {
                let (stream, addr) = try_nb!(listener.accept());
                Ok(Async::Ready(Some((
                    StreamSelector::Tcp(stream),
                    IncomingSocketAddr::Tcp(addr),
                ))))
            }
            #[cfg(unix)]
            Incoming::Unix(ref mut listener) => {
                let (stream, addr) = try_nb!(listener.accept());
                Ok(Async::Ready(Some((
                    StreamSelector::Unix(stream),
                    IncomingSocketAddr::Unix(addr),
                ))))
            }
        }
    }
}
