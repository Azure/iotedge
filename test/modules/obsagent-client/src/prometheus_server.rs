use std::{net::AddrParseError, time::Duration};

use hyper::{
    header::CONTENT_TYPE,
    service::{make_service_fn, service_fn},
    Body, Request, Response, Server,
};
use prometheus::{
    self, opts, register_histogram, register_int_counter, register_int_gauge, Encoder, TextEncoder,
};
use rand::random;
use thiserror::Error;
use tokio::time;
use tracing::{error, info};

use crate::config::Config;

#[derive(Debug, Error)]
pub enum PromServerError {
    #[error("could not register Counter: {0:?}")]
    CounterRegisterError(#[source] prometheus::Error),
    #[error("could not register Gauge: {0:?}")]
    GaugeRegisterError(#[source] prometheus::Error),
    #[error("could not register Histogram: {0:?}")]
    HistogramRegisterError(#[source] prometheus::Error),
    #[error("error parsing Prometheus endpoint config value: {0:?}")]
    SocketAddrParseError(#[source] AddrParseError),
    #[error("error running hyper HTTP server: {0:?}")]
    HyperError(#[source] hyper::Error),
}

async fn serve_req(_req: Request<Body>) -> Result<Response<Body>, hyper::Error> {
    let encoder = TextEncoder::new();
    let metric_families = prometheus::gather();
    let mut buffer = vec![];
    encoder.encode(&metric_families, &mut buffer).unwrap();
    let response = Response::builder()
        .status(200)
        .header(CONTENT_TYPE, encoder.format_type())
        .body(Body::from(buffer))
        .unwrap();
    Ok(response)
}

async fn metrics_loop(update_rate: f64) -> Result<(), PromServerError> {
    let counter = register_int_counter!(opts!("u64_counter_example", "Example of a u64 counter."))
        .map_err(PromServerError::CounterRegisterError)?;
    let gauge = register_int_gauge!(opts!("i64_gauge_example", "Example of a i64 gauge."))
        .map_err(PromServerError::GaugeRegisterError)?;
    let histogram = register_histogram!(
        "f64_histogram_example",
        "Example of a f64 histogram.",
        vec![0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9,]
    )
    .map_err(PromServerError::HistogramRegisterError)?;
    loop {
        counter.inc();
        gauge.inc();
        histogram.observe(random::<f64>());
        time::sleep(Duration::from_secs_f64(1.0 / update_rate)).await;
    }
}

pub async fn run(config: Config) -> Result<(), PromServerError> {
    let addr = config
        .prom_config
        .endpoint
        .parse()
        .map_err(PromServerError::SocketAddrParseError)?;
    info!("Listening on http://{}", addr);

    let serve_future = Server::bind(&addr).serve(make_service_fn(|_| async {
        Ok::<_, hyper::Error>(service_fn(serve_req))
    }));

    tokio::spawn(async move { metrics_loop(config.update_rate).await });

    serve_future.await.map_err(PromServerError::HyperError)?;

    Ok(())
}
