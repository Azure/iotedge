// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::error::Error as StdError;
use std::fs::File;
use std::path::Path;
use std::process::Command as ShellCommand;

use chrono::{DateTime, Local, NaiveDateTime, Utc};
use failure::Fail;
use futures::{Future, Stream};
use tokio::prelude::*;
use zip;

use crate::error::{Error, ErrorKind};
use edgelet_core::{LogOptions, LogTail, Module, ModuleRuntime};

use crate::logs::pull_logs;
use crate::Command;

pub struct SupportBundle<M> {
    runtime: M,
    log_options: LogOptions,
    location: String,
}

struct BundleState<M> {
    runtime: M,
    log_options: LogOptions,
    location: String,
    file_options: zip::write::FileOptions,
    zip_writer: zip::ZipWriter<File>,
}

impl<M> Command for SupportBundle<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        let result = future::result(self.make_state())
            .and_then(SupportBundle::write_all_logs)
            .and_then(SupportBundle::write_check_to_file)
            .and_then(SupportBundle::write_all_inspects)
            .map(drop)
            .map(|_| println!("Created support bundle"));

        Box::new(result)
    }
}

impl<M> SupportBundle<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    pub fn new(log_options: LogOptions, location: String, runtime: M) -> Self {
        SupportBundle {
            runtime,
            log_options,
            location,
        }
    }

    fn make_state(self) -> Result<BundleState<M>, Error> {
        let file_options =
            zip::write::FileOptions::default().compression_method(zip::CompressionMethod::Deflated);

        let zip_writer = zip::ZipWriter::new(
            File::create(format!("{}/iotedge_bundle.zip", self.location.to_owned()))
                .map_err(|err| Error::from(err.context(ErrorKind::WriteToFile)))?,
        );

        Ok(BundleState {
            runtime: self.runtime,
            log_options: self.log_options,
            location: self.location,
            file_options,
            zip_writer,
        })
    }

    fn write_all_logs(s1: BundleState<M>) -> impl Future<Item = BundleState<M>, Error = Error> {
        /* Print status */
        let since_time: DateTime<Utc> = DateTime::from_utc(
            NaiveDateTime::from_timestamp(s1.log_options.since().into(), 0),
            Utc,
        );
        let since_local: DateTime<Local> = DateTime::from(since_time);
        let max_lines = if let LogTail::Num(tail) = s1.log_options.tail() {
            format!("(maximum {} lines) ", tail)
        } else {
            "".to_owned()
        };
        println!(
            "Writing all logs {}since {} (local time {}) to {}",
            max_lines, since_time, since_local, s1.location
        );

        SupportBundle::get_modules(s1).and_then(|(names, s2)| {
            stream::iter_ok(names).fold(s2, SupportBundle::write_log_to_file)
        })
    }

    fn write_all_inspects(s1: BundleState<M>) -> impl Future<Item = BundleState<M>, Error = Error> {
        SupportBundle::get_modules(s1).and_then(|(names, s2)| {
            stream::iter_ok(names).fold(s2, SupportBundle::write_inspect_to_file)
        })
    }

    fn get_modules(
        state: BundleState<M>,
    ) -> impl Future<Item = (Vec<String>, BundleState<M>), Error = Error> {
        state
            .runtime
            .list_with_details()
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .map(|(module, _s)| module.name().to_string())
            .collect()
            .map(|names| (names, state))
    }

    fn write_log_to_file(
        state: BundleState<M>,
        module_name: String,
    ) -> impl Future<Item = BundleState<M>, Error = Error> {
        println!("Writing {} logs to file", module_name);
        let BundleState {
            runtime,
            log_options,
            location,
            file_options,
            mut zip_writer,
        } = state;

        let file_name = format!("logs/{}_log.txt", module_name);
        zip_writer
            .start_file_from_path(&Path::new(&file_name), file_options)
            .into_future()
            .map_err(|err| Error::from(err.context(ErrorKind::WriteToFile)))
            .and_then(move |_| {
                pull_logs(&runtime, &module_name, &log_options, zip_writer).map(move |zw| {
                    println!("Wrote {} logs to file", module_name);
                    BundleState {
                        runtime,
                        log_options,
                        location,
                        file_options,
                        zip_writer: zw,
                    }
                })
            })
    }

    fn write_check_to_file(mut state: BundleState<M>) -> Result<BundleState<M>, Error> {
        let iotedge = env::args().into_iter().nth(0).unwrap();
        println!("Calling iotedge check");
        let check = ShellCommand::new(iotedge)
            .arg("check")
            .args(&["-o", "json"])
            .output()
            .map_err(|err| Error::from(err.context(ErrorKind::BundleCheck)))?;

        state
            .zip_writer
            .start_file_from_path(&Path::new("check.json"), state.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::WriteToFile)))?;

        state
            .zip_writer
            .write(&check.stdout)
            .map_err(|err| Error::from(err.context(ErrorKind::WriteToFile)))?;

        println!("Wrote check output to file");
        Ok(state)
    }

    fn write_inspect_to_file(
        mut state: BundleState<M>,
        module_name: String,
    ) -> Result<BundleState<M>, Error> {
        println!("Running docker inspect for {}", module_name);
        let inspect = ShellCommand::new("docker")
            .arg("inspect")
            .arg(&module_name)
            .output();

        let (file_name, output) = if let Ok(result) = inspect {
            if result.status.success() {
                (format!("inspect/{}.json", module_name), result.stdout)
            } else {
                (format!("inspect/{}_err.json", module_name), result.stderr)
            }
        } else {
            let err_message = inspect.err().unwrap().description().to_owned();
            println!("Could not reach docker. Error message: {}", err_message);
            (format!("inspect/{}_err_docker.txt", module_name), err_message.as_bytes().to_vec())
        };

        state
            .zip_writer
            .start_file_from_path(&Path::new(&file_name), state.file_options)
            .map_err(|err| Error::from(err.context(ErrorKind::WriteToFile)))?;

        state
            .zip_writer
            .write(&output)
            .map_err(|err| Error::from(err.context(ErrorKind::WriteToFile)))?;

        Ok(state)
    }
}

#[cfg(test)]
mod tests {
    use std::str;

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
    fn get_logs() {
        let module_name = "test-module";
        let runtime = make_runtime();

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
    fn write_logs_to_file() {
        let runtime = make_runtime();

        let options = LogOptions::new()
            .with_follow(false)
            .with_tail(LogTail::Num(0))
            .with_since(0);

        let tmp_dir = tempdir().unwrap();

        let bundle = SupportBundle::new(
            options,
            tmp_dir.path().to_str().unwrap().to_owned(),
            runtime,
        );
        bundle.execute().wait().unwrap();

        let result_path = tmp_dir
            .path()
            .join("iotedge_bundle.zip")
            .to_str()
            .unwrap()
            .to_owned();
        File::open(result_path).unwrap();
    }

    fn make_runtime() -> TestRuntime<Error, TestSettings> {
        let module_name = "test-module";
        let logs = vec![
            &[0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0d, b'R', b'o'][..],
            &b"ses are"[..],
            &[b' ', b'r', b'e', b'd', 0x02, 0x00][..],
            &[0x00, 0x00, 0x00, 0x00, 0x00, 0x10][..],
            &b"violets"[..],
            &b" are blue"[..],
        ];

        let image_name = "microsoft/test-image";
        let state: Result<ModuleRuntimeState, Error> = Ok(ModuleRuntimeState::default());
        let config = TestConfig::new(image_name.to_owned());
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
}
