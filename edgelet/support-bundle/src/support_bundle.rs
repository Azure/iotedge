// Copyright (c) Microsoft. All rights reserved.

use std::fs::File;
use std::io::{Cursor, Read, Seek, SeekFrom, Write};
use std::path::PathBuf;

use anyhow::Context;
use zip::{write::FileOptions, CompressionMethod, ZipWriter};

use edgelet_core::{LogOptions, ModuleRuntime};

use crate::error::Error;
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
pub async fn make_bundle(
    output_location: OutputLocation,
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    runtime: &impl ModuleRuntime,
) -> anyhow::Result<(Box<dyn Read + Send>, u64)> {
    match output_location {
        OutputLocation::File(location) => {
            let buffer = File::create(&location).context(Error::SupportBundle)?;
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

async fn write_all<W>(
    mut zip_writer: &mut ZipWriter<W>,
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    runtime: &impl ModuleRuntime,
) -> anyhow::Result<(W, u64)>
where
    W: Write + Seek + Send,
{
    let file_options = FileOptions::default()
        .compression_method(CompressionMethod::Deflated)
        // NOTE: Without this option, uncompressed file sizes are
        // limited to 4GB.
        .large_file(true);

    // Get Check
    zip_writer
        .start_file("check.json", file_options)
        .context(Error::SupportBundle)?;
    write_check(&mut zip_writer, iothub_hostname, verbose).await?;

    // Get all modules
    for module_name in get_modules(runtime, include_ms_only).await {
        // Write module logs
        zip_writer
            .start_file(format!("logs/{}_log.txt", module_name), file_options)
            .context(Error::SupportBundle)?;
        write_logs(runtime, &module_name, &log_options, &mut zip_writer).await?;

        // write module inspect
        write_inspect(&module_name, zip_writer, &file_options, verbose).await?;
    }

    // Get all docker network inspects
    for network_name in get_docker_networks().await? {
        write_network_inspect(&network_name, zip_writer, &file_options, verbose).await?;
    }

    // Get logs for system modules
    for (name, unit) in SYSTEM_MODULES {
        write_system_log(name, unit, &log_options, zip_writer, &file_options, verbose).await?;
    }

    // Finilize buffer and set cursur to 0 for reading.
    let mut buffer = zip_writer.finish().context(Error::SupportBundle)?;
    let len = buffer
        .seek(SeekFrom::Current(0))
        .context(Error::SupportBundle)?;
    buffer
        .seek(SeekFrom::Start(0))
        .context(Error::SupportBundle)?;

    let result = (buffer, len);
    Ok(result)
}

#[derive(Clone, Debug, Eq, PartialEq)]
pub enum OutputLocation {
    File(PathBuf),
    Memory,
}
