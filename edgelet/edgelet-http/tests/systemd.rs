#![cfg(target_os = "linux")]

// These tests are sensitive to the number of FDs open in the current process.
// Specifically, the tests require that fd 3 be available to be bound to a socket
// to simulate what happens when systemd passes down sockets during socket activation.
//
// Thus these tests are in their own separate test crate, and use a Mutex to ensure
// that only one runs at a time.

extern crate edgelet_http;
extern crate futures;
extern crate hyper;
#[macro_use]
extern crate lazy_static;
extern crate nix;
extern crate systemd;
extern crate url;

use std::sync::{Mutex, MutexGuard};
use std::{env, io};

use edgelet_http::HyperExt;
use futures::{future, Future};
use hyper::server::conn::Http;
use hyper::service::Service;
use hyper::{Body, Request, Response, StatusCode};
use nix::sys::socket::{self, AddressFamily, SockType};
use nix::unistd::{self, getpid};
use systemd::Fd;
use url::Url;

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
    type ReqBody = Body;
    type ResBody = Body;
    type Error = io::Error;
    type Future = Box<Future<Item = Response<Self::ResBody>, Error = Self::Error>>;

    fn call(&mut self, _req: Request<Self::ReqBody>) -> Self::Future {
        Box::new(if self.error {
            future::err(io::Error::new(io::ErrorKind::Other, "TestService error"))
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
    let _l = lock_env();
    set_current_pid();
    let fd = create_fd(AddressFamily::Unix, SockType::Stream);
    assert_eq!(fd, 3);

    // set the env var so that it contains the created fd
    env::set_var(ENV_FDS, format!("{}", fd - 3 + 1));

    let url = Url::parse(&format!("fd://{}", fd - 3)).unwrap();
    let run = Http::new().bind_url(url, move || {
        let service = TestService {
            status_code: StatusCode::OK,
            error: false,
        };
        Ok::<_, io::Error>(service)
    });

    unistd::close(fd).unwrap();
    if let Err(err) = run {
        panic!("{:?}", err);
    }
}

#[test]
fn test_fd_err() {
    let _l = lock_env();
    set_current_pid();
    let fd = create_fd(AddressFamily::Unix, SockType::Stream);
    assert_eq!(fd, 3);

    // set the env var so that it contains the created fd
    env::set_var(ENV_FDS, format!("{}", fd - 3 + 1));

    let url = Url::parse("fd://100").unwrap();
    let run = Http::new().bind_url(url, move || {
        let service = TestService {
            status_code: StatusCode::OK,
            error: false,
        };
        Ok::<_, io::Error>(service)
    });

    unistd::close(fd).unwrap();
    assert!(run.is_err());
}
