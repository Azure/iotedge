use std::{env, path::PathBuf};

use anyhow::Result;
use clap::{crate_description, crate_name, crate_version, App, Arg};

use mqttd::{broker, tracing};

#[tokio::main]
async fn main() -> Result<()> {
    tracing::init();

    let config_path = create_app()
        .get_matches()
        .value_of("config")
        .map(PathBuf::from);

    broker::run(config_path).await?;
    Ok(())
}

fn create_app() -> App<'static, 'static> {
    App::new(crate_name!())
        .version(crate_version!())
        .about(crate_description!())
        .arg(
            Arg::with_name("config")
                .short("c")
                .long("config")
                .value_name("FILE")
                .help("Sets a custom config file")
                .takes_value(true),
        )
}
