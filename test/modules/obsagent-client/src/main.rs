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

use std::{
    error::Error,
    sync::{
        atomic::{AtomicBool, Ordering},
        Arc, Mutex,
    },
    time::Duration,
};

use clap::{value_t, App, Arg};
use futures::{Stream, StreamExt};
use opentelemetry::{
    global,
    metrics::{self, ObserverResult},
    sdk::metrics::{selectors, PushController},
};
use opentelemetry_otlp::ExporterConfig;
use rand::Rng;
use tracing::{error, info, subscriber, Level};
use tracing_subscriber::fmt::Subscriber;

const METER_NAME: &str = "microsoft.com/azureiot-edge";

// Skip first immediate tick from tokio, not needed for async_std.
fn delayed_interval(duration: Duration) -> impl Stream<Item = tokio::time::Instant> {
    opentelemetry::util::tokio_interval_stream(duration).skip(1)
}

fn init_meter(period: f64, otlp_endpoint: String) -> metrics::Result<PushController> {
    let export_config = ExporterConfig {
        endpoint: otlp_endpoint,
        ..ExporterConfig::default()
    };
    opentelemetry_otlp::new_metrics_pipeline(tokio::spawn, delayed_interval)
        .with_export_config(export_config)
        .with_aggregator_selector(selectors::simple::Selector::Histogram(vec![
            0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9,
        ]))
        .with_period(Duration::from_secs_f64(period))
        .build()
}

#[derive(Debug)]
struct Config {
    update_rate: f64,
    push_rate: f64,
    otlp_endpoint: String,
}

fn init_config() -> Result<Config, Box<dyn Error + Send + Sync + 'static>> {
    let matches = App::new("obs_agent_client")
        .arg(
            Arg::with_name("update-rate")
                .short("u")       
                .long("update-rate")
                .takes_value(true)
                .help("Rate at which each instrument is updated with a new metric measurement (updates/sec)")
        )
        .arg(
            Arg::with_name("push-rate")
                .short("p")
                .long("push-rate")
                .takes_value(true)
                .help("Rate at which measurements are pushed out of the client (pushes/sec)")
        )
        .arg(
            Arg::with_name("otlp-endpoint")
                .short("e")
                .long("otlp-endpoint")
                .takes_value(true)
                .help("Endpoint to which OTLP messages will be sent.")
        )
        .get_matches();

    let config = Config {
        update_rate: std::env::var("UPDATE_RATE").map_or_else(
            |_e| Ok(value_t!(matches.value_of("update-rate"), f64).unwrap_or(1.0)),
            |v| v.parse(),
        )?,
        push_rate: std::env::var("PUSH_RATE").map_or_else(
            |_e| Ok(value_t!(matches.value_of("push-rate"), f64).unwrap_or(0.2)),
            |v| v.parse(),
        )?,
        otlp_endpoint: std::env::var("OTLP_ENDPOINT").map_or_else(
            |_e| {
                Ok(matches
                    .value_of("otlp-endpoint")
                    .unwrap_or("http://localhost:4317")
                    .to_string())
            },
            |v| v.parse(),
        )?,
    };
    Ok(config)
}

fn init_logging() {
    let subscriber = Subscriber::builder().with_max_level(Level::INFO).finish();
    let _ = subscriber::set_global_default(subscriber);
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn Error + Send + Sync + 'static>> {
    init_logging();
    let config = init_config()?;
    info!(
        "Starting Observability Agent Client with configuration: {:?}",
        config
    );
    let _started = init_meter(1.0 / config.push_rate, config.otlp_endpoint)?;
    let meter = global::meter(METER_NAME);

    // Init synchronous instruments
    let counter = meter.u64_counter("u64_counter_example").init();
    let ud_counter = meter
        .i64_up_down_counter("i64_up_down_counter_example")
        .init();
    let value_recorder = meter
        .f64_value_recorder("f64_value_recorder_example")
        .init();

    // Init asynchronous instruments
    let sum = Arc::new(Mutex::new(0));
    let sum_clone = Arc::clone(&sum);
    let sum_observer_cb = move |res: ObserverResult<u64>| {
        if let Ok(sum_clone) = sum_clone.try_lock() {
            res.observe(*sum_clone, &[]);
        } else {
            error!("try_lock failed")
        }
    };
    let _sum_observer = meter
        .u64_sum_observer("u64_sum_observer_example", sum_observer_cb)
        .init();
    let ud_sum = Arc::new(Mutex::new(0));
    let ud_sum_clone = Arc::clone(&ud_sum);
    let ud_sum_observer_cb = move |res: ObserverResult<i64>| {
        if let Ok(ud_sum_clone) = ud_sum_clone.try_lock() {
            res.observe(*ud_sum_clone, &[]);
        } else {
            error!("try_lock failed")
        }
    };
    let _ud_sum_observer = meter
        .i64_up_down_sum_observer("i64_up_down_sum_observer_example", ud_sum_observer_cb)
        .init();
    let value: Arc<Mutex<f64>> = Arc::new(Mutex::new(0.0));
    let value_clone = Arc::clone(&value);
    let value_observer_cb = move |res: ObserverResult<f64>| {
        if let Ok(value_clone) = value_clone.try_lock() {
            res.observe(*value_clone, &[]);
        } else {
            error!("try_lock failed")
        }
    };
    let _value_observer = meter
        .f64_value_observer("f64_value_observer_example", value_observer_cb)
        .init();

    // Loop, updating each metric value at the configured update rate
    let mut rng = rand::thread_rng();
    let term = Arc::new(AtomicBool::new(false));
    signal_hook::flag::register(signal_hook::consts::SIGTERM, Arc::clone(&term))?;
    while !term.load(Ordering::Relaxed) {
        counter.add(1, &[]);
        ud_counter.add(1, &[]);
        value_recorder.record(rng.gen::<f64>(), &[]);
        {
            if let Ok(mut sum) = sum.try_lock() {
                *sum += 1;
            } else {
                error!("try_lock failed");
            }
        }
        {
            if let Ok(mut ud_sum) = ud_sum.try_lock() {
                *ud_sum += 1;
            } else {
                error!("try_lock failed");
            }
        }
        {
            if let Ok(mut value) = value.try_lock() {
                *value = rng.gen::<f64>();
            } else {
                error!("try_lock failed");
            }
        }
        tokio::time::sleep(Duration::from_secs_f64(1.0 / config.update_rate)).await;
    }

    Ok(())
}
