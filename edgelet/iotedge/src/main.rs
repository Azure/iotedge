// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::similar_names)]

use std::borrow::Cow;
use std::convert::TryInto;
use std::io;
use std::path::{Path, PathBuf};
use std::process;

use chrono::{DateTime, Duration, Local};
use clap::{crate_description, crate_name, App, AppSettings, Arg, SubCommand};
use failure::{Fail, ResultExt};
use futures::Future;
use url::Url;

use edgelet_core::{LogOptions, LogTail};
use edgelet_http_mgmt::ModuleClient;

use iotedge::*;

fn main() {
    if let Err(ref error) = run() {
        let fail: &dyn Fail = error;

        eprintln!("{}", error.to_string());

        for cause in fail.iter_causes() {
            eprintln!("\tcaused by: {}", cause);
        }

        eprintln!();

        process::exit(1);
    }
}

#[allow(clippy::too_many_lines)]
fn run() -> Result<(), Error> {
    let (default_mgmt_uri, default_config_path, default_container_engine_config_path) =
        if cfg!(windows) {
            let program_data: PathBuf = std::env::var_os("PROGRAMDATA")
                .map_or_else(|| r"C:\ProgramData".into(), Into::into);

            let default_mgmt_uri = program_data
                .to_str()
                .expect("PROGRAMDATA is not a utf-8 path")
                .replace('\\', "/");
            let default_mgmt_uri = format!("unix:///{}/iotedge/mgmt/sock", default_mgmt_uri);
            let default_mgmt_uri = Cow::Owned(default_mgmt_uri);

            let mut default_config_path = program_data.clone();
            default_config_path.push("iotedge");
            default_config_path.push("config.yaml");
            let default_config_path = Cow::Owned(default_config_path);

            let mut default_container_engine_config_path = program_data;
            default_container_engine_config_path.push("iotedge-moby");
            default_container_engine_config_path.push("config");
            default_container_engine_config_path.push("daemon.json");
            let default_container_engine_config_path =
                Cow::Owned(default_container_engine_config_path);

            (
                default_mgmt_uri,
                default_config_path,
                default_container_engine_config_path,
            )
        } else {
            (
                Cow::Borrowed("unix:///var/run/iotedge/mgmt.sock"),
                Cow::Borrowed(Path::new("/etc/iotedge/config.yaml")),
                Cow::Borrowed(Path::new("/etc/docker/daemon.json")),
            )
        };

    let default_mgmt_uri = option_env!("IOTEDGE_HOST").unwrap_or(&*default_mgmt_uri);

    let default_diagnostics_image_name = format!(
        "mcr.microsoft.com/azureiotedge-diagnostics:{}",
        edgelet_core::version().replace("~", "-")
    );

    let mut possible_check_id_values: Vec<_> = Check::possible_ids().collect();
    possible_check_id_values.sort();

    let matches = App::new(crate_name!())
        .version(edgelet_core::version_with_source_version())
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
                .default_value(default_mgmt_uri),
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
                        .default_value_os(default_config_path.as_os_str()),
                )
                .arg(
                    Arg::with_name("container-engine-config-file")
                        .long("container-engine-config-file")
                        .value_name("FILE")
                        .help("Sets the path of the container engine configuration file")
                        .takes_value(true)
                        .default_value_os(default_container_engine_config_path.as_os_str()),
                )
                .arg(
                    Arg::with_name("diagnostics-image-name")
                        .long("diagnostics-image-name")
                        .value_name("IMAGE_NAME")
                        .help("Sets the name of the azureiotedge-diagnostics image.")
                        .takes_value(true)
                        .default_value(&default_diagnostics_image_name),
                )
                .arg(
                    Arg::with_name("dont-run")
                        .long("dont-run")
                        .value_name("DONT_RUN")
                        .help("Space-separated list of check IDs. The checks listed here will not be run. See 'iotedge check-list' for details of all checks.\n")
                        .multiple(true)
                        .takes_value(true)
                        .possible_values(&possible_check_id_values),
                )
                .arg(
                    Arg::with_name("expected-iotedged-version")
                        .long("expected-iotedged-version")
                        .value_name("VERSION")
                        .help("Sets the expected version of the iotedged binary. Defaults to the value contained in <http://aka.ms/latest-iotedge-stable>")
                        .takes_value(true),
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
                    Arg::with_name("iothub-hostname")
                        .long("iothub-hostname")
                        .value_name("IOTHUB_HOSTNAME")
                        .help("Sets the hostname of the Azure IoT Hub that this device would connect to. If using manual provisioning, this does not need to be specified.")
                        .takes_value(true),
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
                    Arg::with_name("output")
                        .long("output")
                        .short("o")
                        .value_name("FORMAT")
                        .help("Output format. Note that JSON output contains some additional information like OS name, OS version, disk space, etc.")
                        .takes_value(true)
                        .possible_values(&["json", "text"])
                        .default_value("text"),
                )
                .arg(
                    Arg::with_name("verbose")
                        .long("verbose")
                        .value_name("VERBOSE")
                        .help("Increases verbosity of output.")
                        .takes_value(false),
                )
                .arg(
                    Arg::with_name("warnings-as-errors")
                        .long("warnings-as-errors")
                        .value_name("WARNINGS_AS_ERRORS")
                        .help("Treats warnings as errors. Thus 'iotedge check' will exit with non-zero code if it encounters warnings.")
                        .takes_value(false),
                ),
        )
        .subcommand(SubCommand::with_name("check-list").about("List the checks that are run for 'iotedge check'"))
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
                    Arg::with_name("since")
                        .help("Only return logs since this time, as a duration (1 day, 90 minutes, 2 days 3 hours 2 minutes), rfc3339 timestamp, or UNIX timestamp")
                        .long("since")
                        .takes_value(true)
                        .value_name("DURATION or TIMESTAMP")
                        .default_value("1 day"),
                )
                .arg(
                    Arg::with_name("follow")
                        .help("Follow output log")
                        .short("f")
                        .long("follow"),
                ),
        )
        .subcommand(
            SubCommand::with_name("support-bundle")
                .about("Bundles troubleshooting information")
                .arg(
                    Arg::with_name("output")
                        .help("Path of output file")
                        .long("output")
                        .short("o")
                        .takes_value(true)
                        .value_name("FILENAME")
                        .default_value("support_bundle.zip"),
                )
                .arg(
                    Arg::with_name("since")
                        .help("Only return logs since this time, as a duration (1d, 90m, 2h30m), rfc3339 timestamp, or UNIX timestamp")
                        .long("since")
                        .takes_value(true)
                        .value_name("DURATION or TIMESTAMP")
                        .default_value("1 day"),
                )
                .arg(
                    Arg::with_name("include-edge-runtime-only")
                        .help("Only include logs from Microsoft-owned Edge modules")
                        .long("include-edge-runtime-only")
                        .short("e")
                        .takes_value(false),
                ).arg(
                    Arg::with_name("iothub-hostname")
                        .long("iothub-hostname")
                        .value_name("IOTHUB_HOSTNAME")
                        .help("Sets the hostname of the Azure IoT Hub that this device would connect to. If using manual provisioning, this does not need to be specified.")
                        .takes_value(true),
                ).arg(
                    Arg::with_name("quiet")
                        .help("Suppress output")
                        .long("quiet")
                        .short("q")
                        .takes_value(false),
                ),
        )
        .subcommand(SubCommand::with_name("version").about("Show the version information"))
        .get_matches();

    let runtime = || -> Result<_, Error> {
        let url = matches.value_of("host").map_or_else(
            || Err(Error::from(ErrorKind::MissingHostParameter)),
            |h| {
                Url::parse(h)
                    .context(ErrorKind::BadHostParameter)
                    .map_err(Error::from)
            },
        )?;
        let runtime = ModuleClient::new(&url).context(ErrorKind::ModuleRuntime)?;
        Ok(runtime)
    };

    let mut tokio_runtime = tokio::runtime::Runtime::new().context(ErrorKind::InitializeTokio)?;

    match matches.subcommand() {
        ("check", Some(args)) => tokio_runtime.block_on(
            Check::new(
                args.value_of_os("config-file")
                    .expect("arg has a default value")
                    .to_os_string()
                    .into(),
                args.value_of_os("container-engine-config-file")
                    .expect("arg has a default value")
                    .to_os_string()
                    .into(),
                args.value_of("diagnostics-image-name")
                    .expect("arg has a default value")
                    .to_string(),
                args.values_of("dont-run")
                    .into_iter()
                    .flatten()
                    .map(ToOwned::to_owned)
                    .collect(),
                args.value_of("expected-iotedged-version")
                    .map(ToOwned::to_owned),
                args.value_of_os("iotedged")
                    .expect("arg has a default value")
                    .to_os_string()
                    .into(),
                args.value_of("iothub-hostname").map(ToOwned::to_owned),
                args.value_of("ntp-server")
                    .expect("arg has a default value")
                    .to_string(),
                args.value_of("output")
                    .map(|arg| match arg {
                        "json" => OutputFormat::Json,
                        "text" => OutputFormat::Text,
                        _ => unreachable!(),
                    })
                    .expect("arg has a default value"),
                args.is_present("verbose"),
                args.is_present("warnings-as-errors"),
            )
            .and_then(Command::execute),
        ),
        ("check-list", _) => Check::print_list(),
        ("list", _) => tokio_runtime.block_on(List::new(runtime()?, io::stdout()).execute()),
        ("restart", Some(args)) => tokio_runtime.block_on(
            Restart::new(
                args.value_of("MODULE").unwrap().to_string(),
                runtime()?,
                io::stdout(),
            )
            .execute(),
        ),
        ("logs", Some(args)) => {
            let id = args.value_of("MODULE").unwrap().to_string();
            let follow = args.is_present("follow");
            let tail = args
                .value_of("tail")
                .map(str::parse)
                .transpose()
                .map_err(|err: edgelet_core::Error| {
                    Error::from(err.context(ErrorKind::BadTailParameter))
                })?
                .expect("arg has a default value");
            let since = args
                .value_of("since")
                .map(|s| parse_since(s))
                .transpose()?
                .expect("arg has a default value");
            let options = LogOptions::new()
                .with_follow(follow)
                .with_tail(tail)
                .with_since(since);
            tokio_runtime.block_on(Logs::new(id, options, runtime()?).execute())
        }
        ("support-bundle", Some(args)) => {
            let location = args.value_of_os("output").expect("arg has a default value");
            let since = args
                .value_of("since")
                .map(|s| parse_since(s))
                .transpose()?
                .expect("arg has a default value");
            let options = LogOptions::new()
                .with_follow(false)
                .with_tail(LogTail::All)
                .with_since(since);
            let include_ms_only = args.is_present("include-edge-runtime-only");
            let verbose = !args.is_present("quiet");
            let iothub_hostname = args.value_of("iothub-hostname").map(ToOwned::to_owned);
            tokio_runtime.block_on(
                SupportBundle::new(
                    options,
                    location.to_owned(),
                    include_ms_only,
                    verbose,
                    iothub_hostname,
                    runtime()?,
                )
                .execute(),
            )
        }
        ("version", _) => tokio_runtime.block_on(Version::new().execute()),
        (command, _) => tokio_runtime.block_on(Unknown::new(command.to_string()).execute()),
    }
}

fn parse_since(since: &str) -> Result<i32, Error> {
    if let Ok(datetime) = DateTime::parse_from_rfc3339(since) {
        let temp: Result<i32, _> = datetime.timestamp().try_into();
        Ok(temp.context(ErrorKind::BadSinceParameter)?)
    } else if let Ok(epoch) = since.parse() {
        Ok(epoch)
    } else if let Ok(duration) = parse_duration::parse(since) {
        let nano: Result<i64, _> = duration.as_nanos().try_into();
        let nano = nano.context(ErrorKind::BadSinceParameter)?;

        let temp: Result<i32, _> = (Local::now() - Duration::nanoseconds(nano))
            .timestamp()
            .try_into();
        Ok(temp.context(ErrorKind::BadSinceParameter)?)
    } else {
        Err(Error::from(ErrorKind::BadSinceParameter))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_rfc3339() {
        assert_eq!(
            parse_since("2019-09-27T16:00:00+00:00").unwrap(),
            1_569_600_000
        );
    }

    #[test]
    fn parse_english() {
        assert_near(
            parse_since("1 hour").unwrap(),
            (Local::now() - Duration::hours(1))
                .timestamp()
                .try_into()
                .unwrap(),
            10,
        );

        assert_near(
            parse_since("1 hour 20 minutes").unwrap(),
            (Local::now() - Duration::hours(1) - Duration::minutes(20))
                .timestamp()
                .try_into()
                .unwrap(),
            10,
        );

        assert_near(
            parse_since("1 day").unwrap(),
            (Local::now() - Duration::days(1))
                .timestamp()
                .try_into()
                .unwrap(),
            10,
        );
    }

    #[test]
    fn parse_unix() {
        assert_eq!(parse_since("1569600000").unwrap(), 1_569_600_000);
    }

    #[test]
    fn parse_default() {
        let _ = parse_since("asdfasdf").unwrap_err();
    }

    fn assert_near(a: i32, b: i32, tol: i32) {
        assert!((a - b).abs() < tol)
    }
}
