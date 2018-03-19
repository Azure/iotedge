// Copyright (c) Microsoft. All rights reserved.

#![cfg(unix)]
use std::{fs, io};

use http::request::Request;
use http::response::Response;
use tokio_core::reactor::Core;
use tokio_io::IoStream;
use tokio_uds::{UnixListener as UdsListener, UnixStream};
use tower::NewService;
use tower_h2::{Body, RecvBody};
use url::Url;

use server::{Listener, Server};

/// A Listener that accepts connections over Unix Domain socket.
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

    fn bind<S, B>(address: Url, new_service: S) -> Result<Server<Self, S, B>, io::Error>
    where
        S: NewService<Request = Request<RecvBody>, Response = Response<B>> + 'static,
        B: Body + 'static,
    {
        let core = Core::new()?;
        let handle = core.handle();

        fs::remove_file(address.path())?;
        let listener = UdsListener::bind(address.path(), &handle).map(UnixListener::new)?;

        Ok(Server {
            new_service,
            core,
            listener,
        })
    }

    fn incoming(self) -> Self::Connections {
        self.inner.incoming()
    }
}
