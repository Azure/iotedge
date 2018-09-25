// Copyright (c) Microsoft. All rights reserved.

use clap::{App, Arg, ArgMatches};
use edgelet_core;
use edgelet_docker::DockerConfig;

use error::Error;
use logging;
use settings::Settings;

pub fn create_base_app<'a, 'b>() -> App<'a, 'b> {
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

pub fn log_banner() {
    info!("Starting Azure IoT Edge Security Daemon");
    info!("Version - {}", edgelet_core::version());
}

pub fn init_common<'a>() -> Result<(Settings<DockerConfig>, ArgMatches<'a>), Error> {
    let matches = create_app().get_matches();
    let settings = {
        let config_file = matches
            .value_of("config-file")
            .and_then(|name| {
                info!("Using config file: {}", name);
                Some(name)
            }).or_else(|| {
                info!("Using default configuration");
                None
            });

        Settings::<DockerConfig>::new(config_file)?
    };

    Ok((settings, matches))
}

#[cfg(target_os = "windows")]
pub fn init() -> Result<Settings<DockerConfig>, Error> {
    let (settings, matches) = init_common()?;

    if matches.is_present("use-event-logger") {
        logging::init_win_log();
    } else {
        logging::init();
    }

    log_banner();

    Ok(settings)
}

#[cfg(not(target_os = "windows"))]
pub fn init() -> Result<Settings<DockerConfig>, Error> {
    logging::init();
    log_banner();
    init_common().map(|(settings, _)| settings)
}

#[cfg(target_os = "windows")]
pub fn init_win_svc() -> Result<Settings<DockerConfig>, Error> {
    logging::init_win_log();
    log_banner();
    init_common().map(|(settings, _)| settings)
}
