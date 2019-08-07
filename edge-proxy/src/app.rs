// Copyright (c) Microsoft. All rights reserved.

use std::path::Path;

use clap::{crate_authors, crate_description, crate_name, crate_version, App, Arg};
use log::info;

use crate::{logging, Error, Settings};

pub fn init() -> Result<Settings, Error> {
    logging::init();

    info!("Starting proxy server");

    let matches = create_app().get_matches();
    let config_file = matches
        .value_of_os("config")
        .and_then(|name| {
            let path = Path::new(name);
            info!("Using config file: {}", path.display());
            Some(path)
        })
        .or_else(|| {
            info!("Using default configuration");
            None
        });

    let settings = Settings::new(config_file)?;

    Ok(settings)
}

fn create_app() -> App<'static, 'static> {
    App::new(crate_name!())
        .author(crate_authors!("\n"))
        .version(crate_version!())
        .about(crate_description!())
        .arg(
            Arg::with_name("config")
                .short("c")
                .long("config")
                .value_name("FILE")
                .help("Sets proxy configuration file")
                .takes_value(true),
        )
}
