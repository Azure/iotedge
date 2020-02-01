// Copyright (c) Microsoft. All rights reserved.

#![cfg(windows)]
#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::module_name_repetitions,
    clippy::must_use_candidate,
    clippy::too_many_lines,
    clippy::use_self
)]

pub mod error;
mod handle;

use std::ffi::{OsStr, OsString};
use std::iter::once;
use std::os::windows::ffi::OsStrExt;
use std::ptr;

use failure::ResultExt;
use log::{Level, LevelFilter, Log, Metadata, Record};
use winapi::shared::minwindef::{DWORD, WORD};
use winapi::um::errhandlingapi::GetLastError;
use winapi::um::winbase::{DeregisterEventSource, RegisterEventSourceW, ReportEventW};
use winapi::um::winnt::{
    EVENTLOG_ERROR_TYPE, EVENTLOG_INFORMATION_TYPE, EVENTLOG_SUCCESS, EVENTLOG_WARNING_TYPE,
};

use edgelet_utils::ensure_not_empty_with_context;

use crate::error::{Error, ErrorKind};
use crate::handle::Handle;

pub struct EventLogger {
    name: String,
    handle: Handle,
    min_level: Level,
}

impl EventLogger {
    pub fn new(name: String, min_level: &str) -> Result<Self, Error> {
        ensure_not_empty_with_context(&name, || ErrorKind::InvalidLogName(name.clone()))?;

        let wide_name: Vec<u16> = OsStr::new(&name).encode_wide().chain(once(0)).collect();

        let handle = unsafe { RegisterEventSourceW(ptr::null(), wide_name.as_ptr()) };
        if handle.is_null() {
            Err(Error::from(ErrorKind::RegisterEventSource(unsafe {
                GetLastError()
            })))
        } else {
            Ok(EventLogger {
                name,
                handle: Handle::new(handle),
                min_level: min_level
                    .parse::<Level>()
                    .with_context(|_| ErrorKind::InvalidLogLevel(min_level.to_string()))?,
            })
        }
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn init(self) -> Result<(), Error> {
        log::set_max_level(LevelFilter::Trace);
        log::set_boxed_logger(Box::new(self)).context(ErrorKind::RegisterGlobalLogger)?;
        Ok(())
    }
}

impl Log for EventLogger {
    fn enabled(&self, metadata: &Metadata<'_>) -> bool {
        metadata.level() <= self.min_level
    }

    fn log(&self, record: &Record<'_>) {
        if self.enabled(record.metadata()) {
            let message: Vec<u16> =
                OsString::from(format!("{} -- {}", record.target(), record.args()))
                    .encode_wide()
                    .chain(once(0))
                    .collect();

            unsafe {
                let mut strings = vec![message.as_ptr(), ptr::null()];

                ReportEventW(
                    self.handle.raw(),
                    log_level_to_type(record.level()),
                    0,
                    log_level_to_eventid(record.level()),
                    ptr::null_mut(),
                    1,
                    0,
                    strings.as_mut_ptr(),
                    ptr::null_mut(),
                );
            }
        }
    }

    fn flush(&self) {}
}

impl Drop for EventLogger {
    fn drop(&mut self) {
        unsafe {
            DeregisterEventSource(self.handle.raw());
        }
    }
}

fn log_level_to_eventid(level: Level) -> DWORD {
    // The values returned here must match the event message IDs specified
    // in the event_messages.mc file.
    match level {
        Level::Error => 1,
        Level::Warn => 2,
        Level::Info => 3,
        Level::Debug => 4,
        Level::Trace => 5,
    }
}

fn log_level_to_type(level: Level) -> WORD {
    match level {
        Level::Error => EVENTLOG_ERROR_TYPE,
        Level::Warn => EVENTLOG_WARNING_TYPE,
        Level::Info => EVENTLOG_INFORMATION_TYPE,
        Level::Trace | Level::Debug => EVENTLOG_SUCCESS,
    }
}
