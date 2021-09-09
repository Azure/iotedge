// Copyright (c) Microsoft. All rights reserved.

use anyhow::Error;
use tracing::{info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

use obsagent_client::config;
#[cfg(feature = "otel")]
use obsagent_client::otel_client;
#[cfg(feature = "prom")]
use obsagent_client::prometheus_endpoint;

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

#[tokio::main]
async fn main() -> Result<(), Error> {
    init_logging();
    let config = config::init_config()?;
    info!(
        "Starting Observability Agent Client with configuration: {:?}",
        config
    );

    #[cfg(feature = "otel")]
    let otel_fut = tokio::spawn(otel_client::run(config.clone()));
    #[cfg(feature = "prom")]
    let prom_fut = tokio::spawn(prometheus_endpoint::run(config.clone()));

    cfg_if::cfg_if! {
        if #[cfg(all(feature="otel", feature = "prom"))] {
            let (otel_result, prom_result) = futures::join!(otel_fut, prom_fut);
            otel_result??;
            prom_result??;
        } else if #[cfg(feature = "otel")] {
            otel_fut.await??;
        } else if #[cfg(feature = "prom")] {
            prom_fut.await??;
        }
    }

    Ok(())
}
