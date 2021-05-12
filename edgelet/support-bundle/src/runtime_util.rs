// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::io::Write;

// TODO: make tokio
use std::process::Command as ShellCommand;

use failure::Fail;
use futures::StreamExt;

use edgelet_core::{LogOptions, Module, ModuleRuntime};

use crate::error::{Error, ErrorKind};

pub async fn get_modules(
    runtime: &impl ModuleRuntime,
    include_ms_only: bool,
) -> Result<Vec<String>, Error> {
    const MS_MODULES: &[&str] = &["edgeAgent", "edgeHub"];

    let runtime_modules = runtime
        .list_with_details()
        .await
        .into_iter()
        .map(|(module, _s)| module.name().to_owned())
        .filter(move |name| !include_ms_only || MS_MODULES.iter().any(|ms| ms == name))
        .collect();

    Ok(runtime_modules)
}

pub async fn write_check(
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

pub async fn write_logs(
    runtime: &impl ModuleRuntime,
    module_id: &str,
    options: &LogOptions,
    writer: &mut (impl Write + Send),
) -> Result<(), Error> {
    // Collect Logs
    let logs = runtime
        .logs(module_id, options)
        .await
        .map_err(|err| Error::from(err.context(ErrorKind::ModuleRuntime)))?;

    // Write all logs
    let write = logs.map(|part| writer.write_all(part.as_ref()));

    // Extract errors
    write
        .collect::<Vec<_>>()
        .await
        .into_iter()
        .collect::<Result<(), std::io::Error>>()
        .map_err(|err| Error::from(err.context(ErrorKind::Write)))
}

fn print_verbose<S>(message: S)
where
    S: std::fmt::Display,
{
    if true {
        println!("{}", message);
    }
}
