// Copyright (c) Microsoft. All rights reserved.

#[cfg(windows)]
use std::io::{self, Read, Write};
use std::str;
use std::sync::mpsc::Sender;

use httparse::{self, Request as HtRequest, Status};
use mio::{Events, Poll, PollOpt, Ready, Token};
use mio_named_pipes::NamedPipe;

pub fn run_pipe_server<F>(path: &str, handler: &'static F, ready_channel: &Sender<()>)
where
    F: Fn(&HtRequest, Option<Vec<u8>>) -> String + Send + Sync,
{
    let mut server = NamedPipe::new(path).unwrap();

    let poll = Poll::new().unwrap();
    poll.register(
        &server,
        Token(0),
        Ready::readable() | Ready::writable(),
        PollOpt::edge(),
    ).unwrap();

    // signal that the server is ready to run
    ready_channel.send(()).unwrap();

    // wait for a client to connect
    match server.connect() {
        Ok(_) => (),
        Err(err) => {
            if err.kind() != io::ErrorKind::WouldBlock {
                panic!("connect error {:?}", err);
            }
        }
    };

    let mut events = Events::with_capacity(128);
    poll.poll(&mut events, None).unwrap();

    wait_readable(&poll, &mut events);

    // read and parse the request
    let buffer = read_to_end(&mut server);
    let mut headers = [httparse::EMPTY_HEADER; 16];
    let (req, size) = parse_req(&mut headers, &buffer);

    let content_length = get_content_length(req.headers);

    // size has length of HTTP header; if there's a content-length header
    // and its value is greater than zero then try to read the body
    let mut body = None;
    if let Some(length) = content_length {
        if buffer.len() < (size + length) {
            wait_readable(&poll, &mut events);
            body = Some(read_to_end(&mut server));
        }
    }

    // handle the request and respond
    let response = handler(&req, body);
    server.write(response.as_bytes()).unwrap();
}

fn wait_readable(poll: &Poll, events: &mut Events) {
    loop {
        poll.poll(events, None).unwrap();
        let events = events.iter().collect::<Vec<_>>();
        if let Some(event) = events.iter().find(|e| e.token() == Token(0)) {
            if event.readiness().is_readable() {
                break;
            }
        }
    }
}

fn get_content_length<'a>(headers: &'a [httparse::Header<'a>]) -> Option<usize> {
    for header in headers {
        if header.name == "Content-Length" {
            return Some(str::from_utf8(header.value).unwrap().parse().unwrap());
        }
    }

    None
}

/// Given a type that implements Read, parse an HTTP request from the bytes
/// read from the source.
fn parse_req<'a>(
    headers: &'a mut [httparse::Header<'a>],
    buffer: &'a [u8],
) -> (HtRequest<'a, 'a>, usize) {
    let mut req = HtRequest::new(headers);
    let res = req.parse(buffer).unwrap();
    match res {
        Status::Complete(size) => (req, size),
        Status::Partial => panic!("Unexpected partial parse of HTTP request"),
    }
}

fn read_to_end<T: Read>(source: &mut T) -> Vec<u8> {
    let mut buf = [0; 256];
    let mut buffer = Vec::new();
    loop {
        match source.read(&mut buf) {
            Ok(0) => break,
            Ok(len) => buffer.extend_from_slice(&buf[0..len]),
            Err(err) => if err.kind() == io::ErrorKind::WouldBlock {
                break;
            } else {
                panic!("read_to_end error: {:?}", err);
            },
        }
    }

    buffer
}
