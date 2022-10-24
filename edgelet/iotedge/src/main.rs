// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(clippy::let_unit_value, clippy::similar_names)]

use std::ffi::OsString;
use std::io;
use std::path::PathBuf;
use std::process;

use anyhow::Context;
use clap::builder::TypedValueParser;
use clap::{crate_description, crate_name, Arg, Command};
use url::Url;

use edgelet_core::{parse_since, LogOptions, LogTail};
use support_bundle::OutputLocation;

use iotedge::{
    Check, Error, List, Logs, MgmtClient, OutputFormat, Restart, SupportBundleCommand, System,
    Version,
};

#[tokio::main]
async fn main() {
    if let Err(ref error) = run().await {
        let mut chain = error.chain();

        eprintln!("{}", chain.next().unwrap());

        for cause in chain {
            eprintln!("\tcaused by: {}", cause);
        }

        eprintln!();

        process::exit(1);
    }
}

#[allow(clippy::too_many_lines)]
async fn run() -> anyhow::Result<()> {
    let aziot_bin = option_env!("AZIOT_BIN").unwrap_or("aziotctl");

    let default_mgmt_uri = option_env!("IOTEDGE_CONNECT_MANAGEMENT_URI")
        .unwrap_or("unix:///var/run/iotedge/mgmt.sock");

    let default_diagnostics_image_name = format!(
        "/azureiotedge-diagnostics:{}",
        edgelet_core::version().replace('~', "-")
    );

    let default_support_bundle_name = format!(
        "support_bundle{}.zip",
        chrono::Utc::now().format("_%Y_%m_%d_%H_%M_%S_%Z")
    );

    let matches = Command::new(crate_name!())
        .version(edgelet_core::version_with_source_version())
        .about(crate_description!())
        .subcommand_required(true)
        .arg_required_else_help(true)
        .arg(
            Arg::new("host")
                .help("Daemon socket to connect to")
                .short('H')
                .long("host")
                .num_args(1)
                .value_name("HOST")
                .global(true)
                .env("IOTEDGE_HOST")
                .default_value(default_mgmt_uri),
        )
        .subcommand(
            Command::new("check")
                .about("Check for common config and deployment issues")
                .arg(
                    Arg::new("container-engine-config-file")
                        .long("container-engine-config-file")
                        .value_name("FILE")
                        .help("Sets the path of the container engine configuration file")
                        .num_args(1)
                        .value_parser(clap::value_parser!(PathBuf))
                        .default_value("/etc/docker/daemon.json"),
                )
                .arg(
                    Arg::new("diagnostics-image-name")
                        .long("diagnostics-image-name")
                        .value_name("IMAGE_NAME")
                        .help("Sets the name of the azureiotedge-diagnostics image.")
                        .num_args(1)
                        .default_value(&default_diagnostics_image_name),
                )
                .arg(
                    Arg::new("dont-run")
                        .long("dont-run")
                        .value_name("DONT_RUN")
                        .help("Space-separated list of check IDs. The checks listed here will not be run. See 'iotedge check-list' for details of all checks.\n")
                        .num_args(..)
                )
                .arg(
                    Arg::new("expected-aziot-edged-version")
                        .long("expected-aziot-edged-version")
                        .value_name("VERSION")
                        .help("Sets the expected version of the aziot-edged binary. Defaults to the value contained in <https://aka.ms/latest-aziot-edge>")
                        .num_args(1),
                )
                .arg(
                    Arg::new("expected-aziot-version")
                        .long("expected-aziot-version")
                        .value_name("AZIOT_VERSION")
                        .help("Sets the expected version of the aziot-identity-service package. Defaults to the value contained in <https://aka.ms/latest-aziot-identity-service>")
                        .num_args(1)
                )
                .arg(
                    Arg::new("aziot-edged")
                        .long("aziot-edged")
                        .value_name("PATH_TO_AZIOT_EDGED")
                        .help("Sets the path of the aziot-edged binary.")
                        .num_args(1)
                        .value_parser(clap::value_parser!(PathBuf))
                        .default_value("/usr/libexec/aziot/aziot-edged"),
                )
                .arg(
                    Arg::new("iothub-hostname")
                        .long("iothub-hostname")
                        .value_name("IOTHUB_HOSTNAME")
                        .help("Sets the hostname of the Azure IoT Hub that this device would connect to. If using manual provisioning, this does not need to be specified.")
                        .num_args(1),
                )
                .arg(
                    Arg::new("proxy-uri")
                        .long("proxy-uri")
                        .value_name("PROXY_URI")
                        .help("Sets the proxy URI that this device would use to connect to Azure DPS and IoTHub endpoints.")
                        .num_args(1),
                )
                .arg(
                    Arg::new("ntp-server")
                        .long("ntp-server")
                        .value_name("NTP_SERVER")
                        .help("Sets the NTP server to use when checking host local time.")
                        .num_args(1)
                        .default_value("pool.ntp.org:123"),
                )
                .arg(
                    Arg::new("output")
                        .long("output")
                        .short('o')
                        .value_name("FORMAT")
                        .help("Output format. Note that JSON output contains some additional information like OS name, OS version, disk space, etc.")
                        .num_args(1)
                        .value_parser(["json", "text"])
                        .default_value("text"),
                )
                .arg(
                    Arg::new("verbose")
                        .long("verbose")
                        .value_name("VERBOSE")
                        .num_args(0)
                        .help("Increases verbosity of output.")
                )
                .arg(
                    Arg::new("warnings-as-errors")
                        .long("warnings-as-errors")
                        .value_name("WARNINGS_AS_ERRORS")
                        .num_args(0)
                        .help("Treats warnings as errors. Thus 'iotedge check' will exit with non-zero code if it encounters warnings.")
                ),
        )
        .subcommand(Command::new("check-list").about("List the checks that are run for 'iotedge check'"))
        .subcommand(
            Command::new("config")
                .about("Manage Azure IoT Edge system configuration.")
                .subcommand_required(true)
                .arg_required_else_help(true)
                .subcommand(
                    Command::new("apply")
                    .about("Apply Azure IoT Edge system configuration values.")
                    .arg(
                        Arg::new("config-file")
                            .short('c')
                            .long("config-file")
                            .value_name("FILE")
                            .help("The path of the IoT Edge system configuration file")
                            .num_args(1)
                            .value_parser(clap::value_parser!(PathBuf))
                            .default_value("/etc/aziot/config.toml"),
                    )
                )
                .subcommand(
                    Command::new("import")
                    .about("Initialize Azure IoT Edge system configuration by importing configuration of an existing pre-1.2 installation.")
                    .arg(
                        Arg::new("config-file")
                            .short('c')
                            .long("config-file")
                            .value_name("FILE")
                            .help("The path of the pre-1.2 configuration file to import")
                            .num_args(1)
                            .value_parser(clap::value_parser!(PathBuf))
                            .default_value("/etc/iotedge/config.yaml"),
                    )
                    .arg(
                        Arg::new("out-config-file")
                            .short('o')
                            .long("out-config-file")
                            .value_name("FILE")
                            .help("The path of the Azure IoT Edge system configuration file to write to")
                            .num_args(1)
                            .value_parser(clap::value_parser!(PathBuf))
                            .default_value("/etc/aziot/config.toml"),
                    )
                    .arg(
                        Arg::new("force")
                            .short('f')
                            .long("force")
                            .num_args(0)
                            .help("Overwrite the new configuration file if it already exists")
                    )
                )
                .subcommand(
                    Command::new("mp")
                    .about("Quick-create Azure IoT Edge system configuration for manual provisioning with a connection string.")
                    .arg(
                        Arg::new("connection-string")
                            .short('c')
                            .long("connection-string")
                            .value_name("CONNECTION_STRING")
                            .help("The Azure IoT Hub connection string")
                            .required(true)
                            .num_args(1),
                    )
                    .arg(
                        Arg::new("out-config-file")
                            .short('o')
                            .long("out-config-file")
                            .value_name("FILE")
                            .help("The path of the Azure IoT Edge system configuration file to write to")
                            .num_args(1)
                            .value_parser(clap::value_parser!(PathBuf))
                            .default_value("/etc/aziot/config.toml"),
                    )
                    .arg(
                        Arg::new("force")
                            .short('f')
                            .long("force")
                            .num_args(0)
                            .help("Overwrite the new configuration file if it already exists")
                    )
                )
        )
        .subcommand(Command::new("list").about("List modules"))
        .subcommand(
            Command::new("restart")
                .about("Restart a module")
                .arg(
                    Arg::new("MODULE")
                        .help("Sets the module identity to restart")
                        .required(true)
                        .index(1),
                ),
        )
        .subcommand(
            Command::new("logs")
                .about("Fetch the logs of a module")
                .arg(
                    Arg::new("MODULE")
                        .help("Sets the module identity to get logs")
                        .required(true)
                        .index(1),
                )
                .arg(
                    Arg::new("tail")
                        .help("Number of lines to show from the end of the log")
                        .long("tail")
                        .num_args(1)
                        .value_name("NUM")
                        .default_value("all"),
                )
                .arg(
                    Arg::new("since")
                        .help("Only return logs since this time, as a duration (1 day, 90 minutes, 2 days 3 hours 2 minutes), rfc3339 timestamp, or UNIX timestamp")
                        .long("since")
                        .num_args(1)
                        .value_name("DURATION or TIMESTAMP")
                        .default_value("1 day"),
                )
                .arg(
                    Arg::new("until")
                        .help("Only return logs up to this time, as a duration (1 day, 90 minutes, 2 days 3 hours 2 minutes), rfc3339 timestamp, or UNIX timestamp. For example, 0d would not truncate any logs, while 2h would return logs up to 2 hours ago")
                        .long("until")
                        .num_args(1)
                        .value_name("DURATION or TIMESTAMP"),
                )
                .arg(
                    Arg::new("follow")
                        .short('f')
                        .long("follow")
                        .num_args(0)
                        .help("Follow output log"),
                ),
        )
        .subcommand(
            Command::new("system")
                .about("Manage system services for IoT Edge.")
                .subcommand_required(true)
                .arg_required_else_help(true)
                .subcommand(
                    Command::new("logs")
                    .about("Provides a combined view of logs for IoT Edge system services. Precede arguments intended for journalctl with a double-hyphen -- . Example: iotedge system logs -- -f.")
                    .arg(
                        Arg::new("args")
                            .last(true)
                            .help("Additional argumants to pass to journalctl. See journalctl -h for more information.")
                            .num_args(..)
                            .value_parser(clap::value_parser!(OsString)),
                    )
                )
                .subcommand(
                    Command::new("restart")
                    .about("Restarts aziot-edged and all of its dependencies.")
                )
                .subcommand(
                    Command::new("stop")
                    .about("Stops aziot-edged and all of its dependencies.")
                )
                .subcommand(
                    Command::new("status")
                    .about("Report the status of aziot-edged and all of its dependencies.")
                )
                .subcommand(
                    Command::new("set-log-level")
                    .about("Set the log level of aziot-edged and all of its dependencies.")
                    .arg(
                        // NOTE: Possible value references:
                        // - https://github.com/rust-lang/log/blob/d6707108c6959ac7b60cdb60a005795ece6d82d6/src/lib.rs#L411
                        // - https://github.com/rust-lang/log/blob/d6707108c6959ac7b60cdb60a005795ece6d82d6/src/lib.rs#L473-L487
                        // WARN: "off" is excluded from the `FromStr`
                        // implementation on `log::Level`:
                        // https://github.com/rust-lang/log/blob/d6707108c6959ac7b60cdb60a005795ece6d82d6/src/lib.rs#L481
                        Arg::new("log_level")
                        .value_parser(clap::builder::PossibleValuesParser::new(["error", "warn", "info", "debug", "trace"])
                            .try_map(|s| s.parse::<log::Level>()))
                        .required(true),
                    )
                )
                .subcommand(
                    Command::new("reprovision")
                    .about("Reprovision device with IoT Hub.")
                )
        )
        .subcommand(
            Command::new("support-bundle")
                .about("Bundles troubleshooting information")
                .arg(
                    Arg::new("output")
                        .help("Location to output file. Use - for stdout")
                        .long("output")
                        .short('o')
                        .num_args(1)
                        .value_parser(clap::value_parser!(PathBuf))
                        .value_name("FILENAME")
                        .default_value(&default_support_bundle_name),
                )
                .arg(
                    Arg::new("since")
                        .help("Only return logs since this time, as a duration (1d, 90m, 2h30m), rfc3339 timestamp, or UNIX timestamp")
                        .long("since")
                        .num_args(1)
                        .value_name("DURATION or TIMESTAMP")
                        .default_value("1 day"),
                )
                .arg(
                    Arg::new("until")
                        .help("Only return logs up to this time, as a duration (1 day, 90 minutes, 2 days 3 hours 2 minutes), rfc3339 timestamp, or UNIX timestamp. For example, 0d would not truncate any logs, while 2h would return logs up to 2 hours ago")
                        .long("until")
                        .num_args(1)
                        .value_name("DURATION or TIMESTAMP")
                )
                .arg(
                    Arg::new("include-edge-runtime-only")
                        .short('e')
                        .long("include-edge-runtime-only")
                        .num_args(0)
                        .help("Only include logs from Microsoft-owned Edge modules")
                ).arg(
                    Arg::new("iothub-hostname")
                        .long("iothub-hostname")
                        .value_name("IOTHUB_HOSTNAME")
                        .help("Sets the hostname of the Azure IoT Hub that this device would connect to. If using manual provisioning, this does not need to be specified.")
                        .num_args(1),
                ).arg(
                    Arg::new("quiet")
                        .short('q')
                        .long("quiet")
                        .num_args(0)
                        .help("Suppress status output")
                ),
        )
        .subcommand(Command::new("version").about("Show the version information"))
        .get_matches();

    let runtime = || -> anyhow::Result<_> {
        let url = matches.get_one::<String>("host").map_or_else(
            || Err(Error::MissingHostParameter.into()),
            |h| Url::parse(h).context(Error::BadHostParameter),
        )?;
        MgmtClient::new(&url)
    };

    match matches
        .subcommand()
        .expect("Command::subcommand_required was set, but ArgMatches::subcommand was None")
    {
        ("check", args) => {
            let mut check = Check::new(
                args.get_one::<PathBuf>("container-engine-config-file")
                    .expect("arg has a default value")
                    .into(),
                args.get_one::<String>("diagnostics-image-name")
                    .expect("arg has a default value")
                    .clone(),
                args.get_many::<String>("dont-run")
                    .into_iter()
                    .flatten()
                    .cloned()
                    .collect(),
                args.get_one::<String>("expected-aziot-edged-version")
                    .cloned(),
                args.get_one::<String>("expected-aziot-version").cloned(),
                args.get_one::<PathBuf>("aziot-edged")
                    .expect("arg has a default value")
                    .into(),
                args.get_one::<String>("output")
                    .map(|arg| match &**arg {
                        "json" => OutputFormat::Json,
                        "text" => OutputFormat::Text,
                        _ => unreachable!(),
                    })
                    .expect("arg has a default value"),
                args.get_flag("verbose"),
                args.get_flag("warnings-as-errors"),
                aziot_bin.into(),
                args.get_one::<String>("iothub-hostname").cloned(),
                args.get_one::<String>("proxy-uri").cloned(),
            );
            check.execute().await
        }
        ("check-list", _) => Check::print_list(aziot_bin).await,
        ("config", args) => {
            match args
                .subcommand()
                .expect("Command::subcommand_required was set, but ArgMatches::subcommand was None")
            {
                ("apply", args) => {
                    let config_file = args
                        .get_one::<PathBuf>("config-file")
                        .expect("arg has a default value");

                    let () = iotedge::config::apply::execute(config_file)
                        .await
                        .map_err(Error::Config)?;
                    Ok(())
                }
                ("import", args) => {
                    let old_config_file = args
                        .get_one::<PathBuf>("config-file")
                        .expect("arg has a default value");

                    let new_config_file = args
                        .get_one::<PathBuf>("out-config-file")
                        .expect("arg has a default value");

                    let force = args.get_flag("force");

                    let () =
                        iotedge::config::import::execute(old_config_file, new_config_file, force)
                            .map_err(Error::Config)?;
                    Ok(())
                }
                ("mp", args) => {
                    let connection_string = args
                        .get_one::<String>("connection-string")
                        .expect("arg is required")
                        .clone();

                    let out_config_file = args
                        .get_one::<PathBuf>("out-config-file")
                        .expect("arg has a default value");

                    let force = args.get_flag("force");

                    let () =
                        iotedge::config::mp::execute(connection_string, out_config_file, force)
                            .map_err(Error::Config)?;
                    Ok(())
                }
                (command, _) => {
                    eprintln!("Unknown config subcommand: {}", command);
                    std::process::exit(1);
                }
            }
        }
        ("list", _) => List::new(runtime()?, io::stdout()).execute().await,
        ("restart", args) => {
            Restart::new(
                args.get_one::<String>("MODULE").unwrap().to_string(),
                runtime()?,
                io::stdout(),
            )
            .execute()
            .await
        }
        ("logs", args) => {
            let id = args.get_one::<String>("MODULE").unwrap().to_string();
            let follow = args.get_flag("follow");
            let tail = args
                .get_one::<String>("tail")
                .map(|s| s.parse())
                .transpose()
                .context(Error::BadTailParameter)?
                .expect("arg has a default value");
            let since = args
                .get_one::<String>("since")
                .map(|s| parse_since(s))
                .transpose()
                .context(Error::BadSinceParameter)?
                .expect("arg has a default value");
            let mut options = LogOptions::new()
                .with_follow(follow)
                .with_tail(tail)
                .with_since(since);
            if let Some(until) = args
                .get_one::<String>("until")
                .map(|s| parse_since(s))
                .transpose()
                .context(Error::BadUntilParameter)?
            {
                options = options.with_until(until);
            }

            Logs::new(id, options, runtime()?).execute().await
        }
        ("system", args) => (match args
            .subcommand()
            .expect("Command::subcommand_required was set, but ArgMatches::subcommand was None")
        {
            ("logs", args) => {
                let jctl_args = args
                    .get_many::<OsString>("args")
                    .map_or_else(Vec::new, |args| args.map(AsRef::as_ref).collect());

                System::get_system_logs(&jctl_args)
            }
            ("restart", _) => System::system_restart(),
            ("stop", _) => System::system_stop(),
            ("status", _) => System::get_system_status(),
            ("set-log-level", args) => System::set_log_level(
                args.get_one::<log::Level>("log_level")
                    .copied()
                    .expect("Value is required"),
            ),
            ("reprovision", _) => System::reprovision().await,
            (command, _) => {
                eprintln!("Unknown system subcommand: {}", command);
                std::process::exit(1);
            }
        })
        .map_err(anyhow::Error::from),
        ("support-bundle", args) => {
            let location = args
                .get_one::<PathBuf>("output")
                .expect("arg has a default value");
            let since = args
                .get_one::<String>("since")
                .map(|s| parse_since(s))
                .transpose()
                .context(Error::BadSinceParameter)?
                .expect("arg has a default value");
            let mut options = LogOptions::new()
                .with_follow(false)
                .with_tail(LogTail::All)
                .with_since(since);
            if let Some(until) = args
                .get_one::<String>("until")
                .map(|s| parse_since(s))
                .transpose()
                .context(Error::BadSinceParameter)?
            {
                options = options.with_until(until);
            }
            let include_ms_only = args.get_flag("include-edge-runtime-only");
            let verbose = !args.get_flag("quiet");
            let iothub_hostname = args
                .get_one::<String>("iothub-hostname")
                .map(ToOwned::to_owned);
            let output_location = if location == std::path::Path::new("-") {
                OutputLocation::Memory
            } else {
                OutputLocation::File(location.clone())
            };

            SupportBundleCommand::new(
                options,
                include_ms_only,
                verbose,
                iothub_hostname,
                output_location,
                runtime()?,
            )
            .execute()
            .await
        }
        ("version", _) => {
            Version::print_version();
            Ok(())
        }
        (command, _) => {
            eprintln!("unknown command: {}", command);
            Ok(())
        }
    }
}
