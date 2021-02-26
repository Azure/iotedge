#![type_length_limit = "1230974"]
use std::{env, path::PathBuf};

use anyhow::Result;
use clap::{crate_description, crate_name, crate_version, App, Arg};

use mqttd::{app, tracing};

// TODO 1: Move to a separate module or file
use futures::stream::Stream;
use futures::StreamExt;
use opentelemetry::metrics::{self, MetricsError};
use opentelemetry::sdk::metrics::{selectors, PushController};
use opentelemetry_otlp::ExporterConfig;
use std::time::Duration;

fn delayed_interval(duration: Duration) -> impl Stream<Item = tokio::time::Instant> {
    // opentelemetry::util::tokio_interval_stream(duration).skip(1)
    tokio::time::interval(duration).skip(1)
}

fn init_otlp_metrics_exporter() -> metrics::Result<PushController> {
    // TODO 2: Uncomment and use once we move broker to tokio 1.0
    // opentelemetry_otlp metrics exporter only available starting in opentelemetry v0.12.0,
    // which depends on tokio 1.0
    // let export_config = ExporterConfig {
    //     endpoint: "http://localhost:4317".to_string(),
    //     ..ExporterConfig::default()
    // };
    // opentelemetry_otlp::new_metrics_pipeline(tokio::spawn, delayed_interval)
    //     .with_export_config(export_config)
    //     .with_aggregator_selector(selectors::simple::Selector::Exact)
    //     .build()
    // end TODO 2

    // TODO 3: Remove after moving to tokio 1.0, replacing with above TODO section
    opentelemetry::sdk::export::metrics::stdout(tokio::spawn, delayed_interval)
        .with_quantiles(vec![0.5, 0.9, 0.99])
        .with_formatter(|batch| {
            serde_json::to_value(batch)
                .map(|value| value.to_string())
                .map_err(|err| MetricsError::Other(err.to_string()))
        })
        .try_init()
    // end TODO 3
}
// end TODO 1

#[tokio::main]
async fn main() -> Result<()> {
    tracing::init();

    init_otlp_metrics_exporter()?;

    let config_path = create_app()
        .get_matches()
        .value_of("config")
        .map(PathBuf::from);

    let mut app = app::new();
    if let Some(config_path) = config_path {
        app.setup(config_path)?;
    }

    app.run().await?;

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
