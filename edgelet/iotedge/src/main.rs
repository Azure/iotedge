// Copyright (c) Microsoft. All rights reserved.

#![deny(unused_extern_crates, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::similar_names)]

#[macro_use]
extern crate clap;
extern crate edgelet_core;
extern crate edgelet_http_mgmt;
extern crate failure;
extern crate futures;
extern crate iotedge;
extern crate tokio;
extern crate url;

use std::io;
use std::process;

use clap::{App, AppSettings, Arg, SubCommand};
use failure::{Fail, ResultExt};
use futures::Future;
use url::Url;

use edgelet_core::{LogOptions, LogTail};
use edgelet_http_mgmt::ModuleClient;

use iotedge::*;

#[cfg(unix)]
const MGMT_URI: &str = "unix:///var/run/iotedge/mgmt.sock";
#[cfg(windows)]
const MGMT_URI: &str = "unix:///C:/ProgramData/iotedge/mgmt/sock";

fn main() {
    if let Err(ref error) = run() {
        let mut fail: &Fail = error;

        eprintln!("{}", error.to_string());

        for cause in fail.iter_causes() {
            eprintln!("\tcaused by: {}", cause);
        }

        eprintln!();

        process::exit(1);
    }
}

fn run() -> Result<(), Error> {
    let default_uri = option_env!("IOTEDGE_HOST").unwrap_or(MGMT_URI);

    let matches = App::new(crate_name!())
        .version(edgelet_core::version())
        .about(crate_description!())
        .setting(AppSettings::SubcommandRequiredElseHelp)
        .arg(
            Arg::with_name("host")
                .help("Daemon socket to connect to")
                .short("H")
                .long("host")
                .takes_value(true)
                .value_name("HOST")
                .global(true)
                .env("IOTEDGE_HOST")
                .default_value(default_uri),
        )
        .subcommand(
            SubCommand::with_name("check")
                .about("Check for common config and deployment issues")
                .arg(
                    Arg::with_name("config-file")
                        .short("c")
                        .long("config-file")
                        .value_name("FILE")
                        .help("Sets daemon configuration file")
                        .takes_value(true)
                        .default_value(
                            if cfg!(windows) { r"C:\ProgramData\iotedge\config.yaml" } else { "/etc/iotedge/config.yaml" }
                        ),
                )
                .arg(
                    Arg::with_name("iotedged")
                        .long("iotedged")
                        .value_name("PATH_TO_IOTEDGED")
                        .help("Sets the path of the iotedged binary.")
                        .takes_value(true)
                        .default_value(
                            if cfg!(windows) { r"C:\Program Files\iotedge\iotedged.exe" } else { "/usr/bin/iotedged" }
                        ),
                )
                .arg(
                    Arg::with_name("ntp-server")
                        .long("ntp-server")
                        .value_name("NTP_SERVER")
                        .help("Sets the NTP server to use when checking host local time.")
                        .takes_value(true)
                        .default_value("pool.ntp.org:123"),
                )
                .arg(
                    Arg::with_name("steps")
                        .help("Run specific steps instead of all steps. One or more of `config`, `deps`, `conn`")
                        .multiple(true)
                        .takes_value(true),
                ),
        )
        .subcommand(SubCommand::with_name("list").about("List modules"))
        .subcommand(
            SubCommand::with_name("restart")
                .about("Restart a module")
                .arg(
                    Arg::with_name("MODULE")
                        .help("Sets the module identity to restart")
                        .required(true)
                        .index(1),
                ),
        )
        .subcommand(
            SubCommand::with_name("logs")
                .about("Fetch the logs of a module")
                .arg(
                    Arg::with_name("MODULE")
                        .help("Sets the module identity to get logs")
                        .required(true)
                        .index(1),
                )
                .arg(
                    Arg::with_name("tail")
                        .help("Number of lines to show from the end of the log")
                        .long("tail")
                        .takes_value(true)
                        .value_name("NUM")
                        .default_value("all"),
                )
                .arg(
                    Arg::with_name("follow")
                        .help("Follow output log")
                        .short("f")
                        .long("follow"),
                ),
        )
        .subcommand(SubCommand::with_name("version").about("Show the version information"))
        .get_matches();

    let url = matches.value_of("host").map_or_else(
        || Err(Error::from(ErrorKind::MissingHostParameter)),
        |h| {
            Url::parse(h)
                .context(ErrorKind::BadHostParameter)
                .map_err(Error::from)
        },
    )?;
    let runtime = ModuleClient::new(&url).context(ErrorKind::ModuleRuntime)?;

    let mut tokio_runtime = tokio::runtime::Runtime::new().context(ErrorKind::InitializeTokio)?;

    match matches.subcommand() {
        ("check", Some(args)) => tokio_runtime.block_on(
            Check::new(
                args.value_of_os("config-file")
                    .expect("arg has a default value")
                    .to_os_string()
                    .into(),
                args.value_of_os("iotedged")
                    .expect("arg has a default value")
                    .to_os_string()
                    .into(),
                args.value_of("ntp-server")
                    .expect("arg has a default value")
                    .to_string(),
                args.values_of("steps")
                    .map(|values| values.map(ToOwned::to_owned).collect()),
            )
            .and_then(|mut check| check.execute()),
        ),
        ("list", Some(_args)) => tokio_runtime.block_on(List::new(runtime, io::stdout()).execute()),
        ("restart", Some(args)) => tokio_runtime.block_on(
            Restart::new(
                args.value_of("MODULE").unwrap().to_string(),
                runtime,
                io::stdout(),
            )
            .execute(),
        ),
        ("logs", Some(args)) => {
            let id = args.value_of("MODULE").unwrap().to_string();
            let follow = args.is_present("follow");
            let tail = args
                .value_of("tail")
                .and_then(|a| a.parse::<LogTail>().ok())
                .unwrap_or_default();
            let options = LogOptions::new().with_follow(follow).with_tail(tail);
            tokio_runtime.block_on(Logs::new(id, options, runtime).execute())
        }
        ("version", Some(_args)) => tokio_runtime.block_on(Version::new().execute()),
        (command, _) => tokio_runtime.block_on(Unknown::new(command.to_string()).execute()),
    }
}
