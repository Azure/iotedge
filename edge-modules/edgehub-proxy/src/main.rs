// Copyright (c) Microsoft. All rights reserved.

use std::ffi::OsString;
use std::fs;
use std::path::PathBuf;
use std::process;

use clap::{crate_description, crate_name, crate_version, Arg, Command};

use log::info;

use chrono::DateTime;
use chrono::Utc;
use edgehub_proxy::error::Error;
use edgehub_proxy::logging;
use edgelet_client::workload;

#[tokio::main]
async fn main() {
    logging::init();
    if let Err(e) = run().await {
        logging::log_error(&e);
        process::exit(1);
    }
}

async fn run() -> Result<(), Error> {
    let matches = Command::new(crate_name!())
        .version(crate_version!())
        .about(crate_description!())
        .subcommand_required(true)
        .arg_required_else_help(true)
        .arg(
            Arg::new("host")
                .help("Workload socket to connect to")
                .short('H')
                .long("host")
                .num_args(1)
                .value_name("HOST")
                .required(true)
                .env("IOTEDGE_WORKLOADURI"),
        )
        .arg(
            Arg::new("moduleid")
                .help("Module id")
                .short('m')
                .long("moduleid")
                .num_args(1)
                .value_name("MODULEID")
                .required(true)
                .env("IOTEDGE_MODULEID"),
        )
        .arg(
            Arg::new("genid")
                .help("Generation id")
                .short('g')
                .long("genid")
                .num_args(1)
                .value_name("GENID")
                .required(true)
                .env("IOTEDGE_MODULEGENERATIONID"),
        )
        .arg(
            Arg::new("apiversion")
                .help("iotedged API Version")
                .short('a')
                .long("apiversion")
                .num_args(1)
                .value_name("API_VERSION")
                .required(true)
                .env("IOTEDGE_APIVERSION")
                .default_value("2018-06-28"),
        )
        .subcommand(
            Command::new("cert-server")
                .about("Retrieve a server cert")
                .arg(
                    Arg::new("common-name")
                        .help("Sets the common name of the certificate")
                        .required(true)
                        .long("common-name")
                        .num_args(1)
                        .value_name("COMMON_NAME")
                        .required(true),
                )
                .arg(
                    Arg::new("expiration")
                        .help("Sets the expiration time of the certificate")
                        .required(true)
                        .long("expiration")
                        .num_args(1)
                        .value_name("EXPIRATION")
                        .required(true),
                )
                .arg(
                    Arg::new("crt-file")
                        .help("Sets the crt output file")
                        .required(false)
                        .long("crt")
                        .num_args(1)
                        .value_parser(clap::value_parser!(PathBuf))
                        .value_name("CRT_FILE"),
                )
                .arg(
                    Arg::new("key-file")
                        .help("Sets the key output file")
                        .required(false)
                        .long("key")
                        .num_args(1)
                        .value_parser(clap::value_parser!(PathBuf))
                        .value_name("KEY_FILE"),
                )
                .arg(
                    Arg::new("combined-file")
                        .help("Sets the combined output file")
                        .required(false)
                        .long("combined")
                        .num_args(1)
                        .value_parser(clap::value_parser!(PathBuf))
                        .value_name("COMBINED_FILE"),
                ),
        )
        .arg(
            Arg::new("cmd")
                .help("Command to run after retrieving certificate")
                .global(true)
                .num_args(..)
                .trailing_var_arg(true)
                .value_parser(clap::value_parser!(OsString))
                .value_name("CMD"),
        )
        .get_matches();

    let url = matches
        .get_one::<String>("host")
        .ok_or(Error::MissingVal("HOST"))?;
    let client = workload(url)?;

    let module = matches
        .get_one::<String>("moduleid")
        .ok_or(Error::MissingVal("MODULEID"))?;
    let gen = matches
        .get_one::<String>("genid")
        .ok_or(Error::MissingVal("GENID"))?;

    if let Some(("cert-server", args)) = matches.subcommand() {
        let common_name = args
            .get_one::<String>("common-name")
            .ok_or(Error::MissingVal("COMMON_NAME"))?;
        let expiration = DateTime::parse_from_rfc3339(
            args.get_one::<String>("expiration")
                .ok_or(Error::MissingVal("EXPIRATION"))?,
        )?;
        let expiration_utc = expiration.with_timezone(&Utc);
        info!("Retrieving server certificate with common name \"{}\" and expiration \"{}\" from {}...", common_name, expiration, url);

        let response = client
            .create_server_cert(module, gen, common_name, expiration_utc)
            .await?;

        info!("Retrieved server certificate.");

        if let Some(crt_path) = args.get_one::<PathBuf>("crt-file") {
            fs::write(crt_path, response.certificate())?;
        }

        if let Some(key_path) = args.get_one::<PathBuf>("key-file") {
            if let Some(bytes) = response.private_key().bytes() {
                fs::write(key_path, bytes)?;
            }
        }

        if let Some(combined_path) = args.get_one::<PathBuf>("combined-file") {
            if let Some(bytes) = response.private_key().bytes() {
                fs::write(
                    combined_path,
                    format!("{}{}", response.certificate(), bytes),
                )?;
            }
        }
    }

    if let Some(cmd) = matches.get_many::<OsString>("cmd") {
        let cmd = cmd.collect::<Vec<_>>();
        info!(
            "Executing: {}",
            cmd.iter()
                .map(|s| s.to_string_lossy())
                .collect::<Vec<_>>()
                .join(" ")
        );
        if let Some((head, tail)) = cmd.split_first() {
            let mut child = process::Command::new(head).args(tail).spawn()?;
            child.wait()?;
        }
    }
    Ok(())
}
