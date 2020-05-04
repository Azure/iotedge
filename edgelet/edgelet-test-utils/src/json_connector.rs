// Copyright (c) Microsoft. All rights reserved.

use std::io::{self, Cursor, Read, Write};

use futures::{future, task, Future, Poll};
use hyper::client::connect::{Connect, Connected, Destination};
use serde::Serialize;
use tokio::io::{AsyncRead, AsyncWrite};

pub struct StaticStream {
    wrote: bool,
    body: Cursor<Vec<u8>>,
}

impl StaticStream {
    pub fn new(body: Vec<u8>) -> Self {
        StaticStream {
            wrote: false,
            body: Cursor::new(body),
        }
    }
}

impl Read for StaticStream {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        if self.wrote {
            self.body.read(buf)
        } else {
            Err(io::ErrorKind::WouldBlock.into())
        }
    }
}

impl Write for StaticStream {
    fn write<'a>(&mut self, buf: &'a [u8]) -> io::Result<usize> {
        self.wrote = true;
        task::current().notify();
        Ok(buf.len())
    }

    fn flush(&mut self) -> io::Result<()> {
        Ok(())
    }
}

impl AsyncRead for StaticStream {}

impl AsyncWrite for StaticStream {
    fn shutdown(&mut self) -> Poll<(), io::Error> {
        Ok(().into())
    }
}

pub struct JsonConnector {
    body: Vec<u8>,
}

impl JsonConnector {
    pub fn new<T: Serialize>(body: &T) -> JsonConnector {
        let body = serde_json::to_string(body).unwrap();
        let body = format!(
            "HTTP/1.1 200 OK\r\n\
             Content-Type: application/json; charset=utf-8\r\n\
             Content-Length: {}\r\n\
             \r\n\
             {}",
            body.len(),
            body,
        )
        .into();

        JsonConnector { body }
    }
}

impl Connect for JsonConnector {
    type Transport = StaticStream;
    type Error = io::Error;
    type Future = Box<dyn Future<Item = (Self::Transport, Connected), Error = Self::Error> + Send>;

    fn connect(&self, _dst: Destination) -> Self::Future {
        Box::new(future::ok((
            StaticStream::new(self.body.clone()),
            Connected::new(),
        )))
    }
}
