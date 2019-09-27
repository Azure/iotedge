// Copyright (c) Microsoft. All rights reserved.

use std::fs::File;
use std::path::Path;
use chrono::{NaiveDateTime, DateTime, Local, Utc};
use zip;

use futures::{Future, Stream};
use tokio::prelude::*;

use crate::error::{Error, ErrorKind};
use failure::Fail;

use crate::logs::pull_logs;
use crate::Command;

use edgelet_core::{LogOptions, Module, ModuleRuntime, LogTail::Num};

pub struct Bundle<M> {
    runtime: M,
    log_options: Option<LogOptions>,
    location: Option<String>,
}

struct BundleState<M> {
    runtime: M,
    log_options: LogOptions,
    location: String,
    file_options: zip::write::FileOptions,
    zip_writer: zip::ZipWriter<File>,
}

impl<M> Bundle<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    pub fn new(log_options: LogOptions, location: &str, runtime: M) -> Self {
        Bundle {
            runtime,
            log_options: Option::Some(log_options),
            location: Option::Some(location.to_owned()),
        }
    }

    fn make_state(self) -> Result<BundleState<M>, Error> {
        if let (Some(log_options), Some(location)) = (self.log_options, self.location) {
            /* Print status */
            let since_time: DateTime<Utc> = DateTime::from_utc(NaiveDateTime::from_timestamp(log_options.since().into(), 0), Utc);
            let since_local: DateTime<Local> = DateTime::from(since_time);
            let max_lines = if let Num(tail) = log_options.tail() {
                format!("(maximum {} lines) ", tail)
            } else {"".to_owned()};
            println!("Writing all logs {}since {} (local time {}) to {}", max_lines, since_time, since_local, location);

            /* Make state */
            let file_options = zip::write::FileOptions::default()
                .compression_method(zip::CompressionMethod::Deflated);

            let zip_writer = zip::ZipWriter::new(
                File::create(format!("{}/css_bundle.zip", location.to_owned()))
                    .map_err(|err| Error::from(err.context(ErrorKind::WriteToStdout)))?,
            );

            Ok(BundleState {
                runtime: self.runtime.clone(),
                log_options,
                location,
                file_options,
                zip_writer,
            })
        } else {
            Err(Error::from(ErrorKind::BadHostParameter))
        }
    }
}

impl<M> Command for Bundle<M>
where
    M: 'static + ModuleRuntime + Clone + Send + Sync,
{
    type Future = Box<dyn Future<Item = (), Error = Error> + Send>;

    fn execute(self) -> Self::Future {
        let result = future::result(self.make_state())
            .and_then(Bundle::write_all_logs)
            .map(drop)
            .map(|_| println!("Wrote all logs to file"));

        Box::new(result)
    }
}

impl<M> Bundle<M>
where
    M: 'static + ModuleRuntime + Clone,
{
    fn write_all_logs(s1: BundleState<M>) -> impl Future<Item = BundleState<M>, Error = Error> {
        Bundle::get_modules(s1).and_then(|(names, s2)| {
                        stream::iter_ok(names).fold(s2, |s3, name| Bundle::write_log_to_file(s3, name))
        })
    }

    fn get_modules(state: BundleState<M>) -> impl Future<Item = (Vec<String>, BundleState<M>), Error = Error> {
        state
            .runtime
            .list_with_details()
            .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))
            .map(|(module, _s)| module.name().to_string())
            .collect()
            .map(|names| (names, state))
    }

    fn write_log_to_file(state: BundleState<M>, module_name: String) -> impl Future<Item = BundleState<M>, Error = Error> {
        println!("Writing {} to file", module_name);
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
            .map_err(|err| Error::from(err.context(ErrorKind::WriteToStdout)))
            .and_then(move |_| {
                pull_logs(&runtime, &module_name, &log_options, zip_writer).map(move |zw| {
                    println!("Wrote {} to file", module_name);
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
}

#[cfg(test)]
mod tests {
    use edgelet_core::{MakeModuleRuntime, ModuleRuntimeState};
    use edgelet_test_utils::module::*;
    use edgelet_test_utils::crypto::TestHsm;
    use std::str;
    use tempfile::tempdir;

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
            .with_tail(Num(0))
            .with_since(0);

        let result: Vec<u8> = pull_logs(&runtime, module_name, &options, Vec::new()).wait().unwrap();
        let result_str = str::from_utf8(&result).unwrap();
        assert_eq!("Roses are redviolets are blue", result_str);
    }

    #[test]
    fn write_logs_to_file() {
        let runtime = make_runtime();

        let options = LogOptions::new()
            .with_follow(false)
            .with_tail(Num(0))
            .with_since(0);

        let tmp_dir = tempdir().unwrap();

        let bundle = Bundle::new(options, tmp_dir.path().to_str().unwrap(), runtime);
        bundle.execute().wait().unwrap();

        let expected = tmp_dir.path().join("css_bundle.zip");
        println!("{}", expected.to_str().unwrap());
        File::open(expected).unwrap();

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
