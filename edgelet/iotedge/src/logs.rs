// Copyright (c) Microsoft. All rights reserved.

use std::cmp;
use std::io::{self, Write};

use bytes::{Buf, BufMut, Bytes, BytesMut, IntoBuf};
use failure::Fail;
use futures::prelude::*;
use futures::try_ready;
use tokio::codec::length_delimited;
use tokio::codec::FramedRead;
use tokio::io::AsyncRead;

use edgelet_core::{LogOptions, ModuleRuntime};

use crate::error::{Error, ErrorKind};
use crate::Command;

pub struct Logs<M> {
    id: String,
    options: LogOptions,
    runtime: M,
}

impl<M> Logs<M> {
    pub fn new(id: String, options: LogOptions, runtime: M) -> Self {
        Logs {
            id,
            options,
            runtime,
        }
    }
}

impl<M> Command for Logs<M>
where
    M: 'static + ModuleRuntime + Clone,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(&mut self) -> Self::Future {
        let id = self.id.clone();
        let result = self
            .runtime
            .logs(&id, &self.options)
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .and_then(move |logs| {
                let chunked =
                    Chunked::new(logs.map_err(|_| io::Error::new(io::ErrorKind::Other, "unknown")));
                LogDecode::new(chunked)
                    .for_each(|chunk| {
                        match chunk {
                            LogChunk::Stdin(b)
                            | LogChunk::Stdout(b)
                            | LogChunk::Stderr(b)
                            | LogChunk::Unknown(b) => io::stdout().write(&b)?,
                        };
                        Ok(())
                    })
                    .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            });
        Box::new(result)
    }
}

/// Logs parser
/// Logs are emitted with a simple header to specify stdout or stderr
///
///
/// 01 00 00 00 00 00 00 1f 52 6f 73 65 73 20 61 72  65 ...
/// │  ─────┬── ─────┬─────  R  o  s  e  s     a  r   e ...
/// │       │        │
/// └stdout │        │
///         │        └ 0x0000001f = log message is 31 bytes
///       unused
///
/// The following set of structs converts a `Stream<&[u8]>` into a `Stream<LogChunk>`
/// by implementing [`AsyncRead`] on `Stream<&[u8]>` and then using the `length_delimited`
/// decoder in tokio to emit [`BytesMut`] with complete frames. The [`LogChunk`]
/// is then constructed from these [`BytesMut`]s

#[derive(Debug, PartialEq)]
enum LogChunk {
    Stdin(Bytes),
    Stdout(Bytes),
    Stderr(Bytes),
    Unknown(Bytes),
}

struct LogDecode<T: AsyncRead> {
    inner: FramedRead<T, length_delimited::LengthDelimitedCodec>,
}

impl<T: AsyncRead> LogDecode<T> {
    pub fn new(inner: T) -> Self {
        let delimited = length_delimited::Builder::new()
            .length_field_offset(4)
            .length_field_length(4)
            .length_adjustment(8)
            .num_skip(0)
            .new_read(inner);
        LogDecode { inner: delimited }
    }
}

impl<T: AsyncRead> Stream for LogDecode<T> {
    type Item = LogChunk;
    type Error = io::Error;

    fn poll(&mut self) -> Poll<Option<Self::Item>, Self::Error> {
        // This parser assumes that the length_delimited decoder
        // wraps this decoder. In this case, the BytesMut is
        // guaranteed to have a full frame worth of bytes.
        // We can simply read the needed bytes out of the BytesMut
        // to construct the parsed Frame.

        let option = try_ready!(self.inner.poll());
        let result = option.map(|bytes| {
            let mut buf = bytes.into_buf();
            let stream_type = buf.get_u8();
            buf.advance(3);
            let _length = buf.get_u32_be();
            let payload: Bytes = buf.collect();

            match stream_type {
                0 => LogChunk::Stdin(payload),
                1 => LogChunk::Stdout(payload),
                2 => LogChunk::Stderr(payload),
                _ => LogChunk::Unknown(payload),
            }
        });
        Ok(Async::Ready(result))
    }
}

struct Chunked<S, C>
where
    C: AsRef<[u8]>,
    S: Stream<Item = C, Error = io::Error>,
{
    inner: S,
    remaining: Option<Bytes>,
}

impl<S, C> Chunked<S, C>
where
    C: AsRef<[u8]>,
    S: Stream<Item = C, Error = io::Error>,
{
    pub fn new(inner: S) -> Self {
        Chunked {
            inner,
            remaining: None,
        }
    }

    fn read_remaining(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        let (result, r) = if let Some(ref remaining) = self.remaining {
            let amt = cmp::min(remaining.len(), buf.len());
            let (a, b) = remaining.split_at(amt);
            buf[..amt].copy_from_slice(a);

            if b.is_empty() {
                (Ok(amt), None)
            } else {
                (Ok(amt), Some(Bytes::from(b)))
            }
        } else {
            (Ok(0), None)
        };
        self.remaining = r;
        result
    }
}

impl<S, C> io::Read for Chunked<S, C>
where
    C: AsRef<[u8]>,
    S: Stream<Item = C, Error = io::Error>,
{
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        match self.inner.poll() {
            Ok(Async::Ready(Some(ref t))) => {
                let read = self.read_remaining(buf)?;
                let (result, new_remaining) = if let Some(ref mut remaining) = self.remaining {
                    // There's still some data waiting to be written into the read buffer.
                    // Append to the remaining buffer
                    // Return the amount read from remaining
                    let mut r = BytesMut::with_capacity(remaining.len() + t.as_ref().len());
                    r.put_slice(remaining);
                    r.put_slice(t.as_ref());
                    (Ok(read), Some(r.freeze()))
                } else {
                    // The remaining buffer was cleared.
                    // Attempt to read everything from the poll and add to the remaining buffer
                    // if needed.
                    let data = Bytes::from(t.as_ref());
                    let amt = cmp::min(data.len(), buf.len() - read);
                    let (a, b) = data.split_at(amt);
                    buf[read..read + amt].copy_from_slice(a);

                    if b.is_empty() {
                        (Ok(amt + read), None)
                    } else {
                        (Ok(amt + read), Some(Bytes::from(b)))
                    }
                };
                self.remaining = new_remaining;
                result
            }
            Ok(Async::Ready(None)) => self.read_remaining(buf),
            Ok(Async::NotReady) => Err(io::Error::from(io::ErrorKind::WouldBlock)),
            Err(e) => Err(e),
        }
    }
}

impl<S, C> AsyncRead for Chunked<S, C>
where
    C: AsRef<[u8]>,
    S: Stream<Item = C, Error = io::Error>,
{
}

#[cfg(test)]
mod tests {
    use super::*;

    use std::io::Read;

    use futures::stream::iter_ok;

    #[test]
    fn smoke_test() {
        let chunks = vec![
            &[0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0d, b'R', b'o'][..],
            &b"ses are"[..],
            &[b' ', b'r', b'e', b'd', 0x02, 0x00][..],
            &[0x00, 0x00, 0x00, 0x00, 0x00, 0x10][..],
            &b"violets"[..],
            &b" are blue"[..],
        ];

        let stream = iter_ok::<Vec<&[u8]>, io::Error>(chunks);
        let decoded = LogDecode::new(Chunked::new(stream))
            .collect()
            .wait()
            .unwrap();
        let expected = vec![
            LogChunk::Stdout(Bytes::from("Roses are red")),
            LogChunk::Stderr(Bytes::from("violets are blue")),
        ];

        assert_eq!(expected, decoded);
    }

    #[test]
    fn test_read() {
        let chunks = vec![
            &b"Ro"[..],
            &b"ses are"[..],
            &b" red"[..],
            &b" violets"[..],
            &b" are blue"[..],
        ];

        let mut stream = Chunked::new(iter_ok::<Vec<&[u8]>, io::Error>(chunks));
        let read_buffer = &mut [0_u8; 30];

        for slice in &mut read_buffer.chunks_mut(2) {
            stream.read_exact(slice).unwrap();
        }
        assert_eq!(b"Roses are red violets are blue", read_buffer);
    }
}
