// Copyright (c) Microsoft. All rights reserved.

use std::io::{self, Read, Write};
use std::pin::Pin;
use std::task::{Context, Poll, Waker};

pub struct StaticStream {
    wrote: bool,
    bytes: io::Cursor<Vec<u8>>,
    waker: Option<Waker>,
}

impl StaticStream {
    pub fn new(bytes: Vec<u8>) -> Self {
        Self {
            wrote: false,
            bytes: io::Cursor::new(bytes),
            waker: None,
        }
    }
}

impl Read for StaticStream {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        self.bytes.read(buf)
    }
}

impl Write for StaticStream {
    fn write(&mut self, buf: &[u8]) -> io::Result<usize> {
        Ok(buf.len())
    }

    fn flush(&mut self) -> io::Result<()> {
        Ok(())
    }
}

impl tokio::io::AsyncRead for StaticStream {
    fn poll_read(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut tokio::io::ReadBuf<'_>,
    ) -> Poll<io::Result<()>> {
        let this = self.get_mut();
        if this.wrote {
            let written = this.read(buf.initialize_unfilled())?;
            buf.advance(written);
            Poll::Ready(Ok(()))
        } else {
            this.waker = Some(cx.waker().clone());
            Poll::Pending
        }
    }
}

impl tokio::io::AsyncWrite for StaticStream {
    fn poll_write(
        self: Pin<&mut Self>,
        _cx: &mut Context<'_>,
        buf: &[u8],
    ) -> Poll<io::Result<usize>> {
        let this = self.get_mut();
        this.wrote = true;
        if let Some(waker) = this.waker.take() {
            waker.wake();
        }
        Poll::Ready(this.write(buf))
    }

    fn poll_flush(self: Pin<&mut Self>, _cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        Poll::Ready(self.get_mut().flush())
    }

    fn poll_shutdown(self: Pin<&mut Self>, _cx: &mut Context<'_>) -> Poll<io::Result<()>> {
        Poll::Ready(Ok(()))
    }
}

impl hyper::client::connect::Connection for StaticStream {
    fn connected(&self) -> hyper::client::connect::Connected {
        hyper::client::connect::Connected::new()
    }
}

#[derive(Clone, Debug)]
pub struct JsonConnector {
    body: Vec<u8>,
}

impl JsonConnector {
    #[must_use]
    pub fn ok(body: &str) -> Self {
        let body = format!(
            "HTTP/1.1 200 OK\r\n\
             Content-Type: application/json; charset=utf-8\r\n\
             Content-Length: {}\r\n\
             \r\n\
             {}",
            body.len(),
            body,
        )
        .into();

        JsonConnector { body }
    }

    #[must_use]
    pub fn not_found(body: &str) -> Self {
        let body = format!(
            "HTTP/1.1 404 Not Found\r\n\
             Content-Type: application/json; charset=utf-8\r\n\
             Content-Length: {}\r\n\
             \r\n\
             {}",
            body.len(),
            body,
        )
        .into();

        JsonConnector { body }
    }
}

impl hyper::service::Service<hyper::Uri> for JsonConnector {
    type Response = StaticStream;
    type Error = std::convert::Infallible;
    type Future = std::future::Ready<Result<Self::Response, Self::Error>>;

    fn poll_ready(&mut self, _cx: &mut Context<'_>) -> Poll<Result<(), Self::Error>> {
        Poll::Ready(Ok(()))
    }

    fn call(&mut self, _req: hyper::Uri) -> Self::Future {
        std::future::ready(Ok(StaticStream::new(self.body.clone())))
    }
}
