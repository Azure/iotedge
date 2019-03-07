// Copyright (c) Microsoft. All rights reserved.

#![cfg(windows)]
#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]

use std::io::{Read, Write};
use std::str;

use futures::sink::Sink;
use futures::stream::Stream;
use futures::Future;
use mio::{Events, Poll, PollOpt, Ready, Token};
use mio_named_pipes::NamedPipe;
use rand::Rng;
use tokio::codec::{FramedRead, FramedWrite, LinesCodec};
use tokio::io as tio;

use tokio_named_pipe::PipeStream;

macro_rules! t {
    ($e:expr) => {
        match $e {
            Ok(e) => e,
            Err(e) => panic!("{} failed with {}", stringify!($e), e),
        }
    };
}

fn server() -> (NamedPipe, String) {
    let num: u64 = rand::thread_rng().gen();
    let name = format!(r"\\.\pipe\my-pipe-{}", num);
    let pipe = t!(NamedPipe::new(&name));
    (pipe, name)
}

#[test]
#[should_panic(expected = "The system cannot find the file specified")]
fn connect_invalid_path() {
    let _stream = PipeStream::connect(r"\\.\pipe\boo", None).unwrap();
}

#[test]
fn connect_succeeds() {
    let (_server, path) = server();
    let _stream = PipeStream::connect(path, None).unwrap();
}

#[test]
fn read_data() {
    let data = b"cow say moo";
    let (mut server, path) = server();

    let stream = PipeStream::connect(path, None).unwrap();

    let poll = t!(Poll::new());
    t!(poll.register(
        &server,
        Token(0),
        Ready::readable() | Ready::writable(),
        PollOpt::level()
    ));

    // writing data
    assert_eq!(t!(server.write(data)), data.len());

    let buf = [0; 11];
    let task = tio::read_exact(stream, buf);

    let (_, read_data) = tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(task)
        .unwrap();
    for i in 0..read_data.len() {
        assert_eq!(read_data[i], data[i]);
    }
}

#[test]
fn write_data() {
    let (mut server, path) = server();
    let stream = PipeStream::connect(path, None).unwrap();

    let write_future = tio::write_all(stream, b"cow say moo");
    tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(write_future)
        .unwrap();

    let poll = t!(Poll::new());
    t!(poll.register(
        &server,
        Token(0),
        Ready::readable() | Ready::writable(),
        PollOpt::edge()
    ));

    // reading data
    let mut events = Events::with_capacity(128);
    loop {
        t!(poll.poll(&mut events, None));
        let events = events.iter().collect::<Vec<_>>();
        if let Some(event) = events.iter().find(|e| e.token() == Token(0)) {
            if event.readiness().is_readable() {
                break;
            }
        }
    }

    let mut buf = [0; 11];
    assert_eq!(t!(server.read(&mut buf)), 11);
    assert_eq!(&buf[..11], b"cow say moo");
}

#[test]
fn read_async() {
    let data = &b"cow say moo\nsheep say baa\n"[..];
    let (mut server, path) = server();

    let stream = PipeStream::connect(path, None).unwrap();

    let poll = t!(Poll::new());
    t!(poll.register(
        &server,
        Token(0),
        Ready::readable() | Ready::writable(),
        PollOpt::edge()
    ));

    // writing data
    assert_eq!(t!(server.write(data)), data.len());

    // reading data
    let framed_read = FramedRead::new(stream, LinesCodec::new());
    let task = framed_read.into_future().and_then(|(line1, stream)| {
        stream
            .into_future()
            .map(|(line2, _)| (line1.unwrap(), line2.unwrap()))
    });

    let result = tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(task)
        .unwrap();
    assert_eq!(
        result,
        ("cow say moo".to_string(), "sheep say baa".to_string())
    );
}

#[test]
fn write_async() {
    let (mut server, path) = server();
    let stream = PipeStream::connect(path, None).unwrap();

    let framed_write = FramedWrite::new(stream, LinesCodec::new());
    let task = framed_write
        .send("cow say moo".to_string())
        .and_then(|sink| sink.send("sheep say baa".to_string()));
    tokio::runtime::current_thread::Runtime::new()
        .unwrap()
        .block_on(task)
        .unwrap();

    let poll = t!(Poll::new());
    t!(poll.register(
        &server,
        Token(0),
        Ready::readable() | Ready::writable(),
        PollOpt::edge()
    ));

    // reading data
    let mut events = Events::with_capacity(128);
    let mut output = String::new();
    let mut buf = [0; 26];
    loop {
        t!(poll.poll(&mut events, None));
        let events = events.iter().collect::<Vec<_>>();
        if let Some(event) = events.iter().find(|e| e.token() == Token(0)) {
            if event.readiness().is_readable() {
                let count = t!(server.read(&mut buf));
                output.push_str(str::from_utf8(&buf[..count]).unwrap());

                if output == "cow say moo\nsheep say baa\n" {
                    break;
                }
            }
        }
    }
}
