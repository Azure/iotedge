// Copyright (c) Microsoft. All rights reserved.

use std::path::Path;

use clap::{crate_authors, crate_description, crate_name, crate_version, App, Arg};
use log::info;

use crate::{logging, settings, Error, Settings};

pub fn init() -> Result<Settings, Error> {
    logging::init();

    info!("Starting proxy server");

    let matches = create_app().get_matches();
    let config_file = matches.value_of_os("config").map_or_else(
        || {
            let path = Path::new(settings::DEFAULT_SETTINGS_FILEPATH);
            if path.exists() {
                Some(path)
            } else {
                None
            }
        },
        |name| Some(Path::new(name)),
    );

    let settings = Settings::new(config_file)?;

    Ok(settings)
}

#[allow(deprecated)]
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
