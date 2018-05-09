// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::io::{self, Read, Write};
use std::net::SocketAddr;
#[cfg(unix)]
use std::os::unix::net::SocketAddr as UnixSocketAddr;

use bytes::{Buf, BufMut};
use futures::Poll;
use tokio_core::net::TcpStream;
use tokio_io::{AsyncRead, AsyncWrite};
#[cfg(windows)]
use tokio_named_pipe::PipeStream;
#[cfg(unix)]
use tokio_uds::UnixStream;

pub mod connector;
pub mod incoming;

pub use self::connector::UrlConnector;
pub use self::incoming::Incoming;

pub enum StreamSelector {
    Tcp(TcpStream),
    #[cfg(windows)]
    Pipe(PipeStream),
    #[cfg(unix)]
    Unix(UnixStream),
}

impl Read for StreamSelector {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.read(buf),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => stream.read(buf),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => stream.read(buf),
        }
    }
}

impl Write for StreamSelector {
    fn write(&mut self, buf: &[u8]) -> io::Result<usize> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.write(buf),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => stream.write(buf),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => stream.write(buf),
        }
    }

    fn flush(&mut self) -> io::Result<()> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.flush(),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => stream.flush(),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => stream.flush(),
        }
    }
}

impl AsyncRead for StreamSelector {
    #[inline]
    unsafe fn prepare_uninitialized_buffer(&self, buf: &mut [u8]) -> bool {
        match *self {
            StreamSelector::Tcp(ref stream) => stream.prepare_uninitialized_buffer(buf),
            #[cfg(windows)]
            StreamSelector::Pipe(ref stream) => stream.prepare_uninitialized_buffer(buf),
            #[cfg(unix)]
            StreamSelector::Unix(ref stream) => stream.prepare_uninitialized_buffer(buf),
        }
    }

    #[inline]
    fn read_buf<B: BufMut>(&mut self, buf: &mut B) -> Poll<usize, io::Error> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.read_buf(buf),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => stream.read_buf(buf),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => stream.read_buf(buf),
        }
    }
}

impl AsyncWrite for StreamSelector {
    fn shutdown(&mut self) -> Poll<(), io::Error> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => <&TcpStream>::shutdown(&mut &*stream),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => PipeStream::shutdown(stream),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => <&UnixStream>::shutdown(&mut &*stream),
        }
    }

    #[inline]
    fn write_buf<B: Buf>(&mut self, buf: &mut B) -> Poll<usize, io::Error> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.write_buf(buf),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => stream.write_buf(buf),
            #[cfg(unix)]
            StreamSelector::Unix(ref mut stream) => stream.write_buf(buf),
        }
    }
}

pub enum IncomingSocketAddr {
    Tcp(SocketAddr),
    #[cfg(unix)]
    Unix(UnixSocketAddr),
}

impl fmt::Display for IncomingSocketAddr {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match *self {
            IncomingSocketAddr::Tcp(ref socket) => socket.fmt(f),
            #[cfg(unix)]
            IncomingSocketAddr::Unix(ref socket) => {
                if let Some(path) = socket.as_pathname() {
                    write!(f, "{}", path.display())
                } else {
                    write!(f, "unknown")
                }
            }
        }
    }
}
