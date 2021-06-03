// Copyright (c) Microsoft. All rights reserved.

use std::ffi::OsString;
use std::fs::File;
use std::io::Seek;
use std::io::SeekFrom;
use std::io::Write;
use std::io::{Cursor, Read};
use std::path::Path;

use failure::Fail;
use zip::{write::FileOptions, CompressionMethod, ZipWriter};

use edgelet_core::{LogOptions, ModuleRuntime};

use crate::error::{Error, ErrorKind};
use crate::runtime_util::{get_modules, write_logs};
use crate::shell_util::{
    get_docker_networks, write_check, write_inspect, write_network_inspect, write_system_log,
};

const SYSTEM_MODULES: &[(&str, &str)] = &[
    ("aziot-keyd", "aziot-keyd"),
    ("aziot-certd", "aziot-certd"),
    ("aziot-identityd", "aziot-identityd"),
    ("aziot-edged", "aziot-edged"),
    ("docker", "docker"),
];

/// # Errors
///
/// Will return `Err` if unable to collect support bundle
pub async fn make_bundle<M>(
    output_location: OutputLocation,
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    runtime: M,
) -> Result<(Box<dyn Read + Send>, u64), Error>
where
    M: ModuleRuntime + Clone + Send + Sync,
{
    match output_location {
        OutputLocation::File(location) => {
            let buffer = File::create(Path::new(&location))
                .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
            let mut zip_writer = ZipWriter::new(buffer);

            let (reader, size) = write_all(
                &mut zip_writer,
                log_options,
                include_ms_only,
                verbose,
                iothub_hostname,
                runtime,
            )
            .await?;

            Ok((Box::new(reader), size))
        }
        OutputLocation::Memory => {
            let buffer = Box::new(Cursor::new(Vec::new()));
            let mut zip_writer = ZipWriter::new(buffer);

            let (reader, size) = write_all(
                &mut zip_writer,
                log_options,
                include_ms_only,
                verbose,
                iothub_hostname,
                runtime,
            )
            .await?;

            Ok((Box::new(reader), size))
        }
    }
}

async fn write_all<W, M>(
    mut zip_writer: &mut ZipWriter<W>,
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    runtime: M,
) -> Result<(W, u64), Error>
where
    W: Write + Seek + Send,
    M: ModuleRuntime + Clone + Send + Sync,
{
    let file_options = FileOptions::default().compression_method(CompressionMethod::Deflated);

    // Get Check
    zip_writer
        .start_file("check.json", file_options)
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
    write_check(&mut zip_writer, iothub_hostname, verbose).await?;

    // Get all modules
    for module_name in get_modules(&runtime, include_ms_only).await? {
        // Write module logs
        zip_writer
            .start_file(format!("logs/{}_log.txt", module_name), file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
        write_logs(&runtime, &module_name, &log_options, &mut zip_writer).await?;

        // write module inspect
        write_inspect(&module_name, &mut zip_writer, &file_options, verbose).await?;
    }

    // Get all docker network inspects
    for network_name in get_docker_networks().await? {
        write_network_inspect(&network_name, &mut zip_writer, &file_options, verbose).await?;
    }

    // Get logs for system modules
    for (name, unit) in SYSTEM_MODULES {
        write_system_log(
            name,
            unit,
            &log_options,
            &mut zip_writer,
            &file_options,
            verbose,
        )
        .await?;
    }

    // Finilize buffer and set cursur to 0 for reading.
    let mut buffer = zip_writer
        .finish()
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
    let len = buffer
        .seek(SeekFrom::Current(0))
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
    buffer
        .seek(SeekFrom::Start(0))
        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

    let result = (buffer, len);
    Ok(result)
}

#[derive(Clone, Debug, PartialEq)]
pub enum OutputLocation {
    File(OsString),
    Memory,
}
