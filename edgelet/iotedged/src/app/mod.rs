// Copyright (c) Microsoft. All rights reserved.

use std::fs;
use std::str::FromStr;

use clap::{App, Arg, ArgMatches};
use edgelet_core;
use edgelet_docker::Settings as DockerSettings;
use failure::ResultExt;

use error::{Error, ErrorKind, InitializeErrorReason};

#[cfg(feature = "runtime-docker")]
mod docker;

#[cfg(feature = "runtime-docker")]
pub use self::docker::init;

#[cfg(target_os = "windows")]
#[cfg(feature = "runtime-docker")]
pub use self::docker::init_win_svc;

#[cfg(feature = "runtime-kubernetes")]
mod kubernetes;

#[cfg(feature = "runtime-kubernetes")]
pub use self::kubernetes::init;

#[cfg(unix)]
static DEFAULTS: &str = include_str!("config/unix/default.yaml");

#[cfg(windows)]
static DEFAULTS: &str = include_str!("config/windows/default.yaml");

pub fn init_common<'a>() -> Result<(DockerSettings, ArgMatches<'a>), Error> {
    let matches = create_app().get_matches();
    let settings = {
        let config_str = matches
            .value_of("config-file")
            .map(|name| {
                info!("Using config file: {}", name);
                fs::read_to_string(name)
            })
            .unwrap_or_else(|| {
                info!("Using default configuration");
                Ok(DEFAULTS.to_string())
            })
            .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;

        DockerSettings::from_str(&config_str)
            .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?
    };

    Ok((settings, matches))
}

fn create_base_app<'a, 'b>() -> App<'a, 'b> {
    App::new(crate_name!())
        .version(crate_version!())
        .author(crate_authors!("\n"))
        .about(crate_description!())
        .arg(
            Arg::with_name("config-file")
                .short("c")
                .long("config-file")
                .value_name("FILE")
                .help("Sets daemon configuration file")
                .takes_value(true),
        )
}

#[cfg(not(target_os = "windows"))]
pub fn create_app<'a, 'b>() -> App<'a, 'b> {
    create_base_app()
}

#[cfg(target_os = "windows")]
pub fn create_app<'a, 'b>() -> App<'a, 'b> {
    create_base_app().arg(
        Arg::with_name("use-event-logger")
            .short("e")
            .long("use-event-logger")
            .value_name("USE_EVENT_LOGGER")
            .help("Log to Windows event logger instead of stdout")
            .required(false)
            .takes_value(false),
    )
}

#[cfg(feature = "runtime-docker")]
pub fn log_banner() {
    info!("Starting Azure IoT Edge Security Daemon");
    info!("Version - {}", edgelet_core::version());
}

#[cfg(feature = "runtime-kubernetes")]
pub fn log_banner() {
    info!("Starting Azure IoT Edge Security Daemon (Kubernetes)");
    info!("Version - {}", edgelet_core::version());
}
