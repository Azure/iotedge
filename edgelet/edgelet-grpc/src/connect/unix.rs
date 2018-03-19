// Copyright (c) Microsoft. All rights reserved.

#![cfg(unix)]

use failure::ResultExt;
use futures::Poll;
use futures::future::FutureResult;
use tokio_core::reactor::Handle;
use tokio_uds::UnixStream;
use tower::Service;
use url::Url;

use error::{Error, ErrorKind};

/// A Connector using Unix Domain Sockets as the underlying transport.
pub struct UnixConnector {
    handle: Handle,
}

impl UnixConnector {
    pub fn new(handle: &Handle) -> UnixConnector {
        UnixConnector {
            handle: handle.clone(),
        }
    }
}

impl Service for UnixConnector {
    type Request = Url;
    type Response = UnixStream;
    type Error = Error;
    type Future = FutureResult<Self::Response, Self::Error>;

    fn poll_ready(&mut self) -> Poll<(), Self::Error> {
        Ok(().into())
    }

    fn call(&mut self, url: Self::Request) -> Self::Future {
        trace!("Unix::connect({:?})", url);
        let path = url.path();
        UnixStream::connect(path, &self.handle)
            .context(ErrorKind::Io)
            .map_err(Error::from)
            .into()
    }
}
