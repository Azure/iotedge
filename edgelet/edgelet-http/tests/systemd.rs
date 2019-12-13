#![cfg(target_os = "linux")]
#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

// These tests are sensitive to the number of FDs open in the current process.
// Specifically, the tests require that fd 3 be available to be bound to a socket
// to simulate what happens when systemd passes down sockets during socket activation.
//
// Thus these tests are in their own separate test crate, and use a Mutex to ensure
// that only one runs at a time.

use std::sync::{Mutex, MutexGuard};
use std::{env, io};

use edgelet_hsm::Crypto;
use edgelet_http::{HyperExt, TlsAcceptorParams};
use futures::{future, Future};
use hyper::server::conn::Http;
use hyper::service::Service;
use hyper::{Body, Request, Response, StatusCode};
use lazy_static::lazy_static;
use nix::sys::socket::{self, AddressFamily, SockType};
use nix::unistd::{self, getpid};
use systemd::{Fd, LISTEN_FDS_START};
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
    type Future = Box<dyn Future<Item = Response<Self::ResBody>, Error = Self::Error>>;

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
    socket::socket(family, type_, socket::SockFlag::empty(), None).unwrap()
}

#[test]
fn test_fd_ok() {
    let _l = lock_env();
    set_current_pid();
    let fd = create_fd(AddressFamily::Unix, SockType::Stream);

    if fd != LISTEN_FDS_START {
        // In CI, fd 3 seems to be bound to something else already. The reason is unknown.
        // It used to be because of https://github.com/rust-lang/cargo/issues/6333 but that seems to have been fixed
        // in Rust 1.33.0.
        //
        // Since the rest of the code assumes that all fds between LISTEN_FDS_START and LISTEN_FDS_START + ENV_FDS are valid,
        // it's not possible to work around it by telling `Http` to bind to `fd://1/` - it'll just complain that `fd://0/` (ie fd 3)
        // isn't a valid fd.
        //
        // On local builds, fd 3 *does* seem to be available, and E2E tests also use fds for the iotedged service, so we can just pretend
        // the test succeeded without losing coverage.

        unistd::close(fd).unwrap();

        return;
    }

    // set the env var so that it contains the created fd
    env::set_var(ENV_FDS, format!("{}", fd - LISTEN_FDS_START + 1));

    let url = Url::parse(&format!("fd://{}", fd - LISTEN_FDS_START)).unwrap();
    let run = Http::new().bind_url(
        url,
        move || {
            let service = TestService {
                status_code: StatusCode::OK,
                error: false,
            };
            Ok::<_, io::Error>(service)
        },
        None::<TlsAcceptorParams<'_, Crypto>>,
    );
    if let Err(err) = run {
        unistd::close(fd).unwrap();
        panic!("{:?}", err);
    }
}

#[test]
fn test_fd_err() {
    let _l = lock_env();
    set_current_pid();
    let fd = create_fd(AddressFamily::Unix, SockType::Stream);

    // Assume that this fd is the start of all fds systemd would give us.
    // In the application, this would be fd 3. But in tests, some fds can be held open
    // by cargo or the shell and inherited by the test process, so it's not reliable to assert
    // that fds created within the tests start at 3.
    let listen_fds_start = fd;

    // set the env var so that it contains the created fd
    env::set_var(ENV_FDS, format!("{}", fd - listen_fds_start + 1));

    let url = Url::parse("fd://100").unwrap();
    let run = Http::new().bind_url(
        url,
        move || {
            let service = TestService {
                status_code: StatusCode::OK,
                error: false,
            };
            Ok::<_, io::Error>(service)
        },
        None::<TlsAcceptorParams<'_, Crypto>>,
    );

    unistd::close(fd).unwrap();
    assert!(run.is_err());
}
