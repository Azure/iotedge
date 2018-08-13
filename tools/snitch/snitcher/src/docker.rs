// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::str;

use byteorder::{BigEndian, ByteOrder};
use bytes::Bytes;
use futures::{future, Future};
use hyper::service::Service;
use hyper::{Body, Error as HyperError, Method};

use client::Client;
use error::Error;

#[derive(Clone)]
pub struct DockerClient<S>
where
    S: 'static + Service<ReqBody = Body, ResBody = Body, Error = HyperError> + Send,
{
    client: Client<S>,
}

impl<S> DockerClient<S>
where
    S: 'static + Service<ReqBody = Body, ResBody = Body, Error = HyperError> + Send,
    <S as Service>::Future: Send,
{
    pub fn new(client: Client<S>) -> DockerClient<S> {
        DockerClient { client }
    }

    pub fn list_containers(
        &self,
    ) -> impl Future<Item = Option<Vec<Container>>, Error = Error> + Send {
        let mut query = HashMap::new();
        query.insert("all", "true");
        query.insert("size", "true");
        query.insert("limit", "0");

        self.client.request::<(), Vec<Container>>(
            Method::GET,
            "containers/json",
            Some(query),
            None,
            false,
        )
    }

    pub fn logs(&self, id: &str) -> impl Future<Item = Option<String>, Error = Error> + Send {
        let mut query = HashMap::new();
        query.insert("stdout", "true");
        query.insert("stderr", "true");

        self.client
            .request_bytes::<()>(
                Method::GET,
                &format!("containers/{}/logs", id),
                Some(query),
                None,
                false,
            )
            .and_then(|bytes| {
                bytes
                    .map(|mut bytes| {
                        let mut logs = String::new();
                        while !bytes.is_empty() {
                            let line = read_line(&mut bytes);
                            let line = str::from_utf8(line.as_ref()).map_err(Error::from);
                            if let Err(err) = line {
                                return future::err(err);
                            }
                            logs.push_str(line.expect("Unexpected error value in 'line'"));
                        }

                        future::ok(Some(logs))
                    })
                    .unwrap_or_else(|| future::ok(None))
            })
    }
}

#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub struct Container {
    id: String,
    names: Option<Vec<String>>,
    image: String,
    image_id: Option<String>,
    created: u64,
    state: Option<String>,
    status: Option<String>,
    labels: Option<HashMap<String, String>>,
}

impl Container {
    pub fn id(&self) -> &str {
        &self.id
    }

    pub fn name(&self) -> &str {
        self.names
            .as_ref()
            .and_then(|names| names.get(0))
            .map(|s| s.as_str())
            .unwrap_or_else(|| self.id.as_str())
    }

    pub fn state(&self) -> Option<&String> {
        self.state.as_ref()
    }
}

/// Logs parser
/// Logs are emitted with a simple header to specify stdout or stderr
///
/// 01 00 00 00 00 00 00 1f 52 6f 73 65 73 20 61 72  65 ...
/// │  ─────┬── ─────┬─────  R  o  s  e  s     a  r   e ...
/// │       │        │
/// └stdout │        │
///         │        └ 0x0000001f = log message is 31 bytes
///       unused
///
fn read_line(buf: &mut Bytes) -> Bytes {
    buf.advance(4); // ignore stream type & unused bytes
    let len = BigEndian::read_u32(buf.as_ref()); // read length
    buf.advance(4); // 4 bytes for string length
    let result = buf.slice_to(len as usize);
    buf.advance(len as usize); // len bytes for string
    result
}
