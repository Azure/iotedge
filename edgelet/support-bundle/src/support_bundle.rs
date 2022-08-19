// Copyright (c) Microsoft. All rights reserved.

use std::ffi::OsString;
use std::fs::File;
use std::io::{Cursor, Read, Seek, SeekFrom, Write};
use std::path::Path;

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
            let buffer = File::create(Path::new(&location)).context(Error::SupportBundle)?;
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
    let file_options = FileOptions::default().compression_method(CompressionMethod::Deflated);

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

#[derive(Clone, Debug, PartialEq)]
pub enum OutputLocation {
    File(OsString),
    Memory,
}

// #[cfg(test)]
// mod tests {
//     use std::fs;
//     use std::io;
//     use std::path::PathBuf;
//     use std::str;

//     use edgelet_core::ModuleAction;
//     use futures::sync::mpsc;
//     use regex::Regex;
//     use tempfile::tempdir;

//     use edgelet_core::{MakeModuleRuntime, ModuleRuntimeState};
//     use edgelet_test_utils::module::{TestConfig, TestModule, TestRuntime, TestSettings};

//     use super::{
//         make_bundle, pull_logs, Fail, File, Future, LogOptions, LogTail, OsString, OutputLocation,
//     };

//     #[allow(dead_code)]
//     #[derive(Clone, Copy, Debug, Fail)]
//     pub enum Error {
//         #[fail(display = "General error")]
//         General,
//     }

//     #[test]
//     fn folder_structure() {
//         let module_name = "test-module";
//         let runtime = make_runtime(module_name);
//         let tmp_dir = tempdir().unwrap();
//         let file_path = tmp_dir
//             .path()
//             .join("iotedge_bundle.zip")
//             .to_str()
//             .unwrap()
//             .to_owned();

//         let bundle = make_bundle(
//             OutputLocation::File(OsString::from(file_path.to_owned())),
//             LogOptions::default(),
//             false,
//             false,
//             None,
//             runtime,
//         );

//         tokio::runtime::current_thread::Runtime::new()
//             .unwrap()
//             .block_on(bundle)
//             .unwrap();

//         let extract_path = tmp_dir.path().join("bundle").to_str().unwrap().to_owned();

//         extract_zip(&file_path, &extract_path);

//         // expect logs
//         let mod_log = fs::read_to_string(
//             PathBuf::from(&extract_path)
//                 .join("logs")
//                 .join(format!("{}_log.txt", module_name)),
//         )
//         .unwrap();
//         assert_eq!("Roses are redviolets are blue", mod_log);

//         for name in &[
//             "aziot-edged",
//             "aziot-certd",
//             "aziot-keyd",
//             "aziot-identityd",
//             "docker",
//         ] {
//             let logfile = Regex::new(format!(r"{}.*\.txt", name).as_str()).unwrap();
//             assert!(
//                 fs::read_dir(PathBuf::from(&extract_path).join("logs"))
//                     .unwrap()
//                     .map(|file| file
//                         .unwrap()
//                         .path()
//                         .file_name()
//                         .unwrap()
//                         .to_str()
//                         .unwrap()
//                         .to_owned())
//                     .any(|f| logfile.is_match(&f)),
//                 format!("Missing log file: {}*.txt", name)
//             );
//         }

//         //expect inspect
//         let module_in_inspect = Regex::new(&format!(r"{}.*\.json", module_name)).unwrap();
//         assert!(fs::read_dir(PathBuf::from(&extract_path).join("inspect"))
//             .unwrap()
//             .map(|file| file
//                 .unwrap()
//                 .path()
//                 .file_name()
//                 .unwrap()
//                 .to_str()
//                 .unwrap()
//                 .to_owned())
//             .any(|f| module_in_inspect.is_match(&f)));

//         // expect check
//         File::open(PathBuf::from(&extract_path).join("check.json")).unwrap();

//         // expect network inspect
//         let network_in_inspect = Regex::new(r".*\.json").unwrap();
//         assert!(fs::read_dir(PathBuf::from(&extract_path).join("network"))
//             .unwrap()
//             .map(|file| file
//                 .unwrap()
//                 .path()
//                 .file_name()
//                 .unwrap()
//                 .to_str()
//                 .unwrap()
//                 .to_owned())
//             .any(|f| network_in_inspect.is_match(&f)));
//     }

//     #[test]
//     fn get_logs() {
//         let module_name = "test-module";
//         let runtime = make_runtime(module_name);

//         let options = LogOptions::new()
//             .with_follow(false)
//             .with_tail(LogTail::Num(0))
//             .with_since(0);

//         let result: Vec<u8> = pull_logs(&runtime, module_name, &options, Vec::new())
//             .wait()
//             .unwrap();
//         let result_str = str::from_utf8(&result).unwrap();
//         assert_eq!("Roses are redviolets are blue", result_str);
//     }

//     fn make_runtime(module_name: &str) -> TestRuntime<Error, TestSettings> {
//         let logs = vec![
//             &[0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0d, b'R', b'o'][..],
//             &b"ses are"[..],
//             &[b' ', b'r', b'e', b'd', 0x02, 0x00][..],
//             &[0x00, 0x00, 0x00, 0x00, 0x00, 0x10][..],
//             &b"violets"[..],
//             &b" are blue"[..],
//         ];

//         let state: Result<ModuleRuntimeState, Error> = Ok(ModuleRuntimeState::default());
//         let config = TestConfig::new(format!("microsoft/{}", module_name));
//         let module = TestModule::new_with_logs(module_name.to_owned(), config, state, logs);

//         let (create_socket_channel_snd, _create_socket_channel_rcv) =
//             mpsc::unbounded::<ModuleAction>();

//         TestRuntime::make_runtime(TestSettings::new(), create_socket_channel_snd)
//             .wait()
//             .unwrap()
//             .with_module(Ok(module))
//     }

//     // From https://github.com/mvdnes/zip-rs/blob/master/examples/extract.rs
//     fn extract_zip(source: &str, destination: &str) {
//         let fname = std::path::Path::new(source);
//         let file = File::open(&fname).unwrap();
//         let mut archive = zip::ZipArchive::new(file).unwrap();

//         for i in 0..archive.len() {
//             let mut file = archive.by_index(i).unwrap();

//             let filename = {
//                 let filename = std::path::Path::new(file.name());
//                 // Assert that the path has no components other than Normal
//                 let mut components = filename.components();
//                 assert!(components
//                     .all(|component| matches!(component, std::path::Component::Normal(_))));
//                 filename
//             };

//             let outpath = PathBuf::from(destination).join(filename);

//             if let Some(parent) = outpath.parent() {
//                 fs::create_dir_all(&parent).unwrap();
//             }

//             if file.is_dir() {
//                 fs::create_dir_all(&outpath).unwrap();
//             } else if file.is_file() {
//                 let mut outfile = fs::File::create(&outpath).unwrap();
//                 io::copy(&mut file, &mut outfile).unwrap();
//             }

//             // Get and Set permissions
//             #[cfg(unix)]
//             {
//                 use std::os::unix::fs::PermissionsExt;

//                 if let Some(mode) = file.unix_mode() {
//                     fs::set_permissions(&outpath, fs::Permissions::from_mode(mode)).unwrap();
//                 }
//             }
//         }
//     }
// }
