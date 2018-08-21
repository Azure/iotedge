// Copyright (c) Microsoft. All rights reserved.

use error::{Error, ErrorKind};
use futures::prelude::*;
use hyper::client::connect::{Connect, Connected, Destination};
use std::path::PathBuf;
use tokio_uds::UnixStream;

mod uri;
pub use self::uri::Uri;

#[derive(Clone)]
pub struct UnixConnector;

impl Connect for UnixConnector {
    type Transport = UnixStream;
    type Error = Error;
    type Future = ConnectFuture;

    fn connect(&self, dst: Destination) -> Self::Future {
        let state = if dst.scheme() != "unix" {
            ConnectState::Error(Error::new(ErrorKind::InvalidUrlScheme))
        } else {
            Uri::get_uds_path(dst.host())
                .map(ConnectState::Initialized)
                .unwrap_or_else(ConnectState::Error)
        };

        ConnectFuture::new(state)
    }
}

pub enum ConnectState {
    Initialized(PathBuf),
    // TODO:
    // We are boxing this future because the current version of tokio-uds
    // published on crates.io does not export the ConnectFuture type returned
    // by UnixStream::connect. Once that is exported we can get rid of this
    // box and just use that type.
    Connecting(Box<Future<Item = UnixStream, Error = Error> + Send>),
    Error(Error),
}

pub struct ConnectFuture {
    state: ConnectState,
}

impl ConnectFuture {
    pub fn new(state: ConnectState) -> ConnectFuture {
        match state {
            ConnectState::Initialized(path) => {
                debug!("Connecting to {:?}", path);
                ConnectFuture {
                    state: ConnectState::Connecting(Box::new(
                        UnixStream::connect(path).map_err(Error::from),
                    )),
                }
            }
            _ => ConnectFuture { state },
        }
    }
}

impl Future for ConnectFuture {
    type Item = (UnixStream, Connected);
    type Error = Error;

    fn poll(&mut self) -> Poll<Self::Item, Self::Error> {
        match self.state {
            ConnectState::Initialized(_) => Err(Error::new(ErrorKind::InvalidConnectState)),
            ConnectState::Error(ref err) => {
                Err(Error::new(ErrorKind::Connect(format!("{:?}", err))))
            }
            ConnectState::Connecting(ref mut inner) => match inner.poll()? {
                Async::Ready(stream) => {
                    debug!("Connected");
                    Ok(Async::Ready((stream, Connected::new())))
                }
                Async::NotReady => {
                    debug!("Connection not ready yet");
                    Ok(Async::NotReady)
                }
            },
        }
    }
}
