// Copyright (c) Microsoft. All rights reserved.

use std::ffi::{OsStr, OsString};
use std::path::PathBuf;

use clap::{crate_authors, crate_description, crate_name, App, Arg};
use failure::ResultExt;
use log::info;

use edgelet_core;
#[cfg(feature = "runtime-docker")]
use edgelet_docker::Settings;
#[cfg(feature = "runtime-kubernetes")]
use edgelet_kube::Settings;

use crate::error::{Error, ErrorKind, InitializeErrorReason};
use crate::logging;

#[allow(deprecated)]
fn create_app(default_config_file: &OsStr) -> App<'_, '_> {
    let app = App::new(crate_name!())
        .version(edgelet_core::version_with_source_version())
        .author(crate_authors!("\n"))
        .about(crate_description!())
        .arg(
            Arg::with_name("config-file")
                .short("c")
                .long("config-file")
                .value_name("FILE")
                .help("Sets daemon configuration file")
                .takes_value(true)
                .default_value_os(default_config_file),
        );

    if cfg!(windows) {
        app.arg(
            Arg::with_name("use-event-logger")
                .short("e")
                .long("use-event-logger")
                .value_name("USE_EVENT_LOGGER")
                .help("Log to Windows event logger instead of stdout")
                .required(false)
                .takes_value(false),
        )
    } else {
        app
    }
}

fn init_common(running_as_windows_service: bool) -> Result<Settings, Error> {
    let default_config_file = if cfg!(windows) {
        let program_data: PathBuf =
            std::env::var_os("PROGRAMDATA").map_or_else(|| r"C:\ProgramData".into(), Into::into);

        let mut default_config_file = program_data;
        default_config_file.push("iotedge");
        default_config_file.push("config.yaml");
        default_config_file.into()
    } else {
        OsString::from("/etc/iotedge/config.yaml")
    };

    let matches = create_app(&default_config_file).get_matches();

    // If running as a Windows service, logging was already initialized by init_win_svc_logging(), so don't do it again.
    if !running_as_windows_service {
        if cfg!(windows) && matches.is_present("use-event-logger") {
            #[cfg(windows)]
            logging::init_win_log();
        } else {
            logging::init();
        }
    }

    if cfg!(feature = "runtime-kubernetes") {
        info!("Starting Azure IoT Edge Security Daemon - Kubernetes mode");
    } else {
        info!("Starting Azure IoT Edge Security Daemon");
    };
    info!("Version - {}", edgelet_core::version_with_source_version());

    let config_file: PathBuf = matches
        .value_of_os("config-file")
        .expect("arg has a default value")
        .to_os_string()
        .into();

    info!("Using config file: {}", config_file.display());

    let settings = Settings::new(&config_file)
        .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;

    Ok(settings)
}

pub fn init() -> Result<Settings, Error> {
    init_common(false)
}

#[cfg(windows)]
pub fn init_win_svc() -> Result<Settings, Error> {
    init_common(true)
}

#[cfg(windows)]
pub fn init_win_svc_logging() {
    logging::init_win_log();
}
