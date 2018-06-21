// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

extern crate bytes;
extern crate chrono;
extern crate edgelet_core;
extern crate failure;
#[macro_use]
extern crate failure_derive;
#[macro_use]
extern crate futures;
extern crate http;
extern crate hyper;
#[cfg(windows)]
extern crate hyper_named_pipe;
#[cfg(unix)]
extern crate hyperlocal;
#[cfg(target_os = "linux")]
#[cfg(test)]
#[macro_use]
extern crate lazy_static;
#[cfg(unix)]
extern crate libc;
#[macro_use]
extern crate log;
#[cfg(target_os = "linux")]
#[cfg(test)]
extern crate nix;
extern crate percent_encoding;
extern crate regex;
extern crate serde;
#[macro_use]
extern crate serde_json;
extern crate systemd;
#[cfg(test)]
extern crate tempfile;
#[macro_use]
extern crate tokio_core;
extern crate tokio_io;
#[cfg(windows)]
extern crate tokio_named_pipe;
#[cfg(unix)]
extern crate tokio_uds;
extern crate url;

#[macro_use]
extern crate edgelet_utils;

#[cfg(unix)]
use std::fs;
use std::io;
#[cfg(unix)]
use std::net;
use std::net::ToSocketAddrs;
#[cfg(unix)]
use std::os::unix::io::FromRawFd;
#[cfg(unix)]
use std::path::Path;

use futures::{future, Future, Poll, Stream};
use http::{Request, Response};
use hyper::server::{Http, NewService};
use hyper::{Body, Error as HyperError};
#[cfg(unix)]
use systemd::Socket;
use tokio_core::net::TcpListener;
use tokio_core::reactor::Handle;
#[cfg(unix)]
use tokio_uds::UnixListener;
use url::Url;

pub mod authorization;
pub mod client;
mod compat;
pub mod error;
pub mod logging;
mod pid;
pub mod route;
mod util;
mod version;

pub use self::error::{Error, ErrorKind};
pub use self::util::UrlConnector;
pub use self::version::{ApiVersionService, API_VERSION};

use self::pid::PidService;
use self::util::incoming::Incoming;

const HTTP_SCHEME: &str = "http";
const TCP_SCHEME: &str = "tcp";
#[cfg(unix)]
const UNIX_SCHEME: &str = "unix";
#[cfg(unix)]
const FD_SCHEME: &str = "fd";

pub trait IntoResponse {
    fn into_response(self) -> Response<Body>;
}

impl IntoResponse for Response<Body> {
    fn into_response(self) -> Response<Body> {
        self
    }
}

pub struct Run(Box<Future<Item = (), Error = HyperError> + 'static>);

impl Future for Run {
    type Item = ();
    type Error = HyperError;

    fn poll(&mut self) -> Poll<(), Self::Error> {
        self.0.poll()
    }
}

pub struct Server<S, B>
where
    B: Stream<Error = HyperError>,
    B::Item: AsRef<[u8]>,
{
    protocol: Http<B::Item>,
    new_service: S,
    handle: Handle,
    incoming: Incoming,
}

impl<S, B> Server<S, B>
where
    S: NewService<Request = Request<Body>, Response = Response<B>, Error = HyperError> + 'static,
    B: Stream<Error = HyperError> + 'static,
    B::Item: AsRef<[u8]>,
{
    pub fn run(self) -> Run {
        self.run_until(future::empty())
    }

    pub fn run_until<F>(self, shutdown_signal: F) -> Run
    where
        F: Future<Item = (), Error = ()> + 'static,
    {
        let Server {
            protocol,
            new_service,
            handle,
            incoming,
        } = self;

        let srv = incoming.for_each(move |(socket, addr)| {
            debug!("accepted new connection ({})", addr);
            let pid = socket.pid()?;
            let srv = new_service.new_service()?;
            let service = PidService::new(pid, srv);
            let fut = protocol
                .serve_connection(socket, self::compat::service(service))
                .map(|_| ())
                .map_err(move |err| error!("server connection error: ({}) {}", addr, err));
            handle.spawn(fut);
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
                Ok(((), _incoming)) => future::ok(()),
                Err((e, _other)) => future::err(e.into()),
            });

        Run(Box::new(main_execution))
    }
}

pub trait HyperExt<B: AsRef<[u8]> + 'static> {
    fn bind_handle<S, Bd>(
        &self,
        url: Url,
        handle: Handle,
        new_service: S,
    ) -> Result<Server<S, Bd>, Error>
    where
        S: NewService<Request = Request<Body>, Response = Response<Bd>, Error = HyperError>
            + 'static,
        Bd: Stream<Item = B, Error = HyperError>;
}

impl<B: AsRef<[u8]> + 'static> HyperExt<B> for Http<B> {
    fn bind_handle<S, Bd>(
        &self,
        url: Url,
        handle: Handle,
        new_service: S,
    ) -> Result<Server<S, Bd>, Error>
    where
        S: NewService<Request = Request<Body>, Response = Response<Bd>, Error = HyperError>
            + 'static,
        Bd: Stream<Item = B, Error = HyperError>,
    {
        let incoming = match url.scheme() {
            HTTP_SCHEME | TCP_SCHEME => {
                let addr = url.to_socket_addrs()?.next().ok_or_else(|| {
                    io::Error::new(io::ErrorKind::Other, format!("Invalid url: {}", url))
                })?;

                let listener = TcpListener::bind(&addr, &handle)?;
                Incoming::Tcp(listener)
            }
            #[cfg(unix)]
            UNIX_SCHEME => {
                let path = url.path();
                if Path::new(path).exists() {
                    fs::remove_file(path)?;
                }
                let listener = UnixListener::bind(path, &handle)?;
                Incoming::Unix(listener)
            }
            #[cfg(unix)]
            FD_SCHEME => {
                let host = url.host_str()
                    .ok_or_else(|| Error::from(ErrorKind::InvalidUri(url.to_string())))?;
                let socket = host.parse::<usize>()
                    .map_err(Error::from)
                    .and_then(|num| systemd::listener(num).map_err(Error::from))
                    .or_else(|_| systemd::listener_name(host))?;

                match socket {
                    Socket::Inet(fd, addr) => {
                        let l = unsafe { net::TcpListener::from_raw_fd(fd) };
                        Incoming::Tcp(TcpListener::from_listener(l, &addr, &handle)?)
                    }
                    Socket::Unix(fd) => {
                        let l = unsafe { ::std::os::unix::net::UnixListener::from_raw_fd(fd) };
                        Incoming::Unix(UnixListener::from_listener(l, &handle)?)
                    }
                    _ => Err(Error::from(ErrorKind::InvalidUri(url.to_string())))?,
                }
            }
            _ => Err(Error::from(ErrorKind::InvalidUri(url.to_string())))?,
        };

        Ok(Server {
            protocol: self.clone(),
            new_service,
            handle,
            incoming,
        })
    }
}

#[cfg(target_os = "linux")]
#[cfg(test)]
mod linux_tests {
    use super::*;

    use std::env;
    use std::sync::{Mutex, MutexGuard};

    use http::StatusCode;
    use hyper::server::Service;
    use nix::sys::socket::{self, AddressFamily, SockType};
    use nix::unistd::{self, getpid};
    use systemd::Fd;
    use tokio_core::reactor::Core;

    lazy_static! {
        static ref LOCK: Mutex<()> = Mutex::new(());
    }

    const ENV_FDS: &str = "LISTEN_FDS";
    const ENV_PID: &str = "LISTEN_PID";

    #[derive(Clone)]
    struct TestService {
        status_code: StatusCode,
        error: bool,
    }

    impl Service for TestService {
        type Request = Request<Body>;
        type Response = Response<Body>;
        type Error = HyperError;
        type Future = Box<Future<Item = Self::Response, Error = Self::Error>>;

        fn call(&self, _req: Self::Request) -> Self::Future {
            Box::new(if self.error {
                future::err(HyperError::TooLarge)
            } else {
                future::ok(
                    Response::builder()
                        .status(self.status_code)
                        .body(Body::default())
                        .unwrap(),
                )
            })
        }
    }

    fn lock_env<'a>() -> MutexGuard<'a, ()> {
        LOCK.lock().unwrap()
    }

    fn set_current_pid() {
        let pid = getpid();
        env::set_var(ENV_PID, format!("{}", pid));
    }

    fn create_fd(family: AddressFamily, type_: SockType) -> Fd {
        let fd = socket::socket(family, type_, socket::SockFlag::empty(), None).unwrap();
        fd
    }

    #[test]
    fn test_fd_ok() {
        let core = Core::new().unwrap();
        let _l = lock_env();
        set_current_pid();
        let fd = create_fd(AddressFamily::Unix, SockType::Stream);

        // set the env var so that it contains the created fd
        env::set_var(ENV_FDS, format!("{}", fd - 3 + 1));

        let url = Url::parse(&format!("fd://{}", fd - 3)).unwrap();
        let run = Http::new().bind_handle(url, core.handle(), move || {
            let service = TestService {
                status_code: StatusCode::OK,
                error: false,
            };
            Ok(service)
        });

        unistd::close(fd).unwrap();
        assert!(run.is_ok());
    }

    #[test]
    fn test_fd_err() {
        let core = Core::new().unwrap();
        let _l = lock_env();
        set_current_pid();
        let fd = create_fd(AddressFamily::Unix, SockType::Stream);

        // set the env var so that it contains the created fd
        env::set_var(ENV_FDS, format!("{}", fd - 3 + 1));

        let url = Url::parse("fd://100").unwrap();
        let run = Http::new().bind_handle(url, core.handle(), move || {
            let service = TestService {
                status_code: StatusCode::OK,
                error: false,
            };
            Ok(service)
        });

        unistd::close(fd).unwrap();
        assert!(run.is_err());
    }
}
