// Copyright (c) Microsoft. All rights reserved.

use clap::{App, Arg};
use edgelet_docker::DockerConfig;

use error::Error;
use logging;
use settings::Settings;

pub fn init() -> Result<Settings<DockerConfig>, Error> {
    logging::init();

    let matches = App::new(crate_name!())
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
        .get_matches();

    let config_file = matches
        .value_of("config-file")
        .and_then(|name| {
            info!("Using config file: {}", name);
            Some(name)
        })
        .or_else(|| {
            info!("Using default configuration");
            None
        });

    let settings = Settings::<DockerConfig>::new(config_file)?;
    Ok(settings)
}
