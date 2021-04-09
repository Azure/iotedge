use hyper::{
    header::CONTENT_TYPE,
    service::{make_service_fn, service_fn},
    Body, Error, Request, Response, Server,
};

use core::future::Future;
use std::pin::Pin;

use futures::stream::Stream;
use futures::StreamExt;
use opentelemetry::global;
use opentelemetry::metrics as otel_metrics;
use opentelemetry::metrics::{noop::NoopMeterProvider, MetricsError};
use opentelemetry::sdk::metrics::PushController;
use opentelemetry_prometheus::PrometheusExporter;
use prometheus::{Encoder, TextEncoder};
use std::convert::Infallible;
use std::result::Result;
use std::time::Duration;

pub mod metrics;

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

pub fn init_prometheus_metrics_exporter() -> Result<PrometheusExporter, MetricsError> {
    opentelemetry_prometheus::exporter().try_init()
}

pub fn create_prometheus_server(
    prometheus_exporter: &PrometheusExporter,
) -> Pin<Box<dyn Future<Output = Result<(), Error>> + '_>> {
    // For every connection, we must make a `Service` to handle all
    // incoming HTTP requests on said connection.
    let make_svc = make_service_fn(move |_conn| {
        let prom_exporter = prometheus_exporter.clone();
        // This is the `Service` that will handle the connection.
        // `service_fn` is a helper to convert a function that
        // returns a Response into a `Service`.
        async move { Ok::<_, Infallible>(service_fn(move |req| serve_req(req, prom_exporter.clone()))) }
    });

    // TODO: Make this configurable
    let addr = ([0, 0, 0, 0], 9601).into();

    let server_fut = Server::bind(&addr).serve(make_svc);

    Box::pin(server_fut)
}

fn delayed_interval(duration: Duration) -> impl Stream<Item = tokio::time::Instant> {
    tokio::time::interval(duration).skip(1)
}

// TODO: Uncomment and use once we move broker to tokio v1
// pub fn init_otlp_metrics_exporter() -> otel_metrics::Result<PushController> {
//     opentelemetry_otlp metrics exporter only available starting in opentelemetry v0.12.0,
//     which depends on tokio 1.0
//     let export_config = ExporterConfig {
//         endpoint: "http://localhost:4317".to_string(),
//         ..ExporterConfig::default()
//     };
//     opentelemetry_otlp::new_metrics_pipeline(tokio::spawn, delayed_interval)
//         .with_export_config(export_config)
//         .with_aggregator_selector(selectors::simple::Selector::Exact)
//         .build()
// }

pub fn init_stdout_metrics_exporter() -> otel_metrics::Result<PushController> {
    opentelemetry::sdk::export::metrics::stdout(tokio::spawn, delayed_interval)
        .with_pretty_print(true)
        .try_init()
}

pub fn set_noop_meter_provider() {
    let provider = NoopMeterProvider::new();
    global::set_meter_provider(provider);
}
