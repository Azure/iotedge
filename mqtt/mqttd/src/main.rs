#![type_length_limit = "1230974"]
use std::{env, path::PathBuf};

use anyhow::Result;
use clap::{crate_description, crate_name, crate_version, App, Arg};

use mqttd::{app, tracing};

// TODO 1: Move to a separate module or file
use futures::stream::Stream;
use futures::StreamExt;
use opentelemetry::metrics::{self, MetricsError};
use opentelemetry::sdk::metrics::PushController;
// use opentelemetry_otlp::ExporterConfig;
use hyper::{
    header::CONTENT_TYPE,
    service::{make_service_fn, service_fn},
    Body, Request, Response, Server,
};
use opentelemetry_prometheus::PrometheusExporter;
use prometheus::{Encoder, TextEncoder};
use std::convert::Infallible;
use std::time::Duration;

async fn serve_req(
    _req: Request<Body>,
    prom_exporter: PrometheusExporter,
) -> Result<Response<Body>, hyper::Error> {
    let mut buffer = vec![];
    let encoder = TextEncoder::new();
    let metric_families = prom_exporter.registry().gather();
    encoder.encode(&metric_families, &mut buffer).unwrap();

    let response = Response::builder()
        .status(200)
        .header(CONTENT_TYPE, encoder.format_type())
        .body(Body::from(buffer))
        .unwrap();

    Ok(response)
}

fn delayed_interval(duration: Duration) -> impl Stream<Item = tokio::time::Instant> {
    // opentelemetry::util::tokio_interval_stream(duration).skip(1)
    tokio::time::interval(duration).skip(1)
}

fn init_prometheus_metrics_exporter() -> Result<PrometheusExporter, MetricsError> {
    opentelemetry_prometheus::exporter().try_init()
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
    let prom_exporter = init_prometheus_metrics_exporter()?;

    let config_path = create_app()
        .get_matches()
        .value_of("config")
        .map(PathBuf::from);

    let mut app = app::new();
    if let Some(config_path) = config_path {
        app.setup(config_path)?;
    }

    let app_fut = app.run();

    // For every connection, we must make a `Service` to handle all
    // incoming HTTP requests on said connection.
    let make_svc = make_service_fn(move |_conn| {
        let prom_exporter = prom_exporter.clone();
        // This is the `Service` that will handle the connection.
        // `service_fn` is a helper to convert a function that
        // returns a Response into a `Service`.
        async move { Ok::<_, Infallible>(service_fn(move |req| serve_req(req, prom_exporter.clone()))) }
    });

    let addr = ([127, 0, 0, 1], 3000).into();

    let server_fut = Server::bind(&addr).serve(make_svc);

    println!("Listening on http://{}", addr);

    let (_server_res, _app_res) = futures::join!(server_fut, app_fut);

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
