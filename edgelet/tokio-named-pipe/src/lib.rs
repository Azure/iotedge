// Copyright (c) Microsoft. All rights reserved.

#![cfg(windows)]
#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::use_self)]

extern crate futures;
extern crate mio_named_pipes;
extern crate tokio;
extern crate winapi;

use std::convert::AsRef;
use std::fs::OpenOptions;
use std::io::{self, Read, Write};
use std::iter::once;
use std::os::windows::prelude::*;
use std::path::Path;
use std::time::Duration;

use futures::Poll;
use mio_named_pipes::NamedPipe;
use tokio::io::{AsyncRead, AsyncWrite};
use tokio::reactor::PollEvented2;
use winapi::um::namedpipeapi::WaitNamedPipeW;
use winapi::um::winbase::*;

const ERROR_PIPE_BUSY: i32 = 0xE7;
const PIPE_WAIT_TIMEOUT_MS: u32 = 10 * 1000;

#[derive(Debug)]
pub struct PipeStream {
    io: PollEvented2<NamedPipe>,
}

impl PipeStream {
    pub fn connect<P: AsRef<Path>>(path: P, timeout: Option<Duration>) -> io::Result<Self> {
        #[allow(clippy::cast_possible_truncation)]
        let timeout = timeout.map_or(PIPE_WAIT_TIMEOUT_MS, |t| {
            match t.as_secs() + u64::from(t.subsec_millis()) {
                t if t > u64::from(u32::max_value()) => u32::max_value(),
                t => t as u32,
            }
        });
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
                            Some(Duration::from_millis(u64::from(timeout))),
                        );
                    }
                }

                Err(err)
            }
            Ok(file) => {
                let named_pipe = unsafe { NamedPipe::from_raw_handle(file.into_raw_handle()) };

                Ok(PipeStream {
                    io: PollEvented2::new(named_pipe),
                })
            }
        }
    }

    pub fn disconnect(&self) -> io::Result<()> {
        self.io.get_ref().disconnect()
    }

    pub fn io_mut(&mut self) -> &mut PollEvented2<NamedPipe> {
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
}

impl AsyncWrite for PipeStream {
    fn shutdown(&mut self) -> Poll<(), io::Error> {
        Ok(().into())
    }
}
