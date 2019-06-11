// Copyright (c) Microsoft. All rights reserved.

#[cfg(windows)]
mod windows;

#[cfg(windows)]
pub use self::windows::run_pipe_server;

use std::sync::Arc;
use std::fs;
use std::io;
#[cfg(unix)]
use std::os::unix::net::UnixListener as StdUnixListener;

use futures::prelude::*;
use hyper::server::conn::Http;
use hyper::service::service_fn;
use hyper::{self, Body, Request, Response};
#[cfg(unix)]
use hyperlocal::server::{Http as UdsHttp, Incoming as UdsIncoming};
#[cfg(windows)]
use hyperlocal_windows::server::{Http as UdsHttp, Incoming as UdsIncoming};
#[cfg(windows)]
use mio_uds_windows::net::UnixListener as StdUnixListener;
use tokio::net::TcpListener;
use mio::tcp::{TcpListener, TcpStream, Shutdown};

use rustls::{RootCertStore, ServerConfig, Session};
use rustls::AllowAnyAuthenticatedClient;
use edgelet_http::PemCertificate;
use std::io::BufReader;

pub fn run_tcp_server<F, R>(
    ip: &str,
    port: u16,
    handler: F,
) -> impl Future<Item = (), Error = hyper::Error>
where
    F: 'static + Fn(Request<Body>) -> R + Clone + Send + Sync,
    R: 'static + Future<Item = Response<Body>, Error = hyper::Error> + Send,
{
    let addr = &format!("{}:{}", ip, port).parse().unwrap();

    let serve = Http::new()
        .serve_addr(addr, move || service_fn(handler.clone()))
        .unwrap();
    serve.for_each(|connecting| {
        connecting
            .then(|connection| {
                let connection = connection.unwrap();
                Ok::<_, hyper::Error>(connection)
            })
            .flatten()
    })
}

pub fn run_tls_tcp_server(
    ip: &str,
    port: u16,
    identity: native_tls::Identity,
) -> impl Future<Item = (), Error = ()> {
    let addr = &format!("{}:{}", ip, port).parse().unwrap();
    let listener = TcpListener::bind(&addr).unwrap();
    let tls_acceptor =
        tokio_tls::TlsAcceptor::from(native_tls::TlsAcceptor::builder(identity).build().unwrap());
    listener
        .incoming()
        .for_each(move |socket| {
            let tls_accept = tls_acceptor
                .accept(socket)
                .and_then(move |tls_stream| {
                    let conn = tokio::io::write_all(tls_stream, "HTTP/1.1 200 OK")
                        .map(|_| ())
                        .map_err(|err| panic!("IO write to stream error: {:#?}", err));

                    tokio::spawn(conn);
                    Ok(())
                })
                .map_err(|err| panic!("TLS accept error: {:#?}", err));

            tokio::spawn(tls_accept);
            Ok(())
        })
        .map_err(|err| panic!("server error: {:#?}", err))
}

fn load_server_private_key(server_cert: PemCertificate) -> rustls::PrivateKey {
    let rsa_keys = {
        let key_bytes = server_cert.get_key().expect("did not find private key");
        let mut reader = BufReader::new(key_bytes);
        rustls::internal::pemfile::rsa_private_keys(&mut reader)
            .expect("file contains invalid rsa private key")
    };

    let pkcs8_keys = {
        let key_bytes = server_cert.get_key().expect("did not find private key");
        let mut reader = BufReader::new(key_bytes);
        rustls::internal::pemfile::rsa_private_keys(&mut reader)
            .expect("file contains invalid rsa private key")
    };

    // prefer to load pkcs8 keys
    if !pkcs8_keys.is_empty() {
        pkcs8_keys[0].clone()
    } else {
        assert!(!rsa_keys.is_empty());
        rsa_keys[0].clone()
    }
}

pub fn run_tls_tcp_server_with_mutual_auth(
    ip: &str,
    port: u16,
    server_cert: PemCertificate,
    trust_bundle: Option<PemCertificate>,
) -> impl Future<Item = (), Error = ()> {
    let addr = &format!("{}:{}", ip, port).parse().unwrap();

    let mut client_auth_roots = RootCertStore::empty();

    if let Some(trusted_cas) = trust_bundle {
        let mut certs = BufReader::new(trusted_cas.get_certificate());
        let certs = rustls::internal::pemfile::certs(&mut certs).unwrap();
        for cert in certs {
            client_auth_roots.add(&cert).unwrap();
        }
    }

    let client_auth = AllowAnyAuthenticatedClient::new(client_auth_roots);
    let mut config = ServerConfig::new(client_auth);

    let mut certs = BufReader::new(server_cert.get_certificate());
    let server_certs = rustls::internal::pemfile::certs(&mut certs).unwrap();
    config.set_single_cert(server_certs, load_server_private_key(server_cert));
    let acceptor = tokio_rustls::TlsAcceptor::from(Arc::new(config));
    let listener = TcpListener::bind(&addr).expect("cannot listen on port");
    listener
        .incoming()
        .for_each(move |stream| {
            let addr = stream.peer_addr().ok();
            debug!("Accepting new connection from {:?}", addr);
            let done = acceptor.accept(stream)
                .and_then(|stream| io::write_all(
                    stream,
                    &b"HTTP/1.0 200 ok\r\n\
                    Connection: close\r\n\
                    Content-length: 12\r\n\
                    \r\n\
                    Hello world!"
                ))
                .and_then(|(stream, _)| io::flush(stream))
                .map(move |_| println!("Accept: {:?}", addr))
                .map_err(move |err| println!("Error: {:?} - {:?}", err, addr));
            tokio::spawn(done);
            Ok(())
        })
        .map_err(|err| panic!("server error: {:#?}", err))

    // let connections = socket.incoming();

    // let tls_handshake = connections.map(|(socket, _addr)| {
    //     socket.set_nodelay(true).unwrap();
    //     config.accept_async(socket)
    // });

    // let server = tls_handshake.map(|acceptor| {
    //     let handle = handle.clone();
    //     acceptor.and_then(move |stream| {
    //         let conn = tokio::io::write_all(tls_stream, "HTTP/1.1 200 OK")
    //             .map(|_| ())
    //             .map_err(|err| panic!("IO write to stream error: {:#?}", err));

    //         tokio::spawn(tls_accept);
    //         Ok(())
    //     })
    // })
    // .map_err(|err| panic!("server error: {:#?}", err));
}

pub fn run_uds_server<F, R>(path: &str, handler: F) -> impl Future<Item = (), Error = io::Error>
where
    F: 'static + Fn(Request<Body>) -> R + Clone + Send + Sync,
    R: 'static + Future<Item = Response<Body>, Error = io::Error> + Send,
{
    fs::remove_file(&path).unwrap_or(());

    // Bind a listener synchronously, so that the caller's client will not fail to connect
    // regardless of when the asynchronous server accepts the connection
    let listener = StdUnixListener::bind(path).unwrap();
    let incoming = UdsIncoming::from_std(listener, &Default::default()).unwrap();
    let serve = UdsHttp::new().serve_incoming(incoming, move || service_fn(handler.clone()));

    serve.for_each(|connecting| {
        connecting
            .then(|connection| {
                let connection = connection.unwrap();
                Ok::<_, hyper::Error>(connection)
            })
            .flatten()
            .map_err(|e| {
                io::Error::new(
                    io::ErrorKind::Other,
                    format!("failed to serve connection: {}", e),
                )
            })
    })
}
