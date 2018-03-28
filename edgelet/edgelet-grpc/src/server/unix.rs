// Copyright (c) Microsoft. All rights reserved.

#![cfg(unix)]
use std::{fs, io};
use std::fmt::Debug;
use std::path::Path;

use http::request::Request;
use http::response::Response;
use tokio_core::reactor::Handle;
use tokio_io::IoStream;
use tokio_uds::{UnixListener as UdsListener, UnixStream};
use tower::NewService;
use tower_h2::{Body, RecvBody};
use url::Url;

use server::{Listener, Server};

/// A Listener that accepts connections over Unix Domain socket.
#[derive(Debug)]
pub struct UnixListener {
    inner: UdsListener,
}

impl UnixListener {
    fn new(inner: UdsListener) -> UnixListener {
        UnixListener { inner }
    }
}

impl Listener for UnixListener {
    type Socket = UnixStream;
    type Addr = ::std::os::unix::net::SocketAddr;
    type Connections = IoStream<(Self::Socket, Self::Addr)>;

    fn bind_handle<S, B>(
        address: Url,
        handle: &Handle,
        new_service: S,
    ) -> Result<Server<Self, S, B>, io::Error>
    where
        S: NewService<Request = Request<RecvBody>, Response = Response<B>> + Debug + 'static,
        S::InitError: Debug,
        S::Error: Debug,
        B: Body + 'static,
    {
        if Path::new(address.path()).exists() {
            fs::remove_file(address.path())?;
        }

        let listener = UdsListener::bind(address.path(), &handle).map(UnixListener::new)?;

        Ok(Server {
            new_service,
            handle: handle.clone(),
            listener,
        })
    }

    fn incoming(self) -> Self::Connections {
        self.inner.incoming()
    }
}
