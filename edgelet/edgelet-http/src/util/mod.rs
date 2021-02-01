// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::io::{self, Read, Write};
use std::net::SocketAddr;
#[cfg(unix)]
use std::os::unix::net::SocketAddr as UnixSocketAddr;
use std::path::Path;

use bytes::{Buf, BufMut};
use futures::Poll;
use tokio::io::{AsyncRead, AsyncWrite};
use tokio::net::TcpStream;
use tokio_tls::TlsStream;
#[cfg(unix)]
use tokio_uds::UnixStream;

use crate::pid::{Pid, UnixStreamExt};

pub mod connector;
mod hyperwrap;
pub mod incoming;
pub mod proxy;

pub use connector::UrlConnector;
pub use incoming::Incoming;

pub enum StreamSelector {
    Tcp(TcpStream),
    Tls(TlsStream<TcpStream>),
    Unix(UnixStream),
}

impl StreamSelector {
    #[allow(clippy::match_same_arms)]
    pub fn pid(&self) -> io::Result<Pid> {
        match *self {
            StreamSelector::Tcp(_) => Ok(Pid::Any),
            StreamSelector::Tls(_) => Ok(Pid::Any),
            StreamSelector::Unix(ref stream) => stream.pid(),
        }
    }
}

impl Read for StreamSelector {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        match self {
            StreamSelector::Tcp(stream) => stream.read(buf),
            StreamSelector::Tls(stream) => stream.read(buf),
            StreamSelector::Unix(stream) => stream.read(buf),
        }
    }
}

impl Write for StreamSelector {
    fn write(&mut self, buf: &[u8]) -> io::Result<usize> {
        match self {
            StreamSelector::Tcp(stream) => stream.write(buf),
            StreamSelector::Tls(stream) => stream.write(buf),
            StreamSelector::Unix(stream) => stream.write(buf),
        }
    }

    fn flush(&mut self) -> io::Result<()> {
        match self {
            StreamSelector::Tcp(stream) => stream.flush(),
            StreamSelector::Tls(stream) => stream.flush(),
            StreamSelector::Unix(stream) => stream.flush(),
        }
    }
}

impl AsyncRead for StreamSelector {
    #[inline]
    unsafe fn prepare_uninitialized_buffer(&self, buf: &mut [u8]) -> bool {
        match *self {
            StreamSelector::Tcp(ref stream) => stream.prepare_uninitialized_buffer(buf),
            StreamSelector::Tls(ref stream) => stream.prepare_uninitialized_buffer(buf),
            StreamSelector::Unix(ref stream) => stream.prepare_uninitialized_buffer(buf),
        }
    }

    #[inline]
    fn read_buf<B: BufMut>(&mut self, buf: &mut B) -> Poll<usize, io::Error> {
        match self {
            StreamSelector::Tcp(stream) => stream.read_buf(buf),
            StreamSelector::Tls(stream) => stream.read_buf(buf),
            StreamSelector::Unix(stream) => stream.read_buf(buf),
        }
    }
}

impl AsyncWrite for StreamSelector {
    fn shutdown(&mut self) -> Poll<(), io::Error> {
        match self {
            StreamSelector::Tcp(stream) => AsyncWrite::shutdown(stream),
            StreamSelector::Tls(stream) => TlsStream::shutdown(stream),
            StreamSelector::Unix(stream) => AsyncWrite::shutdown(stream),
        }
    }

    #[inline]
    fn write_buf<B: Buf>(&mut self, buf: &mut B) -> Poll<usize, io::Error> {
        match self {
            StreamSelector::Tcp(stream) => stream.write_buf(buf),
            StreamSelector::Tls(stream) => stream.write_buf(buf),
            StreamSelector::Unix(stream) => stream.write_buf(buf),
        }
    }
}

pub enum IncomingSocketAddr {
    Tcp(SocketAddr),
    Unix(UnixSocketAddr),
}

impl fmt::Display for IncomingSocketAddr {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match *self {
            IncomingSocketAddr::Tcp(ref socket) => socket.fmt(f),
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

pub fn socket_file_exists(path: &Path) -> bool {
    path.exists()
}

#[cfg(test)]
mod tests {
    use tokio_uds::UnixStream;

    use super::{Pid, UnixStreamExt};

    struct Pair {
        a: UnixStream,
        b: UnixStream,
    }

    fn socket_pair() -> Pair {
        let (a, b) = UnixStream::pair().unwrap();
        Pair { a, b }
    }

    #[test]
    fn test_pid() {
        let pair = socket_pair();
        assert_eq!(pair.a.pid().unwrap(), pair.b.pid().unwrap());
        match pair.a.pid().unwrap() {
            Pid::None => panic!("no pid 'a'"),
            Pid::Any => panic!("any pid 'a'"),
            Pid::Value(_) => (),
        }
        match pair.b.pid().unwrap() {
            Pid::None => panic!("no pid 'b'"),
            Pid::Any => panic!("any pid 'b'"),
            Pid::Value(_) => (),
        }
    }
}
