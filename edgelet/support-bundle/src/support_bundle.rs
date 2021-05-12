// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::ffi::OsString;
use std::fs::File;
use std::io::{Cursor, Read, Seek, Write};
use std::path::Path;

use chrono::{DateTime, Local, NaiveDateTime, Utc};
use failure::Fail;
use futures::{Future, Stream};
use zip::{write::FileOptions, CompressionMethod, ZipWriter};

use edgelet_core::{LogOptions, LogTail, Module, ModuleRuntime};

use crate::error::{Error, ErrorKind};
use crate::runtime_util::{get_modules, write_check, write_logs};

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
    let buffer = Cursor::new(Vec::new());

    // Wrap buffer in zip_writer
    let file_options = FileOptions::default().compression_method(CompressionMethod::Deflated);
    let mut zip_writer = ZipWriter::new(buffer);

    // Write all files
    {
        // Get Check
        zip_writer
            .start_file("check.json", file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
        write_check(&mut zip_writer, iothub_hostname).await?;

        for module_name in get_modules(&runtime, include_ms_only).await? {
            // Get all logs
            zip_writer
                .start_file(format!("logs/{}_log.txt", module_name), file_options)
                .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
            write_logs(&runtime, &module_name, &log_options, &mut zip_writer).await?;
        }
    }
    // Finilize buffer and set cursur to 0 for reading.
    let mut buffer = zip_writer
        .finish()
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
    let len = buffer.position();
    buffer.set_position(0);

    let result = (buffer, len);
    Ok(result)
}

#[derive(Clone, Debug, PartialEq)]
pub enum OutputLocation {
    File(OsString),
    Memory,
}
