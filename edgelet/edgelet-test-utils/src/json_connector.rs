// Copyright (c) Microsoft. All rights reserved.

use std::io::{self, Cursor, Read, Write};

use futures::{future, task, Future, Poll};
use hyper::Uri;
use hyper::client::Service;
use serde::Serialize;
use serde_json;
use tokio_io::{AsyncRead, AsyncWrite};

pub struct StaticStream {
    wrote: bool,
    body: Cursor<Vec<u8>>,
}

impl StaticStream {
    pub fn new(body: Vec<u8>) -> StaticStream {
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

pub struct JsonConnector<T: Serialize> {
    body: T,
}

impl<T: Serialize> JsonConnector<T> {
    pub fn new(body: T) -> JsonConnector<T> {
        JsonConnector { body }
    }
}

impl<T: Serialize> Service for JsonConnector<T> {
    type Request = Uri;
    type Response = StaticStream;
    type Error = io::Error;
    type Future = Box<Future<Item = Self::Response, Error = io::Error>>;

    fn call(&self, _req: Uri) -> Self::Future {
        let json = serde_json::to_string(&self.body).unwrap();
        let response = format!(
            "HTTP/1.1 200 OK\r\n\
             Content-Type: application/json; charset=utf-8\r\n\
             Content-Length: {}\r\n\
             \r\n\
             {}",
            json.len(),
            json
        );

        Box::new(future::ok(StaticStream {
            wrote: false,
            body: Cursor::new(response.into_bytes()),
        }))
    }
}
