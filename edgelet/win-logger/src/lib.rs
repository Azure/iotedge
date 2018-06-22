// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]
#![cfg(windows)]

#[macro_use]
extern crate failure;
// NOTE: For some reason if the extern crate statement for edgelet_utils is moved
// above the one for "failure" above then things stop compiling.
#[macro_use]
extern crate edgelet_utils;
extern crate log;
extern crate winapi;

pub mod error;
mod handle;

use std::ffi::{OsStr, OsString};
use std::io::Error as IoError;
use std::iter::once;
use std::os::windows::ffi::OsStrExt;
use std::ptr;

use error::Error;
use handle::Handle;
use log::{Level, LevelFilter, Log, Metadata, Record};
use winapi::shared::minwindef::{DWORD, WORD};
use winapi::um::winbase::{DeregisterEventSource, RegisterEventSourceW, ReportEventW};
use winapi::um::winnt::{
    EVENTLOG_ERROR_TYPE, EVENTLOG_INFORMATION_TYPE, EVENTLOG_SUCCESS, EVENTLOG_WARNING_TYPE,
};

pub struct EventLogger {
    name: String,
    handle: Handle,
    min_level: Level,
}

impl EventLogger {
    pub fn new(name: &str, min_level: &str) -> Result<EventLogger, Error> {
        let wide_name: Vec<u16> = OsStr::new(ensure_not_empty!(name))
            .encode_wide()
            .chain(once(0))
            .collect();

        let handle = unsafe { RegisterEventSourceW(ptr::null(), wide_name.as_ptr()) };
        if handle.is_null() {
            Err(Error::from(IoError::last_os_error()))
        } else {
            Ok(EventLogger {
                name: name.to_string(),
                handle: Handle::new(handle),
                min_level: min_level.parse()?,
            })
        }
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn init(self) -> Result<(), Error> {
        log::set_max_level(LevelFilter::Trace);
        Ok(log::set_boxed_logger(Box::new(self))?)
    }
}

impl Log for EventLogger {
    fn enabled(&self, metadata: &Metadata) -> bool {
        metadata.level() <= self.min_level
    }

    fn log(&self, record: &Record) {
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
        Level::Trace => EVENTLOG_SUCCESS,
        Level::Debug => EVENTLOG_SUCCESS,
    }
}
