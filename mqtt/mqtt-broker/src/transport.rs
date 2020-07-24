use std::{
    fmt::Display,
    future::Future,
    net::SocketAddr,
    pin::Pin,
    sync::Arc,
    task::{Context, Poll},
};

use bytes::{Buf, BufMut};
use core::mem::MaybeUninit;
use futures::stream::FuturesUnordered;
use openssl::{
    ssl::{SslAcceptor, SslMethod, SslVerifyMode},
    x509::X509Ref,
};
use tokio::{
    io::{AsyncRead, AsyncWrite},
    net::{TcpListener, TcpStream, ToSocketAddrs},
    stream::Stream,
};
use tokio_openssl::{accept, HandshakeError, SslStream};
use tracing::{debug, error, warn};

use mqtt_broker_core::auth::Certificate;

use crate::{Error, InitializeBrokerError, ServerCertificate};

pub enum Transport {
    Tcp(TcpListener),
    Tls(TcpListener, SslAcceptor),
}

impl Transport {
    pub async fn new_tcp<A>(addr: A) -> Result<Self, InitializeBrokerError>
    where
        A: ToSocketAddrs + Display,
    {
        let tcp = TcpListener::bind(&addr)
            .await
            .map_err(|e| InitializeBrokerError::BindServer(addr.to_string(), e))?;

        Ok(Transport::Tcp(tcp))
    }

    pub async fn new_tls<A>(
        addr: A,
        identity: ServerCertificate,
    ) -> Result<Self, InitializeBrokerError>
    where
        A: ToSocketAddrs + Display,
    {
        let tcp = TcpListener::bind(&addr)
            .await
            .map_err(|e| InitializeBrokerError::BindServer(addr.to_string(), e))?;

        let (private_key, certificate, chain, ca) = identity.into_parts();

        let mut acceptor = SslAcceptor::mozilla_modern(SslMethod::tls())?;
        acceptor.set_private_key(&private_key)?;
        acceptor.set_certificate(&certificate)?;

        if let Some(chain) = chain {
            for cert in chain {
                acceptor.add_extra_chain_cert(cert)?;
            }
        }

        if let Some(ca) = ca {
            acceptor.cert_store_mut().add_cert(ca)?;
        }

        acceptor.set_verify(SslVerifyMode::PEER);

        Ok(Transport::Tls(tcp, acceptor.build()))
    }

    pub fn incoming(self) -> Incoming {
        match self {
            Self::Tcp(listener) => Incoming::Tcp(IncomingTcp::new(listener)),
            Self::Tls(listener, acceptor) => Incoming::Tls(IncomingTls::new(listener, acceptor)),
        }
    }

    pub fn local_addr(&self) -> Result<SocketAddr, InitializeBrokerError> {
        let addr = match self {
            Self::Tcp(listener) => listener.local_addr(),
            Self::Tls(listener, _) => listener.local_addr(),
        };
        addr.map_err(InitializeBrokerError::ConnectionLocalAddress)
    }
}

type HandshakeFuture =
    Pin<Box<dyn Future<Output = Result<SslStream<TcpStream>, HandshakeError<TcpStream>>> + Send>>;

pub enum Incoming {
    Tcp(IncomingTcp),
    Tls(IncomingTls),
}

impl Stream for Incoming {
    type Item = std::io::Result<StreamSelector>;

    fn poll_next(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        match self.get_mut() {
            Self::Tcp(incoming) => Pin::new(incoming).poll_next(cx),
            Self::Tls(incoming) => Pin::new(incoming).poll_next(cx),
        }
    }
}

pub struct IncomingTcp {
    listener: TcpListener,
}

impl IncomingTcp {
    fn new(listener: TcpListener) -> Self {
        Self { listener }
    }
}

impl Stream for IncomingTcp {
    type Item = std::io::Result<StreamSelector>;

    fn poll_next(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        match self.listener.poll_accept(cx) {
            Poll::Ready(Ok((tcp, _))) => match tcp.set_nodelay(true) {
                Ok(()) => {
                    debug!("TCP: Accepted connection from client");
                    Poll::Ready(Some(Ok(StreamSelector::Tcp(tcp))))
                }
                Err(err) => {
                    warn!(
                        "TCP: Dropping client because failed to setup TCP properties: {}",
                        err
                    );
                    Poll::Ready(Some(Err(err)))
                }
            },
            Poll::Ready(Err(err)) => {
                error!(
                    "TCP: Dropping client that failed to completely establish a TCP connection: {}",
                    err
                );
                Poll::Ready(Some(Err(err)))
            }
            Poll::Pending => Poll::Pending,
        }
    }
}

pub struct IncomingTls {
    listener: TcpListener,
    acceptor: Arc<SslAcceptor>,
    connections: FuturesUnordered<HandshakeFuture>,
}

impl IncomingTls {
    fn new(listener: TcpListener, acceptor: SslAcceptor) -> Self {
        Self {
            listener,
            acceptor: Arc::new(acceptor),
            connections: FuturesUnordered::default(),
        }
    }
}

impl Stream for IncomingTls {
    type Item = std::io::Result<StreamSelector>;

    fn poll_next(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        loop {
            match self.listener.poll_accept(cx) {
                Poll::Ready(Ok((stream, _))) => match stream.set_nodelay(true) {
                    Ok(()) => {
                        let acceptor = self.acceptor.clone();
                        self.connections
                            .push(Box::pin(async move { accept(&acceptor, stream).await }));
                    }
                    Err(err) => warn!(
                        "TCP: Dropping client because failed to setup TCP properties: {}",
                        err
                    ),
                },
                Poll::Ready(Err(err)) => warn!(
                    "TCP: Dropping client that failed to completely establish a TCP connection: {}",
                    err
                ),
                Poll::Pending => break,
            }
        }

        loop {
            if self.connections.is_empty() {
                return Poll::Pending;
            }

            match Pin::new(&mut self.connections).poll_next(cx) {
                Poll::Ready(Some(Ok(stream))) => {
                    debug!("TLS: Accepted connection from client");
                    return Poll::Ready(Some(Ok(StreamSelector::Tls(stream))));
                }

                Poll::Ready(Some(Err(err))) => warn!(
                    "TLS: Dropping client that failed to complete a TLS handshake: {}",
                    err
                ),

                Poll::Ready(None) => {
                    debug!("TLS: Shutting down web server");
                    return Poll::Ready(None);
                }

                Poll::Pending => return Poll::Pending,
            }
        }
    }
}

pub enum StreamSelector {
    Tcp(TcpStream),
    Tls(SslStream<TcpStream>),
}

pub trait GetPeerInfo {
    type Certificate;

    fn peer_certificate(&self) -> Result<Option<Self::Certificate>, Error>;

    fn peer_cert_chain(&self) -> Result<Option<Vec<Self::Certificate>>, Error>;

    fn peer_addr(&self) -> Result<SocketAddr, Error>;
}

impl GetPeerInfo for StreamSelector {
    type Certificate = Certificate;

    fn peer_certificate(&self) -> Result<Option<Self::Certificate>, Error> {
        match self {
            Self::Tcp(_) => Ok(None),
            Self::Tls(stream) => stream
                .ssl()
                .peer_certificate()
                .map(|cert| stringify(cert.as_ref()))
                .transpose(),
        }
    }

    fn peer_cert_chain(&self) -> Result<Option<Vec<Self::Certificate>>, Error> {
        match self {
            Self::Tcp(_) => Ok(None),
            Self::Tls(stream) => stream
                .ssl()
                .peer_cert_chain()
                .map(|chain| chain.iter().map(stringify).collect())
                .transpose(),
        }
    }

    fn peer_addr(&self) -> Result<SocketAddr, Error> {
        let stream = match self {
            Self::Tcp(stream) => &stream,
            Self::Tls(stream) => stream.get_ref(),
        };

        stream.peer_addr().map_err(Error::PeerAddr)
    }
}

fn stringify(cert: &X509Ref) -> Result<Certificate, Error> {
    let pem = cert
        .to_pem()
        .map_err(|e| Error::PeerCertificate(Box::new(e)))?;

    let pem = String::from_utf8(pem).map_err(|e| Error::PeerCertificate(Box::new(e)))?;
    Ok(Certificate::from(pem))
}

impl AsyncRead for StreamSelector {
    #[inline]
    unsafe fn prepare_uninitialized_buffer(&self, buf: &mut [MaybeUninit<u8>]) -> bool {
        match self {
            Self::Tcp(stream) => stream.prepare_uninitialized_buffer(buf),
            Self::Tls(stream) => stream.prepare_uninitialized_buffer(buf),
        }
    }

    #[inline]
    fn poll_read_buf<B: BufMut>(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut B,
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Tcp(stream) => Pin::new(stream).poll_read_buf(cx, buf),
            Self::Tls(stream) => Pin::new(stream).poll_read_buf(cx, buf),
        }
    }

    fn poll_read(
        self: std::pin::Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut [u8],
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Tcp(stream) => Pin::new(stream).poll_read(cx, buf),
            Self::Tls(stream) => Pin::new(stream).poll_read(cx, buf),
        }
    }
}

impl AsyncWrite for StreamSelector {
    fn poll_write(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &[u8],
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Tcp(stream) => Pin::new(stream).poll_write(cx, buf),
            Self::Tls(stream) => Pin::new(stream).poll_write(cx, buf),
        }
    }

    fn poll_write_buf<B: Buf>(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut B,
    ) -> Poll<std::io::Result<usize>> {
        match self.get_mut() {
            Self::Tcp(stream) => Pin::new(stream).poll_write_buf(cx, buf),
            Self::Tls(stream) => Pin::new(stream).poll_write_buf(cx, buf),
        }
    }

    #[inline]
    fn poll_flush(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<std::io::Result<()>> {
        match self.get_mut() {
            Self::Tcp(stream) => Pin::new(stream).poll_flush(cx),
            Self::Tls(stream) => Pin::new(stream).poll_flush(cx),
        }
    }

    fn poll_shutdown(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<std::io::Result<()>> {
        match self.get_mut() {
            Self::Tcp(stream) => Pin::new(stream).poll_shutdown(cx),
            Self::Tls(stream) => Pin::new(stream).poll_shutdown(cx),
        }
    }
}
