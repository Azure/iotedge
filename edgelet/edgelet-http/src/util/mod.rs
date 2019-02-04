// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::io::{self, Read, Write};
use std::net::SocketAddr;
#[cfg(unix)]
use std::os::unix::net::SocketAddr as UnixSocketAddr;
use std::path::Path;

use bytes::{Buf, BufMut};
use edgelet_core::pid::Pid;
use futures::Poll;
#[cfg(windows)]
use mio_uds_windows::net::SocketAddr as UnixSocketAddr;
use tokio::io::{AsyncRead, AsyncWrite};
use tokio::net::TcpStream;
#[cfg(windows)]
use tokio_named_pipe::PipeStream;
#[cfg(unix)]
use tokio_uds::UnixStream;
#[cfg(windows)]
use tokio_uds_windows::UnixStream;

use pid::UnixStreamExt;

pub mod connector;
mod hyperwrap;
pub mod incoming;
pub mod proxy;

pub use self::connector::UrlConnector;
pub use self::incoming::Incoming;

pub enum StreamSelector {
    Tcp(TcpStream),
    #[cfg(windows)]
    Pipe(PipeStream),
    Unix(UnixStream),
}

impl StreamSelector {
    #[allow(clippy::match_same_arms)]
    pub fn pid(&self) -> io::Result<Pid> {
        match *self {
            StreamSelector::Tcp(_) => Ok(Pid::Any),
            #[cfg(windows)]
            StreamSelector::Pipe(_) => Ok(Pid::Any),
            StreamSelector::Unix(ref stream) => stream.pid(),
        }
    }
}

impl Read for StreamSelector {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.read(buf),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => stream.read(buf),
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
            StreamSelector::Unix(ref mut stream) => stream.write(buf),
        }
    }

    fn flush(&mut self) -> io::Result<()> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.flush(),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => stream.flush(),
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
            StreamSelector::Unix(ref stream) => stream.prepare_uninitialized_buffer(buf),
        }
    }

    #[inline]
    fn read_buf<B: BufMut>(&mut self, buf: &mut B) -> Poll<usize, io::Error> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.read_buf(buf),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => stream.read_buf(buf),
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
            StreamSelector::Unix(ref mut stream) => <&UnixStream>::shutdown(&mut &*stream),
        }
    }

    #[inline]
    fn write_buf<B: Buf>(&mut self, buf: &mut B) -> Poll<usize, io::Error> {
        match *self {
            StreamSelector::Tcp(ref mut stream) => stream.write_buf(buf),
            #[cfg(windows)]
            StreamSelector::Pipe(ref mut stream) => stream.write_buf(buf),
            StreamSelector::Unix(ref mut stream) => stream.write_buf(buf),
        }
    }
}

pub enum IncomingSocketAddr {
    Tcp(SocketAddr),
    Unix(UnixSocketAddr),
}

impl fmt::Display for IncomingSocketAddr {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
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
    if cfg!(windows) {
        use std::fs;
        // Unix domain socket files in Windows are reparse points, so path.exists()
        // (which calls fs::metadata(path)) won't work. Use fs::symlink_metadata()
        // instead.
        fs::symlink_metadata(path).is_ok()
    } else {
        path.exists()
    }
}

#[cfg(test)]
mod tests {
    #[cfg(windows)]
    use tempdir::TempDir;
    #[cfg(windows)]
    use tokio::runtime::current_thread::Runtime;
    #[cfg(unix)]
    use tokio_uds::UnixStream;
    #[cfg(windows)]
    use tokio_uds_windows::{UnixListener, UnixStream};

    use super::*;

    struct Pair {
        #[cfg(windows)]
        _dir: TempDir,
        #[cfg(windows)]
        _rt: Runtime,

        a: UnixStream,
        b: UnixStream,
    }

    #[cfg(unix)]
    fn socket_pair() -> Pair {
        let (a, b) = UnixStream::pair().unwrap();
        Pair { a, b }
    }

    #[cfg(windows)]
    fn socket_pair() -> Pair {
        // 'pair' not implemented on Windows
        use futures::sync::oneshot;
        use futures::{Future, Stream};
        let dir = TempDir::new("uds").unwrap();
        let addr = dir.path().join("sock");
        let mut rt = Runtime::new().unwrap();
        let server = UnixListener::bind(&addr).unwrap();
        let (tx, rx) = oneshot::channel();
        rt.spawn(
            server
                .incoming()
                .into_future()
                .and_then(move |(sock, _)| {
                    tx.send(sock.unwrap()).unwrap();
                    Ok(())
                })
                .map_err(|e| panic!("err={:?}", e)),
        );

        let a = rt.block_on(UnixStream::connect(&addr)).unwrap();
        let b = rt.block_on(rx).unwrap();
        Pair {
            _dir: dir,
            _rt: rt,
            a,
            b,
        }
    }

    #[test]
    #[cfg_attr(windows, ignore)] // TODO: remove when windows build servers are upgraded to RS5
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
