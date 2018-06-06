// Copyright (c) Microsoft. All rights reserved.

#[macro_use]
extern crate clap;
extern crate edgelet_core;
extern crate edgelet_http_mgmt;
extern crate failure;
extern crate futures;
extern crate hyper;
extern crate iotedge;
extern crate management;
extern crate tokio_core;
extern crate url;

use std::io;
use std::io::Write;
use std::process;

use clap::{App, AppSettings, Arg, SubCommand};
use edgelet_core::{LogOptions, LogTail};
use edgelet_http_mgmt::ModuleClient;
use failure::Fail;
use iotedge::*;
use tokio_core::reactor::Core;
use url::Url;

#[cfg(unix)]
const MGMT_URI: &str = "unix:///var/run/iotedge/mgmt.sock";
#[cfg(windows)]
const MGMT_URI: &str = "http://localhost:8080";

fn main() {
    if let Err(ref error) = run() {
        let stderr = &mut io::stderr();
        let errmsg = "Error writing to stderr";

        let mut fail: &Fail = error;
        writeln!(stderr, "{}", error.to_string()).expect(errmsg);
        while let Some(cause) = fail.cause() {
            writeln!(stderr, "\tcaused by: {}", cause.to_string()).expect(errmsg);
            fail = cause;
        }
        process::exit(1);
    }
}

fn run() -> Result<(), Error> {
    let mut core = Core::new()?;
    let url = Url::parse(MGMT_URI)?;
    let runtime = ModuleClient::new(&url, &core.handle())?;

    let matches = App::new(crate_name!())
        .version(crate_version!())
        .author(crate_authors!("\n"))
        .about(crate_description!())
        .setting(AppSettings::SubcommandRequiredElseHelp)
        .subcommand(SubCommand::with_name("list").about("List modules"))
        .subcommand(
            SubCommand::with_name("start").about("Start a module").arg(
                Arg::with_name("MODULE")
                    .help("Sets the module identity to start")
                    .required(true)
                    .index(1),
            ),
        )
        .subcommand(
            SubCommand::with_name("stop").about("Stop a module").arg(
                Arg::with_name("MODULE")
                    .help("Sets the module identity to stop")
                    .required(true)
                    .index(1),
            ),
        )
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

    match matches.subcommand() {
        ("list", Some(_args)) => core.run(List::new(runtime, io::stdout()).execute()),
        ("start", Some(args)) => core.run(
            Start::new(
                args.value_of("MODULE").unwrap().to_string(),
                runtime,
                io::stdout(),
            ).execute(),
        ),
        ("stop", Some(args)) => core.run(
            Stop::new(
                args.value_of("MODULE").unwrap().to_string(),
                runtime,
                io::stdout(),
            ).execute(),
        ),
        ("restart", Some(args)) => core.run(
            Restart::new(
                args.value_of("MODULE").unwrap().to_string(),
                runtime,
                io::stdout(),
            ).execute(),
        ),
        ("logs", Some(args)) => {
            let id = args.value_of("MODULE").unwrap().to_string();
            let follow = args.is_present("follow");
            let tail = args.value_of("tail")
                .and_then(|a| a.parse::<LogTail>().ok())
                .unwrap_or_default();
            let options = LogOptions::new().with_follow(follow).with_tail(tail);
            core.run(Logs::new(id, options, runtime).execute())
        }
        ("version", Some(_args)) => core.run(Version::new().execute()),
        (command, _) => core.run(Unknown::new(command.to_string()).execute()),
    }
}
