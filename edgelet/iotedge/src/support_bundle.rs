// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::ffi::OsString;
use std::fs::File;
use std::io::{stdout, Cursor, Seek};
use std::path::{Path, PathBuf};
use std::process::Command as ShellCommand;

use chrono::{DateTime, Local, NaiveDateTime, Utc};
use failure::Fail;
use futures::{Future, Stream};
use tokio::prelude::*;
use zip::{write::FileOptions, CompressionMethod, ZipWriter};

use edgelet_core::{LogOptions, LogTail, Module, ModuleRuntime};

use crate::error::{Error, ErrorKind};
use crate::logs::pull_logs;
use crate::Command;

pub struct SupportBundle<M> {
    runtime: M,
    log_options: LogOptions,
    include_ms_only: bool,
    verbose: bool,
    iothub_hostname: Option<String>,
    output_location: OutputLocation,
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

impl<M> Command for SupportBundle<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        println!("Making support bundle");

        match self.output_location.clone() {
            OutputLocation::File(location) => Box::new(
                Self::bundle_all(future::result(self.make_file_state())).map(|_state| {
                    let path = PathBuf::from(location);
                    println!(
                        "Created support bundle at {}",
                        path.canonicalize().unwrap_or_else(|_| path).display()
                    );
                }),
            ),
            OutputLocation::Console => Box::new(
                Self::bundle_all(future::result(self.make_vector_state())).and_then(|mut state| {
                    stdout()
                        .write_all(
                            state
                                .zip_writer
                                .finish()
                                .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?
                                .get_ref(),
                        )
                        .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))
                }),
            ),
        }
    }
}

impl<M> SupportBundle<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    pub fn new(
        log_options: LogOptions,
        include_ms_only: bool,
        verbose: bool,
        iothub_hostname: Option<String>,
        output_location: OutputLocation,
        runtime: M,
    ) -> Self {
        SupportBundle {
            runtime,
            log_options,
            include_ms_only,
            verbose,
            iothub_hostname,
            output_location,
        }
    }

    fn make_file_state(self) -> Result<BundleState<M, File>, Error> {
        let writer = File::create(Path::new(self.output_location.get_file_location()))
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        self.make_state(writer)
    }

    fn make_vector_state(self) -> Result<BundleState<M, Cursor<Vec<u8>>>, Error> {
        self.make_state(Cursor::new(Vec::new()))
    }

    fn make_state<W>(self, writer: W) -> Result<BundleState<M, W>, Error>
    where
        W: Write + Seek + Send,
    {
        let file_options = FileOptions::default().compression_method(CompressionMethod::Deflated);
        let zip_writer = ZipWriter::new(writer);

        Ok(BundleState {
            runtime: self.runtime,
            log_options: self.log_options,
            include_ms_only: self.include_ms_only,
            verbose: self.verbose,
            iothub_hostname: self.iothub_hostname,
            file_options,
            zip_writer,
        })
    }

    fn bundle_all<S, W>(state: S) -> impl Future<Item = BundleState<M, W>, Error = Error>
    where
        S: Future<Item = BundleState<M, W>, Error = Error>,
        W: Write + Seek + Send,
    {
        state
            .and_then(Self::write_check)
            .and_then(Self::write_module_logs)
            .and_then(Self::write_edgelet_log)
            .and_then(Self::write_docker_log)
            .and_then(Self::write_all_inspects)
            .and_then(Self::write_all_network_inspects)
    }

    fn write_module_logs<W>(
        state: BundleState<M, W>,
    ) -> impl Future<Item = BundleState<M, W>, Error = Error>
    where
        W: Write + Seek + Send,
    {
        /* Print status */
        if state.verbose {
            let since_time: DateTime<Utc> = DateTime::from_utc(
                NaiveDateTime::from_timestamp(state.log_options.since().into(), 0),
                Utc,
            );
            let since_local: DateTime<Local> = DateTime::from(since_time);
            let max_lines = if let LogTail::Num(tail) = state.log_options.tail() {
                format!("(maximum {} lines) ", tail)
            } else {
                "".to_owned()
            };
            println!(
                "Writing all logs {}since {} (local time {})",
                max_lines, since_time, since_local
            );
        }

        SupportBundle::get_modules(state).and_then(|(names, s2)| {
            stream::iter_ok(names).fold(s2, SupportBundle::write_log_to_file)
        })
    }

    fn write_all_inspects<W>(
        s1: BundleState<M, W>,
    ) -> impl Future<Item = BundleState<M, W>, Error = Error>
    where
        W: Write + Seek + Send,
    {
        SupportBundle::get_modules(s1).and_then(|(names, s2)| {
            stream::iter_ok(names).fold(s2, |s3, name| {
                SupportBundle::write_inspect_to_file(s3, &name)
            })
        })
    }

    fn write_all_network_inspects<W>(s1: BundleState<M, W>) -> Result<BundleState<M, W>, Error>
    where
        W: Write + Seek + Send,
    {
        SupportBundle::get_docker_networks(s1).and_then(|(names, s2)| {
            names.into_iter().fold(Ok(s2), |s3, name| {
                if let Ok(s3) = s3 {
                    SupportBundle::write_docker_network_to_file(s3, &name)
                } else {
                    s3
                }
            })
        })
    }

    fn get_modules<W>(
        state: BundleState<M, W>,
    ) -> impl Future<Item = (Vec<String>, BundleState<M, W>), Error = Error>
    where
        W: Write + Seek + Send,
    {
        const MS_MODULES: &[&str] = &["edgeAgent", "edgeHub"];

        let include_ms_only = state.include_ms_only;

        state
            .runtime
            .list_with_details()
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .map(|(module, _s)| module.name().to_owned())
            .filter(move |name| !include_ms_only || MS_MODULES.iter().any(|ms| ms == name))
            .collect()
            .map(|names| (names, state))
    }

    fn write_log_to_file<W>(
        state: BundleState<M, W>,
        module_name: String,
    ) -> impl Future<Item = BundleState<M, W>, Error = Error>
    where
        W: Write + Seek + Send,
    {
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

        let file_name = format!("{}_log.txt", module_name);
        zip_writer
            .start_file_from_path(&Path::new("logs").join(file_name), file_options)
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

    fn write_edgelet_log<W>(mut state: BundleState<M, W>) -> Result<BundleState<M, W>, Error>
    where
        W: Write + Seek + Send,
    {
        state.print_verbose("Getting system logs for iotedged");
        let since_time: DateTime<Utc> = DateTime::from_utc(
            NaiveDateTime::from_timestamp(state.log_options.since().into(), 0),
            Utc,
        );

        #[cfg(unix)]
        let inspect = ShellCommand::new("journalctl")
            .arg("-a")
            .args(&["-u", "iotedge"])
            .args(&["-S", &since_time.format("%F %T").to_string()])
            .arg("--no-pager")
            .output();

        #[cfg(windows)]
         let inspect = ShellCommand::new("powershell.exe")
            .arg("-NoProfile")
            .arg("-Command")
            .arg(&format!(r"$start=[Xml.XmlConvert]::ToDateTime('{}');
                            Get-WinEvent -ea SilentlyContinue -FilterHashtable @{{ProviderName='iotedged';LogName='application';StartTime=$start}} |
                            Select TimeCreated, Message |
                            Sort-Object @{{Expression='TimeCreated';Descending=$false}} |
                            Format-List", since_time.to_rfc3339()))
            .output();

        let (file_name, output) = if let Ok(result) = inspect {
            if result.status.success() {
                ("iotedged.txt", result.stdout)
            } else {
                ("iotedged_err.txt", result.stderr)
            }
        } else {
            let err_message = inspect.err().unwrap().to_string();
            println!("Could not find system logs for iotedge. Including error in bundle.\nError message: {}", err_message);
            ("iotedged_err.txt", err_message.as_bytes().to_vec())
        };

        state
            .zip_writer
            .start_file_from_path(&Path::new("logs").join(file_name), state.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state
            .zip_writer
            .write_all(&output)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state.print_verbose("Got logs for iotedged");
        Ok(state)
    }

    fn write_docker_log<W>(mut state: BundleState<M, W>) -> Result<BundleState<M, W>, Error>
    where
        W: Write + Seek + Send,
    {
        state.print_verbose("Getting system logs for docker");
        let since_time: DateTime<Utc> = DateTime::from_utc(
            NaiveDateTime::from_timestamp(state.log_options.since().into(), 0),
            Utc,
        );
        let since = since_time.format("%F %T").to_string();

        #[cfg(unix)]
        let inspect = ShellCommand::new("journalctl")
            .arg("-a")
            .args(&["-u", "docker"])
            .args(&["-S", &since])
            .arg("--no-pager")
            .output();

        /* from https://docs.microsoft.com/en-us/virtualization/windowscontainers/troubleshooting#finding-logs */
        #[cfg(windows)]
        let inspect = ShellCommand::new("powershell.exe")
            .arg("-NoProfile")
            .arg("-Command")
            .arg(&format!(
                r#"Get-EventLog -LogName Application -Source Docker -After "{}" |
                    Sort-Object Time |
                    Format-List"#,
                since
            ))
            .output();

        let (file_name, output) = if let Ok(result) = inspect {
            if result.status.success() {
                ("docker.txt", result.stdout)
            } else {
                ("docker_err.txt", result.stderr)
            }
        } else {
            let err_message = inspect.err().unwrap().to_string();
            println!("Could not find system logs for docker. Including error in bundle.\nError message: {}", err_message);
            ("docker_err.txt", err_message.as_bytes().to_vec())
        };

        state
            .zip_writer
            .start_file_from_path(&Path::new("logs").join(file_name), state.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state
            .zip_writer
            .write_all(&output)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state.print_verbose("Got logs for docker");
        Ok(state)
    }

    fn write_check<W>(mut state: BundleState<M, W>) -> Result<BundleState<M, W>, Error>
    where
        W: Write + Seek + Send,
    {
        let iotedge = env::args().next().unwrap();
        state.print_verbose("Calling iotedge check");

        let mut check = ShellCommand::new(iotedge);
        check.arg("check").args(&["-o", "json"]);

        if let Some(host_name) = state.iothub_hostname.clone() {
            check.args(&["--iothub-hostname", &host_name]);
        }
        let check = check
            .output()
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state
            .zip_writer
            .start_file_from_path(&Path::new("check.json"), state.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state
            .zip_writer
            .write_all(&check.stdout)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state.print_verbose("Wrote check output to file");
        Ok(state)
    }

    fn write_inspect_to_file<W>(
        mut state: BundleState<M, W>,
        module_name: &str,
    ) -> Result<BundleState<M, W>, Error>
    where
        W: Write + Seek + Send,
    {
        state.print_verbose(&format!("Running docker inspect for {}", module_name));
        let mut inspect = ShellCommand::new("docker");

        /***
         * Note: this assumes using windows containers on a windows machine.
         * This is the expected production scenario.
         * Since the bundle command does not read the config.yaml, it cannot use the `moby.runtime_uri` from there.
         * This will not fail the bundle, only note the failure to the user and in the bundle.
         */
        #[cfg(windows)]
        inspect.args(&["-H", "npipe:////./pipe/iotedge_moby_engine"]);

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

        state
            .zip_writer
            .start_file_from_path(&Path::new(&file_name), state.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state
            .zip_writer
            .write_all(&output)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state.print_verbose(&format!("Got docker inspect for {}", module_name));
        Ok(state)
    }

    fn get_docker_networks<W>(
        state: BundleState<M, W>,
    ) -> Result<(Vec<String>, BundleState<M, W>), Error>
    where
        W: Write + Seek + Send,
    {
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

        Ok((result.lines().map(String::from).collect(), state))
    }

    fn write_docker_network_to_file<W>(
        mut state: BundleState<M, W>,
        network_name: &str,
    ) -> Result<BundleState<M, W>, Error>
    where
        W: Write + Seek + Send,
    {
        state.print_verbose(&format!(
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

        state
            .zip_writer
            .start_file_from_path(&Path::new(&file_name), state.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state
            .zip_writer
            .write_all(&output)
            .map_err(|err| Error::from(err.context(ErrorKind::SupportBundle)))?;

        state.print_verbose(&format!("Got docker network inspect for {}", network_name));
        Ok(state)
    }
}

impl<M, W> BundleState<M, W>
where
    W: Write + Seek + Send,
{
    fn print_verbose(&self, message: &str) {
        if self.verbose {
            println!("{}", message);
        }
    }
}

#[derive(Clone, Debug, PartialEq)]
pub enum OutputLocation {
    File(OsString),
    Console,
}

impl OutputLocation {
    fn get_file_location(&self) -> &OsString {
        if let Self::File(location) = self {
            location
        } else {
            panic!("Cannot get file location for console mode");
        }
    }
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
    use edgelet_test_utils::crypto::TestHsm;
    use edgelet_test_utils::module::*;

    use super::*;

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

        let bundle = SupportBundle::new(
            LogOptions::default(),
            false,
            false,
            None,
            OutputLocation::File(OsString::from(file_path.to_owned())),
            runtime,
        );

        bundle.execute().wait().unwrap();

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

        let iotedged_log = Regex::new(r"iotedged.*\.txt").unwrap();
        assert!(fs::read_dir(PathBuf::from(&extract_path).join("logs"))
            .unwrap()
            .map(|file| file
                .unwrap()
                .path()
                .file_name()
                .unwrap()
                .to_str()
                .unwrap()
                .to_owned())
            .any(|f| iotedged_log.is_match(&f)));

        let docker_log = Regex::new(r"docker.*\.txt").unwrap();
        assert!(fs::read_dir(PathBuf::from(&extract_path).join("logs"))
            .unwrap()
            .map(|file| file
                .unwrap()
                .path()
                .file_name()
                .unwrap()
                .to_str()
                .unwrap()
                .to_owned())
            .any(|f| docker_log.is_match(&f)));

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

    #[test]
    fn get_modules() {
        let runtime = make_runtime("test-module");
        let tmp_dir = tempdir().unwrap();
        let file_path = tmp_dir
            .path()
            .join("iotedge_bundle.zip")
            .to_str()
            .unwrap()
            .to_owned();
        let bundle = SupportBundle::new(
            LogOptions::default(),
            false,
            true,
            None,
            OutputLocation::File(OsString::from(file_path.to_owned())),
            runtime,
        );

        let state = bundle.make_file_state().unwrap();

        let (modules, mut state) = SupportBundle::get_modules(state).wait().unwrap();
        assert_eq!(modules.len(), 1);

        state.include_ms_only = true;

        let (modules, _state) = SupportBundle::get_modules(state).wait().unwrap();
        assert_eq!(modules.len(), 0);

        /* with edge agent */
        let runtime = make_runtime("edgeAgent");
        let bundle = SupportBundle::new(
            LogOptions::default(),
            false,
            true,
            None,
            OutputLocation::File(OsString::from(file_path)),
            runtime,
        );

        let state = bundle.make_file_state().unwrap();

        let (modules, mut state) = SupportBundle::get_modules(state).wait().unwrap();
        assert_eq!(modules.len(), 1);

        state.include_ms_only = true;

        let (modules, _state) = SupportBundle::get_modules(state).wait().unwrap();
        assert_eq!(modules.len(), 1);
    }

    #[test]
    fn write_logs_to_file() {
        let runtime = make_runtime("test-module");
        let tmp_dir = tempdir().unwrap();
        let file_path = tmp_dir
            .path()
            .join("iotedge_bundle.zip")
            .to_str()
            .unwrap()
            .to_owned();

        let bundle = SupportBundle::new(
            LogOptions::default(),
            false,
            true,
            None,
            OutputLocation::File(OsString::from(file_path.to_owned())),
            runtime,
        );

        bundle.execute().wait().unwrap();

        File::open(file_path).unwrap();
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

        TestRuntime::make_runtime(
            TestSettings::new(),
            TestProvisioningResult::new(),
            TestHsm::default(),
        )
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
            let outpath = PathBuf::from(destination).join(file.sanitized_name());

            if (&*file.name()).ends_with('/') {
                fs::create_dir_all(&outpath).unwrap();
            } else {
                if let Some(p) = outpath.parent() {
                    if !p.exists() {
                        fs::create_dir_all(&p).unwrap();
                    }
                }
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
