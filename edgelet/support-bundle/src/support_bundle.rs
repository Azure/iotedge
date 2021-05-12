// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::ffi::OsString;
use std::fs::File;
use std::io::{Cursor, Read, Seek, Write};
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
    let buffer = Cursor::new(Vec::new());

    // Wrap buffer in zip_writer
    let file_options = FileOptions::default().compression_method(CompressionMethod::Deflated);
    let mut zip_writer = ZipWriter::new(buffer);

    // Write all files
    zip_writer
        .start_file("check.json", file_options)
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
    write_check(&mut zip_writer, iothub_hostname).await?;

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

async fn write_check(
    writer: &mut impl Write,
    iothub_hostname: Option<String>,
) -> Result<(), Error> {
    print_verbose("Calling iotedge check");

    let mut iotedge = env::args().next().unwrap();
    if iotedge.contains("aziot-edged") {
        print_verbose("Calling iotedge check from edgelet, using iotedge from path");
        iotedge = "iotedge".to_string();
    }

    let mut check = ShellCommand::new(iotedge);
    check.arg("check").args(&["-o", "json"]);

    if let Some(host_name) = iothub_hostname {
        check.args(&["--iothub-hostname", &host_name]);
    }
    let check = check
        .output()
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

    writer
        .write_all(&check.stdout)
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
    writer
        .write_all(&check.stderr)
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

    print_verbose("Wrote check output to file");
    Ok(())
}

fn print_verbose<S>(message: S)
where
    S: std::fmt::Display,
{
    if true {
        println!("{}", message);
    }
}
