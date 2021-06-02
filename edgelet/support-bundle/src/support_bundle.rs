// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::ffi::OsString;
use std::fs::File;
use std::io::{Cursor, Seek};
use std::path::Path;
use std::process::Command as ShellCommand;

use chrono::{DateTime, Local, NaiveDateTime, Utc};
use failure::Fail;
use futures::{Future, Stream};
use tokio::prelude::*;
use zip::{write::FileOptions, CompressionMethod, ZipWriter};

use edgelet_core::{LogOptions, LogTail, Module, ModuleRuntime};

use crate::error::{Error, ErrorKind};
use crate::logs::pull_logs;

pub fn make_bundle<M>(
    output_location: OutputLocation,
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    runtime: M,
) -> Box<dyn Future<Item = (Box<dyn Read + Send>, u64), Error = Error> + Send>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    match output_location {
        OutputLocation::File(location) => {
            let writer = future::result(
                File::create(Path::new(&location))
                    .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle))),
            );

            let state = writer.and_then(move |writer| {
                make_state(
                    log_options,
                    include_ms_only,
                    verbose,
                    iothub_hostname,
                    runtime,
                    writer,
                )
            });

            let bundle = state.and_then(BundleState::bundle_all);

            let read =
                bundle.and_then(|mut bundle| -> Result<(Box<dyn Read + Send>, u64), Error> {
                    let result: Box<dyn Read + Send> = Box::new(
                        bundle
                            .zip_writer
                            .finish()
                            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?,
                    );
                    Ok((result, 0)) // TODO: Get Size
                });

            Box::new(read)
        }
        OutputLocation::Memory => {
            let writer = future::ok(Cursor::new(Vec::new()));

            let state = writer.and_then(move |writer| {
                make_state(
                    log_options,
                    include_ms_only,
                    verbose,
                    iothub_hostname,
                    runtime,
                    writer,
                )
            });

            let bundle = state.and_then(BundleState::bundle_all);

            let read =
                bundle.and_then(|mut bundle| -> Result<(Box<dyn Read + Send>, u64), Error> {
                    let mut cursor = bundle
                        .zip_writer
                        .finish()
                        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;
                    let len = cursor.position();
                    cursor.set_position(0);
                    let reader: Box<dyn Read + Send> = Box::new(cursor);
                    Ok((reader, len))
                });

            Box::new(read)
        }
    }
}

fn make_state<M, W>(
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    runtime: M,
    writer: W,
) -> Result<BundleState<M, W>, Error>
where
    W: Write + Seek + Send,
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    let file_options = FileOptions::default().compression_method(CompressionMethod::Deflated);
    let zip_writer = ZipWriter::new(writer);

    Ok(BundleState {
        runtime,
        log_options,
        include_ms_only,
        verbose,
        iothub_hostname,
        file_options,
        zip_writer,
    })
}

struct BundleState<M, W>
where
    W: Write + Seek + Send,
{
    runtime: M,
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    file_options: FileOptions,
    zip_writer: ZipWriter<W>,
}

impl<M, W> BundleState<M, W>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
    W: Write + Seek + Send,
{
    fn bundle_all(self) -> impl Future<Item = Self, Error = Error> {
        future::ok(self)
            .and_then(Self::write_check)
            .and_then(Self::write_module_logs)
            .and_then(|s| s.write_system_log("aziot-keyd", "aziot-keyd"))
            .and_then(|s| s.write_system_log("aziot-certd", "aziot-certd"))
            .and_then(|s| s.write_system_log("aziot-identityd", "aziot-identityd"))
            .and_then(|s| s.write_system_log("aziot-edged", "aziot-edged"))
            .and_then(|s| s.write_system_log("docker", "docker"))
            .and_then(Self::write_all_inspects)
            .and_then(Self::write_all_network_inspects)
    }

    fn write_module_logs(self) -> impl Future<Item = Self, Error = Error> {
        /* Print status */
        if self.verbose {
            let since_time: DateTime<Utc> = DateTime::from_utc(
                NaiveDateTime::from_timestamp(self.log_options.since().into(), 0),
                Utc,
            );
            let since_local: DateTime<Local> = DateTime::from(since_time);
            let until_local = self.log_options.until().map(|until| {
                let until_time: DateTime<Utc> =
                    DateTime::from_utc(NaiveDateTime::from_timestamp(until.into(), 0), Utc);
                let until_local: DateTime<Local> = DateTime::from(until_time);
                until_local
            });
            let max_lines = if let LogTail::Num(tail) = self.log_options.tail() {
                format!("(maximum {} lines) ", tail)
            } else {
                "".to_owned()
            };
            let until_string = if let Some(until) = until_local {
                format!(" until {}", until)
            } else {
                "".to_owned()
            };
            println!(
                "Writing all logs {}since {} (local time {}){}",
                max_lines, since_time, since_local, until_string
            );
        }

        self.get_modules()
            .and_then(|(names, state)| stream::iter_ok(names).fold(state, Self::write_log_to_file))
    }

    fn write_all_inspects(self) -> impl Future<Item = Self, Error = Error> {
        self.get_modules().and_then(|(names, s2)| {
            stream::iter_ok(names).fold(s2, |s3, name| s3.write_inspect_to_file(&name))
        })
    }

    fn write_all_network_inspects(self) -> Result<Self, Error> {
        self.get_docker_networks().and_then(|(names, s2)| {
            names.into_iter().fold(Ok(s2), |s3, name| {
                if let Ok(s3) = s3 {
                    s3.write_docker_network_to_file(&name)
                } else {
                    s3
                }
            })
        })
    }

    fn get_modules(self) -> impl Future<Item = (Vec<String>, Self), Error = Error> {
        const MS_MODULES: &[&str] = &["edgeAgent", "edgeHub"];

        let include_ms_only = self.include_ms_only;

        let runtime_modules = self
            .runtime
            .list_with_details()
            .collect()
            .then(move |result| {
                future::ok(match result {
                    Ok(modules) => modules
                        .into_iter()
                        .map(|(module, _s)| module.name().to_owned())
                        .filter(move |name| {
                            !include_ms_only || MS_MODULES.iter().any(|ms| ms == name)
                        })
                        .collect(),
                    Err(_) => Vec::new(),
                })
            });

        runtime_modules.map(|names| (names, self))
    }

    fn write_log_to_file(
        state: Self,
        module_name: String,
    ) -> impl Future<Item = Self, Error = Error> {
        state.print_verbose(&format!("Writing {} logs to file", module_name));
        let BundleState {
            runtime,
            log_options,
            include_ms_only,
            verbose,
            iothub_hostname,
            file_options,
            mut zip_writer,
        } = state;

        zip_writer
            .start_file(format!("logs/{}_log.txt", module_name), file_options)
            .into_future()
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))
            .and_then(move |_| {
                pull_logs(&runtime, &module_name, &log_options, zip_writer).map(move |zw| {
                    let state = BundleState {
                        runtime,
                        log_options,
                        include_ms_only,
                        verbose,
                        iothub_hostname,
                        file_options,
                        zip_writer: zw,
                    };
                    state.print_verbose(&format!("Wrote {} logs to file", module_name));
                    state
                })
            })
    }

    fn write_system_log(mut self, name: &str, unit: &str) -> Result<Self, Error> {
        self.print_verbose(format!("Getting system logs for {}", name).as_str());
        let since_time: DateTime<Utc> = DateTime::from_utc(
            NaiveDateTime::from_timestamp(self.log_options.since().into(), 0),
            Utc,
        );
        let until_time: Option<DateTime<Utc>> = self
            .log_options
            .until()
            .map(|until| DateTime::from_utc(NaiveDateTime::from_timestamp(until.into(), 0), Utc));

        #[cfg(unix)]
        let command = {
            let mut command = ShellCommand::new("journalctl");
            command
                .arg("-a")
                .args(&["-u", unit])
                .args(&["-S", &since_time.format("%F %T").to_string()])
                .arg("--no-pager");
            if let Some(until) = until_time {
                command.args(&["-U", &until.format("%F %T").to_string()]);
            }

            command.output()
        };

        let (file_name, output) = if let Ok(result) = command {
            if result.status.success() {
                (format!("logs/{}.txt", name), result.stdout)
            } else {
                (format!("logs/{}_err.txt", name), result.stderr)
            }
        } else {
            let err_message = command.err().unwrap().to_string();
            println!(
                "Could not find system logs for {}. Including error in bundle.\nError message: {}",
                name, err_message
            );
            (
                format!("logs/{}_err.txt", name),
                err_message.as_bytes().to_vec(),
            )
        };

        self.zip_writer
            .start_file(file_name, self.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.zip_writer
            .write_all(&output)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.print_verbose(format!("Got logs for {}", name).as_str());
        Ok(self)
    }

    fn write_check(mut self) -> Result<Self, Error> {
        self.print_verbose("Calling iotedge check");

        let mut iotedge = env::args().next().unwrap();
        if iotedge.contains("aziot-edged") {
            self.print_verbose("Calling iotedge check from edgelet, using iotedge from path");
            iotedge = "iotedge".to_string();
        }

        let mut check = ShellCommand::new(iotedge);
        check.arg("check").args(&["-o", "json"]);

        if let Some(host_name) = self.iothub_hostname.clone() {
            check.args(&["--iothub-hostname", &host_name]);
        }
        let check = check
            .output()
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.zip_writer
            .start_file("check.json", self.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.zip_writer
            .write_all(&check.stdout)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.zip_writer
            .write_all(&check.stderr)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.print_verbose("Wrote check output to file");
        Ok(self)
    }

    fn write_inspect_to_file(mut self, module_name: &str) -> Result<Self, Error> {
        self.print_verbose(&format!("Running docker inspect for {}", module_name));
        let mut inspect = ShellCommand::new("docker");

        inspect.arg("inspect").arg(&module_name);
        let inspect = inspect.output();

        let (file_name, output) = if let Ok(result) = inspect {
            if result.status.success() {
                (format!("inspect/{}.json", module_name), result.stdout)
            } else {
                (format!("inspect/{}_err.json", module_name), result.stderr)
            }
        } else {
            let err_message = inspect.err().unwrap().to_string();
            println!(
                "Could not reach docker. Including error in bundle.\nError message: {}",
                err_message
            );
            (
                format!("inspect/{}_err_docker.txt", module_name),
                err_message.as_bytes().to_vec(),
            )
        };

        self.zip_writer
            .start_file(file_name, self.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.zip_writer
            .write_all(&output)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.print_verbose(&format!("Got docker inspect for {}", module_name));
        Ok(self)
    }

    fn get_docker_networks(self) -> Result<(Vec<String>, Self), Error> {
        let mut inspect = ShellCommand::new("docker");

        /***
         * Note: just like inspect, this assumes using windows containers on a windows machine.
         */
        #[cfg(windows)]
        inspect.args(&["-H", "npipe:////./pipe/iotedge_moby_engine"]);

        inspect.args(&["network", "ls"]);
        inspect.args(&["--format", "{{.Name}}"]);
        let inspect = inspect.output();

        let result = if let Ok(result) = inspect {
            if result.status.success() {
                String::from_utf8_lossy(&result.stdout).to_string()
            } else {
                println!(
                    "Could not find network names: {}",
                    String::from_utf8_lossy(&result.stderr)
                );
                "azure-iot-edge".to_owned()
            }
        } else {
            println!("Could not find network names: {}", inspect.err().unwrap());
            "azure-iot-edge".to_owned()
        };

        Ok((result.lines().map(String::from).collect(), self))
    }

    fn write_docker_network_to_file(mut self, network_name: &str) -> Result<Self, Error> {
        self.print_verbose(&format!(
            "Running docker network inspect for {}",
            network_name
        ));
        let mut inspect = ShellCommand::new("docker");

        /***
         * Note: just like inspect, this assumes using windows containers on a windows machine.
         */
        #[cfg(windows)]
        inspect.args(&["-H", "npipe:////./pipe/iotedge_moby_engine"]);

        inspect.args(&["network", "inspect", &network_name, "-v"]);
        let inspect = inspect.output();

        let (file_name, output) = if let Ok(result) = inspect {
            if result.status.success() {
                (format!("network/{}.json", network_name), result.stdout)
            } else {
                (format!("network/{}_err.json", network_name), result.stderr)
            }
        } else {
            let err_message = inspect.err().unwrap().to_string();
            println!(
                "Could not reach docker. Including error in bundle.\nError message: {}",
                err_message
            );
            (
                format!("network/{}_err_docker.txt", network_name),
                err_message.as_bytes().to_vec(),
            )
        };

        self.zip_writer
            .start_file(file_name, self.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.zip_writer
            .write_all(&output)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.print_verbose(&format!("Got docker network inspect for {}", network_name));
        Ok(self)
    }

    fn print_verbose(&self, message: &str) {
        if self.verbose {
            println!("{}", message);
        }
    }
}

#[derive(Clone, Debug, PartialEq)]
pub enum OutputLocation {
    File(OsString),
    Memory,
}

#[cfg(test)]
mod tests {
    use std::fs;
    use std::io;
    use std::path::PathBuf;
    use std::str;

    use regex::Regex;
    use tempfile::tempdir;

    use edgelet_core::{MakeModuleRuntime, ModuleRuntimeState};
    use edgelet_test_utils::module::{TestConfig, TestModule, TestRuntime, TestSettings};

    use super::{
        make_bundle, pull_logs, Fail, File, Future, LogOptions, LogTail, OsString, OutputLocation,
    };

    #[allow(dead_code)]
    #[derive(Clone, Copy, Debug, Fail)]
    pub enum Error {
        #[fail(display = "General error")]
        General,
    }

    #[test]
    fn folder_structure() {
        let module_name = "test-module";
        let runtime = make_runtime(module_name);
        let tmp_dir = tempdir().unwrap();
        let file_path = tmp_dir
            .path()
            .join("iotedge_bundle.zip")
            .to_str()
            .unwrap()
            .to_owned();

        let bundle = make_bundle(
            OutputLocation::File(OsString::from(file_path.to_owned())),
            LogOptions::default(),
            false,
            false,
            None,
            runtime,
        );

        tokio::runtime::current_thread::Runtime::new()
            .unwrap()
            .block_on(bundle)
            .unwrap();

        let extract_path = tmp_dir.path().join("bundle").to_str().unwrap().to_owned();

        extract_zip(&file_path, &extract_path);

        // expect logs
        let mod_log = fs::read_to_string(
            PathBuf::from(&extract_path)
                .join("logs")
                .join(format!("{}_log.txt", module_name)),
        )
        .unwrap();
        assert_eq!("Roses are redviolets are blue", mod_log);

        for name in &[
            "aziot-edged",
            "aziot-certd",
            "aziot-keyd",
            "aziot-identityd",
            "docker",
        ] {
            let logfile = Regex::new(format!(r"{}.*\.txt", name).as_str()).unwrap();
            assert!(
                fs::read_dir(PathBuf::from(&extract_path).join("logs"))
                    .unwrap()
                    .map(|file| file
                        .unwrap()
                        .path()
                        .file_name()
                        .unwrap()
                        .to_str()
                        .unwrap()
                        .to_owned())
                    .any(|f| logfile.is_match(&f)),
                format!("Missing log file: {}*.txt", name)
            );
        }

        //expect inspect
        let module_in_inspect = Regex::new(&format!(r"{}.*\.json", module_name)).unwrap();
        assert!(fs::read_dir(PathBuf::from(&extract_path).join("inspect"))
            .unwrap()
            .map(|file| file
                .unwrap()
                .path()
                .file_name()
                .unwrap()
                .to_str()
                .unwrap()
                .to_owned())
            .any(|f| module_in_inspect.is_match(&f)));

        // expect check
        File::open(PathBuf::from(&extract_path).join("check.json")).unwrap();

        // expect network inspect
        let network_in_inspect = Regex::new(r".*\.json").unwrap();
        assert!(fs::read_dir(PathBuf::from(&extract_path).join("network"))
            .unwrap()
            .map(|file| file
                .unwrap()
                .path()
                .file_name()
                .unwrap()
                .to_str()
                .unwrap()
                .to_owned())
            .any(|f| network_in_inspect.is_match(&f)));
    }

    #[test]
    fn get_logs() {
        let module_name = "test-module";
        let runtime = make_runtime(module_name);

        let options = LogOptions::new()
            .with_follow(false)
            .with_tail(LogTail::Num(0))
            .with_since(0);

        let result: Vec<u8> = pull_logs(&runtime, module_name, &options, Vec::new())
            .wait()
            .unwrap();
        let result_str = str::from_utf8(&result).unwrap();
        assert_eq!("Roses are redviolets are blue", result_str);
    }

    fn make_runtime(module_name: &str) -> TestRuntime<Error, TestSettings> {
        let logs = vec![
            &[0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0d, b'R', b'o'][..],
            &b"ses are"[..],
            &[b' ', b'r', b'e', b'd', 0x02, 0x00][..],
            &[0x00, 0x00, 0x00, 0x00, 0x00, 0x10][..],
            &b"violets"[..],
            &b" are blue"[..],
        ];

        let state: Result<ModuleRuntimeState, Error> = Ok(ModuleRuntimeState::default());
        let config = TestConfig::new(format!("microsoft/{}", module_name));
        let module = TestModule::new_with_logs(module_name.to_owned(), config, state, logs);

        TestRuntime::make_runtime(TestSettings::new())
            .wait()
            .unwrap()
            .with_module(Ok(module))
    }

    // From https://github.com/mvdnes/zip-rs/blob/master/examples/extract.rs
    fn extract_zip(source: &str, destination: &str) {
        let fname = std::path::Path::new(source);
        let file = File::open(&fname).unwrap();
        let mut archive = zip::ZipArchive::new(file).unwrap();

        for i in 0..archive.len() {
            let mut file = archive.by_index(i).unwrap();

            let filename = {
                let filename = std::path::Path::new(file.name());
                // Assert that the path has no components other than Normal
                let mut components = filename.components();
                assert!(components
                    .all(|component| matches!(component, std::path::Component::Normal(_))));
                filename
            };

            let outpath = PathBuf::from(destination).join(filename);

            if let Some(parent) = outpath.parent() {
                fs::create_dir_all(&parent).unwrap();
            }

            if file.is_dir() {
                fs::create_dir_all(&outpath).unwrap();
            } else if file.is_file() {
                let mut outfile = fs::File::create(&outpath).unwrap();
                io::copy(&mut file, &mut outfile).unwrap();
            }

            // Get and Set permissions
            #[cfg(unix)]
            {
                use std::os::unix::fs::PermissionsExt;

                if let Some(mode) = file.unix_mode() {
                    fs::set_permissions(&outpath, fs::Permissions::from_mode(mode)).unwrap();
                }
            }
        }
    }
}
