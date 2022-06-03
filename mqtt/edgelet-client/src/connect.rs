use std::{
    error::Error as StdError,
    pin::Pin,
    task::{Context, Poll},
};

use futures_util::{future::BoxFuture, FutureExt};
use http::Uri;
use hyper::client::{connect::Connection, HttpConnector};
#[cfg(unix)]
use hyperlocal::UnixConnector;
use tokio::io::{AsyncRead, AsyncWrite};
use tower_service::Service;

/// A wrapper around `hyper::HttpConnector` and `hyperlocal::UnixConnector` for `hyper::Client`.
/// This connector required to support both transports to communicate with edgelet via HTTP and UDS.
/// `hyper::Client` expect as a parameter any type that implements `tower_service::Service<Uri>`.
/// `Connector` just delegates call to underlying connector instances with no additional logic behind.
#[derive(Debug, Clone)]
pub enum Connector {
    #[cfg(unix)]
    Unix(UnixConnector),
    Http(HttpConnector),
}

impl Service<Uri> for Connector {
    type Response = Stream;
    type Error = Box<dyn StdError + Send + Sync + 'static>;
    type Future = BoxFuture<'static, Result<Self::Response, Self::Error>>;

    fn call(&mut self, req: Uri) -> Self::Future {
        match self {
            #[cfg(unix)]
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
            #[cfg(unix)]
            Connector::Unix(connector) => connector.poll_ready(cx).map_err(|e| Box::new(e).into()),
            Connector::Http(connector) => connector.poll_ready(cx).map_err(|e| Box::new(e).into()),
        }
    }
}

/// A wrapper around instance of a `Stream` returned by either `UnixConnector` or `HttpConnector`.
/// The wrapper expected to implement `AsyncRead`, `AsyncWrite` and `Connection` in order to be used
/// by a `hyper::Client`.
/// `Stream` just delegates call to underlying `Stream` instances with no additional logic behind.
pub enum Stream {
    #[cfg(unix)]
    Unix(<UnixConnector as Service<Uri>>::Response),
    Http(<HttpConnector as Service<Uri>>::Response),
}

impl AsyncRead for Stream {
    fn poll_read(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut tokio::io::ReadBuf<'_>,
    ) -> Poll<std::io::Result<()>> {
        match self.get_mut() {
            #[cfg(unix)]
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
    ) -> Poll<Result<usize, std::io::Error>> {
        match self.get_mut() {
            #[cfg(unix)]
            Self::Unix(stream) => Pin::new(stream).poll_write(cx, buf),
            Self::Http(stream) => Pin::new(stream).poll_write(cx, buf),
        }
    }

    fn poll_flush(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Result<(), std::io::Error>> {
        match self.get_mut() {
            #[cfg(unix)]
            Self::Unix(stream) => Pin::new(stream).poll_flush(cx),
            Self::Http(stream) => Pin::new(stream).poll_flush(cx),
        }
    }

    fn poll_shutdown(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
    ) -> Poll<Result<(), std::io::Error>> {
        match self.get_mut() {
            #[cfg(unix)]
            Self::Unix(stream) => Pin::new(stream).poll_shutdown(cx),
            Self::Http(stream) => Pin::new(stream).poll_shutdown(cx),
        }
    }

    fn poll_write_vectored(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        bufs: &[std::io::IoSlice<'_>],
    ) -> Poll<Result<usize, std::io::Error>> {
        match self.get_mut() {
            #[cfg(unix)]
            Self::Unix(stream) => Pin::new(stream).poll_write_vectored(cx, bufs),
            Self::Http(stream) => Pin::new(stream).poll_write_vectored(cx, bufs),
        }
    }

    fn is_write_vectored(&self) -> bool {
        match self {
            #[cfg(unix)]
            Self::Unix(stream) => stream.is_write_vectored(),
            Self::Http(stream) => stream.is_write_vectored(),
        }
    }
}

impl Connection for Stream {
    fn connected(&self) -> hyper::client::connect::Connected {
        match self {
            #[cfg(unix)]
            Stream::Unix(stream) => stream.connected(),
            Stream::Http(stream) => stream.connected(),
        }
    }
}
