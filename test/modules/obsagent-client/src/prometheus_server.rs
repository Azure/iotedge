use std::{error::Error, sync::Arc, time::Duration};

use hyper::{
    header::CONTENT_TYPE,
    service::{make_service_fn, service_fn},
    Body, Request, Response, Server,
};
use prometheus::{
    opts, register_histogram, register_int_counter, register_int_gauge, Encoder, TextEncoder,
};
use rand::random;
use tracing::{error, info};

use crate::config::Config;

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

async fn metrics_loop(update_rate: f64) -> Result<(), Box<dyn Error + Send + Sync + 'static>> {
    let counter = register_int_counter!(opts!("u64_counter_example", "Example of a u64 counter."))?;
    let gauge = register_int_gauge!(opts!("i64_gauge_example", "Example of a i64 gauge."))?;
    let histogram = register_histogram!(
        "f64_histogram_example",
        "Example of a f64 histogram.",
        vec![0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9,]
    )?;
    loop {
        counter.inc();
        gauge.inc();
        histogram.observe(random::<f64>());
        tokio::time::sleep(Duration::from_secs_f64(1.0 / update_rate)).await;
    }
}

pub async fn run(config: Arc<Config>) -> Result<(), Box<dyn Error + Send + Sync + 'static>> {
    let addr = config.prom_config.endpoint.parse()?;
    info!("Listening on http://{}", addr);

    let serve_future = Server::bind(&addr).serve(make_service_fn(|_| async {
        Ok::<_, hyper::Error>(service_fn(serve_req))
    }));

    tokio::spawn(async move { metrics_loop(config.update_rate).await });

    if let Err(err) = serve_future.await {
        error!("server error: {}", err);
    }

    Ok(())
}
