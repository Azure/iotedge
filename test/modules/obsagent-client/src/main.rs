// Copyright (c) Microsoft. All rights reserved.

//! This program sends example metrics to a configurable OTLP endpoint. The program
//! loops, updating and exporting metrics for one example of each OTel instrument type (i.e,
//! counter, up-down-counter, value-recorder, sum-observer, up-down-sum-observer,
//! value-observer).  The rate at which the metrics are updated is configurable. The
//! rate at which metrics are exported (or pushed) to the OTLP endpoint is also configurable.
//!
//! The program is configurable via command line arguments as well as environment variables,
//! with the environment variables taking precendence. The following is a list of the environment
//! variables (and cmd line args in parenthesis):
//!     1. UPDATE_RATE (--update-rate/-u) - Rate at which each instrument is
//!            updated with a new metric measurement (updates/sec)
//!     2. PUSH_RATE (--push-rate/-p) - Rate at which measurements are pushed
//!            out of the client (pushes/sec)
//!     3. OTLP_ENDPOINT (--otlp-endpoint/-e) - Endpoint to which OTLP messages
//!             will be sent.

use std::error::Error;

use tracing::{info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

use obsagent_client::{config, otel_client};

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn Error + Send + Sync + 'static>> {
    init_logging();
    let config = config::init_config()?;
    info!(
        "Starting Observability Agent Client with configuration: {:?}",
        config
    );

    otel_client::run(config).await
}
