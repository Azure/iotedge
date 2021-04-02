#![type_length_limit = "1230974"]
use std::{env, path::PathBuf};

use anyhow::Result;
use clap::{crate_description, crate_name, crate_version, App, Arg};

use mqttd::{app, tracing};

#[tokio::main]
async fn main() -> Result<()> {
    tracing::init();
    let _metrics_pc = mqtt_otel::init_stdout_metrics_exporter();
    let prom_exporter = mqtt_otel::init_prometheus_metrics_exporter()?;
    let prom_server_fut = mqtt_otel::create_prometheus_server(&prom_exporter);

    let config_path = create_app()
        .get_matches()
        .value_of("config")
        .map(PathBuf::from);

    let mut app = app::new();
    if let Some(config_path) = config_path {
        app.setup(config_path)?;
    }

    let app_fut = app.run();

    tokio::select! {
        _ = prom_server_fut => Ok(()),
        _ = app_fut => Ok(())
    }
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
