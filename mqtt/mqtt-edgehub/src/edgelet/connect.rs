use std::{
    error::Error as StdError,
    mem::MaybeUninit,
    pin::Pin,
    task::{Context, Poll},
};

use bytes::{Buf, BufMut};
use futures_util::{future::BoxFuture, FutureExt};
use http::Uri;
use hyper::client::{connect::Connection, HttpConnector};
use hyperlocal::UnixConnector;
use tokio::io::{AsyncRead, AsyncWrite};
use tower_service::Service;

#[derive(Debug, Clone)]
pub enum Connector {
    Unix(UnixConnector),
    Http(HttpConnector),
}

impl Service<Uri> for Connector {
    type Response = Stream;
    type Error = Box<dyn StdError + Send + Sync + 'static>;
    type Future = BoxFuture<'static, Result<Self::Response, Self::Error>>;

    fn call(&mut self, req: Uri) -> Self::Future {
        match self {
            Connector::Unix(connector) => {
                let fut = connector
                    .call(req)
                    .map(|stream| stream.map(Stream::Unix).map_err(|e| Box::new(e).into()));
                Box::pin(fut)
            }
            Connector::Http(connector) => {
                let fut = connector
                    .call(req)
                    .map(|stream| stream.map(Stream::Http).map_err(|e| Box::new(e).into()));
                Box::pin(fut)
            }
        }
    }

    fn poll_ready(&mut self, cx: &mut Context<'_>) -> Poll<Result<(), Self::Error>> {
        match self {
            Connector::Unix(connector) => connector.poll_ready(cx).map_err(|e| Box::new(e).into()),
            Connector::Http(connector) => connector.poll_ready(cx).map_err(|e| Box::new(e).into()),
        }
    }
}

pub enum Stream {
    Unix(<UnixConnector as Service<Uri>>::Response),
    Http(<HttpConnector as Service<Uri>>::Response),
}

impl AsyncRead for Stream {
    #[inline]
    unsafe fn prepare_uninitialized_buffer(&self, buf: &mut [MaybeUninit<u8>]) -> bool {
        match self {
            Self::Unix(stream) => stream.prepare_uninitialized_buffer(buf),
            Self::Http(stream) => stream.prepare_uninitialized_buffer(buf),
        }
    }

    #[inline]
    fn poll_read_buf<B: BufMut>(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut B,
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_read_buf(cx, buf),
            Self::Http(stream) => Pin::new(stream).poll_read_buf(cx, buf),
        }
    }

    fn poll_read(
        self: std::pin::Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut [u8],
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_read(cx, buf),
            Self::Http(stream) => Pin::new(stream).poll_read(cx, buf),
        }
    }
}

impl AsyncWrite for Stream {
    fn poll_write(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &[u8],
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_write(cx, buf),
            Self::Http(stream) => Pin::new(stream).poll_write(cx, buf),
        }
    }

    fn poll_write_buf<B: Buf>(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut B,
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_write_buf(cx, buf),
            Self::Http(stream) => Pin::new(stream).poll_write_buf(cx, buf),
        }
    }

    #[inline]
    fn poll_flush(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<std::io::Result<()>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_flush(cx),
            Self::Http(stream) => Pin::new(stream).poll_flush(cx),
        }
    }

    fn poll_shutdown(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<std::io::Result<()>> {
        match self.get_mut() {
            Self::Unix(stream) => Pin::new(stream).poll_shutdown(cx),
            Self::Http(stream) => Pin::new(stream).poll_shutdown(cx),
        }
    }
}

impl Connection for Stream {
    fn connected(&self) -> hyper::client::connect::Connected {
        match self {
            Stream::Unix(stream) => stream.connected(),
            Stream::Http(stream) => stream.connected(),
        }
    }
}
