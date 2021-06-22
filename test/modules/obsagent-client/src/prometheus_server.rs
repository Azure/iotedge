use std::{
    error::Error,
    time::Duration,
};

use hyper::{
    header::CONTENT_TYPE,
    service::{make_service_fn, service_fn},
    Body, Request, Response, Server,
};
use prometheus::{
    Encoder,
    opts, register_int_counter,
    TextEncoder,

};

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

async fn metrics_loop() -> Result<(), Box<dyn Error + Send + Sync + 'static>> {
    let counter = register_int_counter!(opts!("u64_counter_example", "Example of a u64 counter."))?;
    loop {
        counter.inc();
        // TODO: Replace '10000' w/ config.update_rate
        tokio::time::sleep(Duration::from_secs_f64(1.0/10000.0f64)).await;
    }
}

pub async fn run() -> Result<(), Box<dyn Error + Send + Sync + 'static>> {
    let addr = ([127, 0, 0, 1], 9600).into();
    println!("Listening on http://{}", addr);

    let serve_future = Server::bind(&addr).serve(make_service_fn(|_| async {
        Ok::<_, hyper::Error>(service_fn(serve_req))
    }));

    tokio::spawn(async { metrics_loop().await });

    if let Err(err) = serve_future.await {
        eprintln!("server error: {}", err);
    }

    Ok(())
}
