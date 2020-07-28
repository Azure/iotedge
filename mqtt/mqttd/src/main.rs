use std::{env, io, path::PathBuf};

use anyhow::Result;
use clap::{crate_description, crate_name, crate_version, App, Arg};
use tracing::Level;
use tracing_subscriber::{fmt, EnvFilter};

use mqttd::broker;

const BROKER_LOG_LEVEL_ENV: &str = "BROKER_LOG";
const HUB_LOG_LEVEL_ENV: &str = "RuntimeLogLevel";

#[tokio::main]
async fn main() -> Result<()> {
    let mut log_level = EnvFilter::try_from_env(BROKER_LOG_LEVEL_ENV);

    if let Err(_) = log_level {
        log_level = EnvFilter::try_from_env(HUB_LOG_LEVEL_ENV)
    };

    let log_level = match log_level {
        Ok(log_level) => log_level,
        Err(_) => EnvFilter::new("INFO"),
    };

    let subscriber = fmt::Subscriber::builder()
        .with_ansi(atty::is(atty::Stream::Stderr))
        .with_max_level(Level::TRACE)
        .with_writer(io::stderr)
        .with_env_filter(log_level)
        .finish();
    let _ = tracing::subscriber::set_global_default(subscriber);

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
