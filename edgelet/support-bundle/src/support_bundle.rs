// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::ffi::OsString;
use std::fs::File;
use std::io::{Cursor, Read, Seek};
use std::path::Path;

// TODO: make tokio
use std::process::Command as ShellCommand;

use chrono::{DateTime, Local, NaiveDateTime, Utc};
use failure::Fail;
use futures::{Future, Stream};
use zip::{write::FileOptions, CompressionMethod, ZipWriter};

use edgelet_core::{LogOptions, LogTail, Module, ModuleRuntime};

use crate::error::{Error, ErrorKind};
use crate::logs::pull_logs;

pub async fn make_bundle<M>(
    output_location: OutputLocation,
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    runtime: M,
) -> Result<(impl Read + Send, u64), Error>
where
    M: ModuleRuntime + Clone + Send + Sync,
{
    let writer = Cursor::new(Vec::new());

    let result = (writer, 0);
    Ok(result)
}

#[derive(Clone, Debug, PartialEq)]
pub enum OutputLocation {
    File(OsString),
    Memory,
}
