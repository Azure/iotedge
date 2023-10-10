// Copyright (c) Microsoft. All rights reserved.

use chrono::{DateTime, NaiveDateTime, Utc};
use std::io::{Seek, Write};
use tokio::process::Command;

use anyhow::Context;
use zip::{write::FileOptions, ZipWriter};

use crate::error::Error;
use edgelet_core::LogOptions;

pub async fn write_check(
    writer: &mut impl Write,
    iothub_hostname: Option<String>,
    verbose: bool,
) -> anyhow::Result<()> {
    print_verbose("Calling iotedge check", verbose);

    let mut iotedge = std::env::current_exe().unwrap();
    if iotedge.to_string_lossy().contains("aziot-edged") {
        print_verbose(
            "Calling iotedge check from edgelet, using iotedge from path",
            verbose,
        );
        iotedge = "iotedge".into();
    }

    let mut check = Command::new(iotedge);
    check.arg("check").args(["-o", "json"]);

    if let Some(host_name) = iothub_hostname {
        check.args(["--iothub-hostname", &host_name]);
    }
    let check = check.output().await.context(Error::SupportBundle)?;

    writer
        .write_all(&check.stdout)
        .context(Error::SupportBundle)?;
    writer
        .write_all(&check.stderr)
        .context(Error::SupportBundle)?;

    print_verbose("Wrote check output to file", verbose);
    Ok(())
}

pub async fn write_inspect<W>(
    module_name: &str,
    zip_writer: &mut ZipWriter<W>,
    file_options: &FileOptions,
    verbose: bool,
) -> anyhow::Result<()>
where
    W: Write + Seek,
{
    print_verbose(
        &format!("Running docker inspect for {}", module_name),
        verbose,
    );

    let mut inspect = Command::new("docker");
    inspect.arg("inspect").arg(module_name);
    let inspect = inspect.output().await;

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

    zip_writer
        .start_file(file_name, *file_options)
        .context(Error::SupportBundle)?;

    zip_writer
        .write_all(&output)
        .context(Error::SupportBundle)?;

    print_verbose(&format!("Got docker inspect for {}", module_name), verbose);

    Ok(())
}

pub async fn get_docker_networks() -> Result<Vec<String>, Error> {
    let mut inspect = Command::new("docker");
    inspect.args(["network", "ls"]);
    inspect.args(["--format", "{{.Name}}"]);
    let inspect = inspect.output().await;

    let result = if let Ok(result) = inspect {
        if result.status.success() {
            String::from_utf8_lossy(&result.stdout).to_string()
        } else {
            println!(
                "Could not find network names: {}",
                String::from_utf8_lossy(&result.stderr)
            );
            edgelet_settings::DEFAULT_NETWORKID.to_owned()
        }
    } else {
        println!("Could not find network names: {}", inspect.err().unwrap());
        edgelet_settings::DEFAULT_NETWORKID.to_owned()
    };

    let result = result.lines().map(String::from).collect();
    Ok(result)
}

pub async fn write_network_inspect<W>(
    network_name: &str,
    zip_writer: &mut ZipWriter<W>,
    file_options: &FileOptions,
    verbose: bool,
) -> anyhow::Result<()>
where
    W: Write + Seek,
{
    print_verbose(
        &format!("Running docker network inspect for {}", network_name),
        verbose,
    );
    let mut inspect = Command::new("docker");

    inspect.args(["network", "inspect", network_name, "-v"]);
    let inspect = inspect.output().await;

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

    zip_writer
        .start_file(file_name, *file_options)
        .context(Error::SupportBundle)?;

    zip_writer
        .write_all(&output)
        .context(Error::SupportBundle)?;

    print_verbose(
        &format!("Got docker network inspect for {}", network_name),
        verbose,
    );
    Ok(())
}

pub async fn write_system_log<W>(
    name: &str,
    unit: &str,
    log_options: &LogOptions,
    zip_writer: &mut ZipWriter<W>,
    file_options: &FileOptions,
    verbose: bool,
) -> anyhow::Result<()>
where
    W: Write + Seek,
{
    print_verbose(
        format!("Getting system logs for {}", name).as_str(),
        verbose,
    );
    let timestamp = NaiveDateTime::from_timestamp_opt(log_options.since().into(), 0)
        .ok_or(Error::SupportBundle)?;
    let since_time: DateTime<Utc> = DateTime::from_utc(timestamp, Utc);
    let until_time: Option<DateTime<Utc>> = log_options
        .until()
        .and_then(|until| NaiveDateTime::from_timestamp_opt(until.into(), 0))
        .map(|until| DateTime::from_utc(until, Utc));

    let command = {
        let mut command = Command::new("journalctl");
        command
            .arg("-a")
            .args(["-u", unit])
            .args(["-S", &since_time.format("%F %T").to_string()])
            .arg("--no-pager");
        if let Some(until) = until_time {
            command.args(["-U", &until.format("%F %T").to_string()]);
        }

        command.output().await
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

    zip_writer
        .start_file(file_name, *file_options)
        .context(Error::SupportBundle)?;

    zip_writer
        .write_all(&output)
        .context(Error::SupportBundle)?;

    print_verbose(format!("Got logs for {}", name).as_str(), verbose);
    Ok(())
}

fn print_verbose<S>(message: S, verbose: bool)
where
    S: std::fmt::Display,
{
    if verbose {
        println!("{}", message);
    }
}
