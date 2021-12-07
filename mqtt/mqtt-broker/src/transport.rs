use std::{
    fmt::Display,
    future::Future,
    net::{SocketAddr, ToSocketAddrs},
    pin::Pin,
    sync::Arc,
    task::{Context, Poll},
};

use futures_util::stream::{FuturesUnordered, Stream};
use openssl::{
    ssl::{Ssl, SslAcceptor, SslMethod, SslOptions, SslVerifyMode},
    x509::X509Ref,
};
use tokio::{
    io::{AsyncRead, AsyncWrite},
    net::{TcpListener, TcpStream},
};
use tokio_openssl::SslStream;
use tracing::{debug, error, warn};

use crate::{auth::Certificate, Error, InitializeBrokerError, ServerCertificate};

/// Represents transport protocol that is exposed to the clients.
pub struct Transport {
    protocol: Protocol,
}

impl Transport {
    /// Creates a new instance of a transport protocol over TCP.
    pub fn new_tcp<A>(addr: A) -> Result<Self, InitializeBrokerError>
    where
        A: ToSocketAddrs + Display,
    {
        let mut addrs = addr
            .to_socket_addrs()
            .map_err(|e| InitializeBrokerError::SocketAddr(addr.to_string(), e))?;

        match addrs.next() {
            Some(addr) => Ok(Self {
                protocol: Protocol::Tcp(addr),
            }),
            None => Err(InitializeBrokerError::MissingSocketAddr(addr.to_string())),
        }
    }

    /// Creates a new instance of a transport protocol TCP over TLS.
    pub fn new_tls<A>(addr: A, identity: ServerCertificate) -> Result<Self, InitializeBrokerError>
    where
        A: ToSocketAddrs + Display,
    {
        let mut addrs = addr
            .to_socket_addrs()
            .map_err(|e| InitializeBrokerError::SocketAddr(addr.to_string(), e))?;

        match addrs.next() {
            Some(addr) => Ok(Self {
                protocol: Protocol::Tls(addr, identity),
            }),
            None => Err(InitializeBrokerError::MissingSocketAddr(addr.to_string())),
        }
    }

    /// Starts to listen incoming connections from remote clients.
    pub async fn incoming(self) -> Result<Incoming, InitializeBrokerError> {
        match self.protocol {
            Protocol::Tcp(addr) => {
                let tcp = TcpListener::bind(&addr)
                    .await
                    .map_err(|e| InitializeBrokerError::BindServer(addr, e))?;

                Ok(Incoming::Tcp(IncomingTcp::new(tcp)))
            }
            Protocol::Tls(addr, identity) => {
                let tcp = TcpListener::bind(&addr)
                    .await
                    .map_err(|e| InitializeBrokerError::BindServer(addr, e))?;
                let acceptor = prepare_acceptor(identity)?;

                Ok(Incoming::Tls(IncomingTls::new(tcp, acceptor)))
            }
        }
    }

    /// Returns a local address which transport listens to.
    pub fn addr(&self) -> SocketAddr {
        match self.protocol {
            Protocol::Tcp(addr) => addr,
            Protocol::Tls(addr, _) => addr,
        }
    }

    /// Returns a server certificate if any.
    pub fn identity(&self) -> Option<&ServerCertificate> {
        match &self.protocol {
            Protocol::Tcp(_) => None,
            Protocol::Tls(_, identity) => Some(identity),
        }
    }
}

enum Protocol {
    Tcp(SocketAddr),
    Tls(SocketAddr, ServerCertificate),
}

fn prepare_acceptor(identity: ServerCertificate) -> Result<SslAcceptor, InitializeBrokerError> {
    let (private_key, certificate, chain, ca) = identity.into_parts();

    let mut acceptor = SslAcceptor::mozilla_modern(SslMethod::tls())?;

    // add server certificate and private key
    acceptor.set_private_key(&private_key)?;
    acceptor.set_certificate(&certificate)?;

    // add all certificates from a chain
    if let Some(chain) = chain {
        for cert in chain {
            acceptor.add_extra_chain_cert(cert)?;
        }
    }

    // install CA certificate in the store
    if let Some(ca) = ca {
        acceptor.cert_store_mut().add_cert(ca)?;
    }

    // set options to support some clients
    acceptor
        .set_options(SslOptions::NO_SESSION_RESUMPTION_ON_RENEGOTIATION | SslOptions::NO_TICKET);

    // request client certificate for verification but disabel certificate verification
    acceptor.set_verify_callback(SslVerifyMode::PEER, |_, _| true);

    // check that private key corresponds to certificate
    acceptor.check_private_key()?;

    Ok(acceptor.build())
}

type HandshakeFuture =
    Pin<Box<dyn Future<Output = Result<SslStream<TcpStream>, openssl::ssl::Error>> + Send>>;

pub enum Incoming {
    Tcp(IncomingTcp),
    Tls(IncomingTls),
}

impl Incoming {
    pub fn local_addr(&self) -> Result<SocketAddr, InitializeBrokerError> {
        let addr = match self {
            Self::Tcp(incoming) => incoming.listener.local_addr(),
            Self::Tls(incoming) => incoming.listener.local_addr(),
        };
        addr.map_err(InitializeBrokerError::ConnectionLocalAddress)
    }
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

    fn poll_next(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        match self.listener.poll_accept(cx) {
            Poll::Ready(Ok((tcp, _))) => match tcp.set_nodelay(true) {
                Ok(()) => {
                    debug!("accepted connection from client");
                    Poll::Ready(Some(Ok(StreamSelector::Tcp(tcp))))
                }
                Err(err) => {
                    warn!(
                        "dropping client because failed to setup TCP properties: {}",
                        err
                    );
                    Poll::Ready(Some(Err(err)))
                }
            },
            Poll::Ready(Err(err)) => {
                error!(
                    "dropping client that failed to completely establish a TCP connection: {}",
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
                        let stream = Ssl::new(acceptor.context())
                            .and_then(|ssl| SslStream::new(ssl, stream));

                        let mut stream = match stream {
                            Ok(stream) => stream,
                            Err(err) => {
                                error!(
                                    error = %err,
                                    "dropping client that failed to complete a TLS handshake",
                                );
                                continue;
                            }
                        };

                        self.connections.push(Box::pin(async move {
                            Pin::new(&mut stream).accept().await?;
                            Ok(stream)
                        }));
                    }
                    Err(err) => warn!(
                        "dropping client because failed to setup TCP properties: {}",
                        err
                    ),
                },
                Poll::Ready(Err(err)) => warn!(
                    "dropping client that failed to completely establish a TCP connection: {}",
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
                    debug!("accepted connection from client");
                    return Poll::Ready(Some(Ok(StreamSelector::Tls(stream))));
                }

                Poll::Ready(Some(Err(err))) => warn!(
                    "dropping client that failed to complete a TLS handshake: {}",
                    err
                ),

                Poll::Ready(None) => {
                    debug!("shutting down web server");
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
            Self::Tcp(stream) => stream,
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
    fn poll_read(
        self: Pin<&mut Self>,
        cx: &mut Context<'_>,
        buf: &mut tokio::io::ReadBuf<'_>,
    ) -> Poll<std::io::Result<()>> {
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
