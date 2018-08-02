// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]
#![cfg(windows)]

extern crate bytes;
extern crate futures;
extern crate mio_named_pipes;
extern crate tokio_core;
extern crate tokio_io;
extern crate winapi;

use std::convert::AsRef;
use std::fs::OpenOptions;
use std::io::{self, Read, Write};
use std::iter::once;
use std::os::windows::prelude::*;
use std::path::Path;
use std::time::Duration;

use bytes::{Buf, BufMut};
use futures::{Async, Poll};
use mio_named_pipes::NamedPipe;
use tokio_core::reactor::{Handle, PollEvented};
use tokio_io::{AsyncRead, AsyncWrite};
use winapi::um::namedpipeapi::WaitNamedPipeW;
use winapi::um::winbase::*;

const ERROR_PIPE_BUSY: i32 = 0xE7;
const PIPE_WAIT_TIMEOUT_MS: u32 = 10 * 1000;

#[derive(Debug)]
pub struct PipeStream {
    io: PollEvented<NamedPipe>,
}

impl PipeStream {
    pub fn connect<P: AsRef<Path>>(
        path: P,
        handle: &Handle,
        timeout: Option<Duration>,
    ) -> io::Result<PipeStream> {
        let timeout = timeout
            .map(|t| (t.as_secs() as u32) + t.subsec_millis())
            .unwrap_or(PIPE_WAIT_TIMEOUT_MS);
        let pipe_path: Vec<u16> = path
            .as_ref()
            .as_os_str()
            .encode_wide()
            .chain(once(0))
            .collect();

        unsafe {
            WaitNamedPipeW(pipe_path.as_ptr(), timeout);
        }

        let mut options = OpenOptions::new();
        options
            .read(true)
            .write(true)
            .custom_flags(FILE_FLAG_OVERLAPPED);

        match options.open(path.as_ref()) {
            Err(err) => {
                if let Some(code) = err.raw_os_error() {
                    if code == ERROR_PIPE_BUSY {
                        unsafe {
                            WaitNamedPipeW(pipe_path.as_ptr(), timeout);
                        }
                        return PipeStream::connect(
                            path,
                            handle,
                            Some(Duration::from_millis(u64::from(timeout))),
                        );
                    }
                }

                Err(err)
            }
            Ok(file) => {
                let named_pipe = unsafe { NamedPipe::from_raw_handle(file.into_raw_handle()) };

                Ok(PipeStream {
                    io: PollEvented::new(named_pipe, handle)?,
                })
            }
        }
    }

    pub fn poll_read(&self) -> Async<()> {
        self.io.poll_read()
    }

    pub fn poll_write(&self) -> Async<()> {
        self.io.poll_write()
    }

    pub fn disconnect(&self) -> io::Result<()> {
        self.io.get_ref().disconnect()
    }

    pub fn io_mut(&mut self) -> &mut PollEvented<NamedPipe> {
        &mut self.io
    }
}

impl Read for PipeStream {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        self.io.read(buf)
    }
}

impl Write for PipeStream {
    fn write(&mut self, buf: &[u8]) -> io::Result<usize> {
        self.io.write(buf)
    }

    fn flush(&mut self) -> io::Result<()> {
        self.io.flush()
    }
}

impl<'a> Read for &'a PipeStream {
    fn read(&mut self, buf: &mut [u8]) -> io::Result<usize> {
        (&self.io).read(buf)
    }
}

impl<'a> Write for &'a PipeStream {
    fn write(&mut self, buf: &[u8]) -> io::Result<usize> {
        (&self.io).write(buf)
    }

    fn flush(&mut self) -> io::Result<()> {
        (&self.io).flush()
    }
}

impl AsyncRead for PipeStream {
    unsafe fn prepare_uninitialized_buffer(&self, _: &mut [u8]) -> bool {
        false
    }

    fn read_buf<B: BufMut>(&mut self, buf: &mut B) -> Poll<usize, io::Error> {
        if let Async::NotReady = <PipeStream>::poll_read(self) {
            return Ok(Async::NotReady);
        }

        unsafe {
            match self.io_mut().get_mut().read(buf.bytes_mut()) {
                Err(e) => {
                    if e.kind() == io::ErrorKind::WouldBlock {
                        self.io.need_read();
                        Ok(Async::NotReady)
                    } else {
                        Err(e)
                    }
                }
                Ok(size) => {
                    buf.advance_mut(size);
                    Ok(size.into())
                }
            }
        }
    }
}

impl AsyncWrite for PipeStream {
    fn shutdown(&mut self) -> Poll<(), io::Error> {
        Ok(().into())
    }

    fn write_buf<B: Buf>(&mut self, buf: &mut B) -> Poll<usize, io::Error> {
        if let Async::NotReady = <PipeStream>::poll_write(self) {
            return Ok(Async::NotReady);
        }

        match self.io_mut().get_mut().write(buf.bytes()) {
            Err(e) => {
                if e.kind() == io::ErrorKind::WouldBlock {
                    self.io.need_write();
                    Ok(Async::NotReady)
                } else {
                    Err(e)
                }
            }
            Ok(size) => {
                buf.advance(size);
                Ok(size.into())
            }
        }
    }
}
