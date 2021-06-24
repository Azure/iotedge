// Copyright (c) Microsoft. All rights reserved.

use std::{error::Error, sync::Arc};

use tracing::{info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

use obsagent_client::config;
#[cfg(feature = "otel")]
use obsagent_client::otel_client;
#[cfg(feature = "prom")]
use obsagent_client::prometheus_server;

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn Error + Send + Sync + 'static>> {
    init_logging();
    let config = Arc::new(config::init_config()?);
    info!(
        "Starting Observability Agent Client with configuration: {:?}",
        config
    );

    #[cfg(feature = "otel")]
    let otel_fut = otel_client::run(config.clone());
    #[cfg(feature = "prom")]
    let prom_fut = prometheus_server::run(config.clone());

    cfg_if::cfg_if! {
        if #[cfg(all(feature="otel", feature = "prom"))] {
            let (otel_result, prom_result) = futures::join!(otel_fut, prom_fut);
            otel_result?;
            prom_result?;
        } else if #[cfg(feature = "otel")] {
            otel_fut.await?;
        } else if #[cfg(feature = "prom")] {
            prom_fut.await?;
        }
    }

    Ok(())
}
