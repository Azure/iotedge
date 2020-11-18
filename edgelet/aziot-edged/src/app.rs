// Copyright (c) Microsoft. All rights reserved.

use std::ffi::{OsStr, OsString};
use std::path::PathBuf;

use clap::{crate_authors, crate_description, crate_name, App, Arg};
use failure::ResultExt;
use log::info;

#[cfg(feature = "runtime-docker")]
use edgelet_docker::Settings;

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

    app
}

fn init_common(running_as_windows_service: bool) -> Result<Settings, Error> {
    let default_config_file = OsString::from("/etc/aziot/edged/config.yaml");

    let matches = create_app(&default_config_file).get_matches();

    // If running as a Windows service, logging was already initialized by init_win_svc_logging(), so don't do it again.
    if !running_as_windows_service {
        logging::init();
    }

    info!("Starting Azure IoT Edge Module Runtime");
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
