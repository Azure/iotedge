// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::module_name_repetitions,
    clippy::similar_names,
    clippy::use_self
)]

use std::fmt;
use std::fmt::{Debug, Display, Formatter};
#[cfg(target_os = "linux")]
use std::net;
use std::net::ToSocketAddrs;
#[cfg(target_os = "linux")]
use std::os::unix::io::FromRawFd;
#[cfg(windows)]
use std::sync::Arc;
#[cfg(unix)]
use std::sync::{Arc, Mutex};

use failure::{Fail, ResultExt};
use futures::{future, Future, Poll, Stream};
use hyper::server::conn::Http;
use hyper::service::{NewService, Service};
use hyper::{Body, Response};
use log::{debug, error, Level};
#[cfg(target_os = "linux")]
use systemd::Socket;
use tokio::net::TcpListener;
#[cfg(target_os = "linux")]
use tokio_uds::UnixListener;
use url::Url;

use openssl::pkcs12::Pkcs12;
use openssl::pkey::PKey;
use openssl::stack::Stack;
use openssl::x509::X509;

use edgelet_core::crypto::{Certificate, CreateCertificate, KeyBytes, PrivateKey};
use edgelet_core::{UrlExt, UNIX_SCHEME};
use edgelet_utils::log_failure;
use native_tls::{Identity, TlsAcceptor};

pub mod authorization;
pub mod certificate_manager;
pub mod client;
pub mod error;
pub mod logging;
mod pid;
pub mod route;
mod unix;
mod util;
mod version;

pub use self::certificate_manager::CertificateManager;
pub use self::error::{BindListenerType, Error, ErrorKind, InvalidUrlReason};
pub use self::util::proxy::MaybeProxyClient;
pub use self::util::UrlConnector;
pub use self::version::{Version, API_VERSION};

use self::pid::PidService;
use self::util::incoming::Incoming;

const HTTP_SCHEME: &str = "http";
#[cfg(unix)]
const HTTPS_SCHEME: &str = "https";
#[cfg(windows)]
const PIPE_SCHEME: &str = "npipe";
const TCP_SCHEME: &str = "tcp";
#[cfg(target_os = "linux")]
const FD_SCHEME: &str = "fd";

#[derive(Clone)]
pub struct PemCertificate {
    cert: Vec<u8>,
    key: Vec<u8>,
    username: Option<String>,
    password: Option<String>,
}

impl PemCertificate {
    pub fn new(
        cert: Vec<u8>,
        key: Vec<u8>,
        username: Option<String>,
        password: Option<String>,
    ) -> Self {
        PemCertificate {
            cert,
            key,
            username,
            password,
        }
    }

    pub fn from<C: Certificate>(id_cert: &C) -> Result<Self, Error> {
        let cert = match id_cert.pem() {
            Ok(cert_buffer) => cert_buffer.as_ref().to_owned(),
            _ => return Err(Error::from(ErrorKind::IdentityCertificate)),
        };

        let key = match id_cert.get_private_key() {
            Ok(Some(PrivateKey::Ref(ref_))) => ref_.into_bytes().clone(),

            Ok(Some(PrivateKey::Key(KeyBytes::Pem(buffer)))) => buffer.as_ref().to_vec(),

            _ => return Err(Error::from(ErrorKind::IdentityPrivateKey)),
        };

        Ok(PemCertificate::new(cert, key, None, None))
    }

    pub fn get_identity(&self) -> Result<Identity, Error> {
        let mut certs =
            X509::stack_from_pem(&self.cert).with_context(|_| ErrorKind::IdentityCertificate)?;

        // the first cert is the identity cert and the other certs are part of the CA
        // chain; we skip the server cert and build an OpenSSL cert stack with the
        // other certs
        let mut ca_certs = Stack::new().with_context(|_| ErrorKind::IdentityCertificate)?;
        for cert in certs.split_off(1) {
            ca_certs
                .push(cert)
                .with_context(|_| ErrorKind::IdentityCertificate)?;
        }

        let key = PKey::private_key_from_pem(&self.key)
            .with_context(|_| ErrorKind::IdentityCertificate)?;

        let identity_cert = &certs[0];

        let mut builder = Pkcs12::builder();
        builder.ca(ca_certs);
        let pkcs_certs = builder
            .build(
                self.password.as_ref().map_or("", String::as_str),
                self.username.as_ref().map_or("", String::as_str),
                &key,
                &identity_cert,
            )
            .with_context(|_| ErrorKind::IdentityCertificate)?;

        let der = pkcs_certs
            .to_der()
            .with_context(|_| ErrorKind::IdentityCertificate)?;

        let identity = Identity::from_pkcs12(&der, "")?;
        Ok(identity)
    }
}

impl Debug for PemCertificate {
    fn fmt(&self, f: &mut Formatter<'_>) -> fmt::Result {
        // do not print either the username, password or private key
        write!(f, "Certificate: {:?}", self.cert)
    }
}

impl Display for PemCertificate {
    // do not print either the username, password or private key
    fn fmt(&self, f: &mut Formatter<'_>) -> fmt::Result {
        write!(f, "Certificate: {:?}", self.cert)
    }
}

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}

impl IntoResponse for Response<Body> {
    fn into_response(self) -> Response<Body> {
        self
    }
}

pub struct Run(Box<dyn Future<Item = (), Error = Error> + Send + 'static>);

impl Future for Run {
    type Item = ();
    type Error = Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        self.0.poll()
    }
}

pub struct Server<S> {
    protocol: Http,
    new_service: S,
    incoming: Incoming,
}

impl<S> Server<S>
where
    S: NewService<ReqBody = Body, ResBody = Body> + Send + 'static,
    <S as NewService>::Future: Send + 'static,
    <S as NewService>::Service: Send + 'static,
    // <S as NewService>::InitError: std::error::Error + Send + Sync + 'static,
    <S as NewService>::InitError: Fail,
    <<S as NewService>::Service as Service>::Future: Send + 'static,
{
    pub fn run(self) -> Run {
        self.run_until(future::empty())
    }

    pub fn run_until<F>(self, shutdown_signal: F) -> Run
    where
        F: Future<Item = (), Error = ()> + Send + 'static,
    {
        let Server {
            protocol,
            new_service,
            incoming,
        } = self;

        let protocol = Arc::new(protocol);

        let srv = incoming.for_each(move |(socket, addr)| {
            let protocol = protocol.clone();

            debug!("accepted new connection ({})", addr);
            let pid = socket.pid()?;
            let fut = new_service
                .new_service()
                .then(move |srv| match srv {
                    Ok(srv) => Ok((srv, addr)),
                    Err(err) => {
                        error!("server connection error: ({})", addr);
                        log_failure(Level::Error, &err);
                        Err(())
                    }
                })
                .and_then(move |(srv, addr)| {
                    let service = PidService::new(pid, srv);
                    protocol
                        .serve_connection(socket, service)
                        .then(move |result| match result {
                            Ok(_) => Ok(()),
                            Err(err) => {
                                error!("server connection error: ({})", addr);
                                log_failure(Level::Error, &err);
                                Err(())
                            }
                        })
                });
            tokio::spawn(fut);
            Ok(())
        });

        // We don't care if the shut_down signal errors.
        // Swallow the error.
        let shutdown_signal = shutdown_signal.then(|_| Ok(()));

        // Main execution
        // Use select to wait for either `incoming` or `f` to resolve.
        let main_execution = shutdown_signal
            .select(srv)
            .then(move |result| match result {
                Ok(((), _other)) => Ok(()),
                Err((e, _other)) => Err(Error::from(e.context(ErrorKind::ServiceError))),
            });

        Run(Box::new(main_execution))
    }
}

pub trait HyperExt {
    fn bind_url<C, S>(
        &self,
        url: Url,
        new_service: S,
        cert_manager: Option<&CertificateManager<C>>,
    ) -> Result<Server<S>, Error>
    where
        C: CreateCertificate + Clone,
        S: NewService<ReqBody = Body>;
}

// This variable is used on Unix but not Windows
#[allow(unused_variables)]
impl HyperExt for Http {
    fn bind_url<C, S>(
        &self,
        url: Url,
        new_service: S,
        cert_manager: Option<&CertificateManager<C>>,
    ) -> Result<Server<S>, Error>
    where
        C: CreateCertificate + Clone,
        S: NewService<ReqBody = Body>,
    {
        let incoming = match url.scheme() {
            HTTP_SCHEME | TCP_SCHEME => {
                let addr = url
                    .to_socket_addrs()
                    .context(ErrorKind::InvalidUrl(url.to_string()))?
                    .next()
                    .ok_or_else(|| {
                        ErrorKind::InvalidUrlWithReason(
                            url.to_string(),
                            InvalidUrlReason::NoAddress,
                        )
                    })?;

                let listener = TcpListener::bind(&addr)
                    .with_context(|_| ErrorKind::BindListener(BindListenerType::Address(addr)))?;
                Incoming::Tcp(listener)
            }
            #[cfg(unix)]
            HTTPS_SCHEME => {
                let addr = url
                    .to_socket_addrs()
                    .context(ErrorKind::InvalidUrl(url.to_string()))?
                    .next()
                    .ok_or_else(|| {
                        ErrorKind::InvalidUrlWithReason(
                            url.to_string(),
                            InvalidUrlReason::NoAddress,
                        )
                    })?;

                let cert = match cert_manager {
                    Some(cert_manager) => cert_manager.get_pkcs12_certificate(),
                    None => return Err(Error::from(ErrorKind::CertificateCreationError)),
                };

                let cert = cert.with_context(|_| ErrorKind::TlsBootstrapError)?;

                let cert_identity = Identity::from_pkcs12(&cert, "")
                    .with_context(|_| ErrorKind::TlsIdentityCreationError)?;

                let tls_acceptor = TlsAcceptor::builder(cert_identity)
                    .build()
                    .with_context(|_| ErrorKind::TlsBootstrapError)?;
                let tls_acceptor = tokio_tls::TlsAcceptor::from(tls_acceptor);

                let listener = TcpListener::bind(&addr)
                    .with_context(|_| ErrorKind::BindListener(BindListenerType::Address(addr)))?;
                Incoming::Tls(listener, tls_acceptor, Mutex::new(vec![]))
            }
            UNIX_SCHEME => {
                let path = url
                    .to_uds_file_path()
                    .map_err(|_| ErrorKind::InvalidUrl(url.to_string()))?;
                unix::listener(path)?
            }
            #[cfg(target_os = "linux")]
            FD_SCHEME => {
                let host = url.host_str().ok_or_else(|| {
                    ErrorKind::InvalidUrlWithReason(url.to_string(), InvalidUrlReason::NoHost)
                })?;

                // Try to parse the host as an FD number, then as an FD name
                let socket = host
                    .parse()
                    .map_err(|_| ())
                    .and_then(|num| systemd::listener(num).map_err(|_| ()))
                    .or_else(|_| systemd::listener_name(host))
                    .with_context(|_| {
                        ErrorKind::InvalidUrlWithReason(
                            url.to_string(),
                            InvalidUrlReason::FdNeitherNumberNorName,
                        )
                    })?;

                match socket {
                    Socket::Inet(fd, _addr) => {
                        let l = unsafe { net::TcpListener::from_raw_fd(fd) };
                        Incoming::Tcp(
                            TcpListener::from_std(l, &Default::default()).with_context(|_| {
                                ErrorKind::BindListener(BindListenerType::Fd(fd))
                            })?,
                        )
                    }
                    Socket::Unix(fd) => {
                        let l = unsafe { ::std::os::unix::net::UnixListener::from_raw_fd(fd) };
                        Incoming::Unix(
                            UnixListener::from_std(l, &Default::default()).with_context(|_| {
                                ErrorKind::BindListener(BindListenerType::Fd(fd))
                            })?,
                        )
                    }
                    Socket::Unknown => Err(ErrorKind::InvalidUrlWithReason(
                        url.to_string(),
                        InvalidUrlReason::UnrecognizedSocket,
                    ))?,
                }
            }
            _ => Err(Error::from(ErrorKind::InvalidUrlWithReason(
                url.to_string(),
                InvalidUrlReason::InvalidScheme,
            )))?,
        };

        Ok(Server {
            protocol: self.clone(),
            new_service,
            incoming,
        })
    }
}
