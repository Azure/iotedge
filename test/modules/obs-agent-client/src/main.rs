use futures::stream::Stream;
use futures::StreamExt;
// use opentelemetry::global::shutdown_tracer_provider;
use opentelemetry::sdk::metrics::{selectors, PushController};
// use opentelemetry::trace::TraceError;
use opentelemetry::global;
use opentelemetry::metrics::{self, ObserverResult};
use opentelemetry_otlp::ExporterConfig;
use std::error::Error;
use std::time::Duration;

use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};

use clap::{value_t, App, Arg};
use rand::Rng;

// Skip first immediate tick from tokio, not needed for async_std.
fn delayed_interval(duration: Duration) -> impl Stream<Item = tokio::time::Instant> {
    opentelemetry::util::tokio_interval_stream(duration).skip(1)
}

fn init_meter(period: f64, otlp_endpoint: String) -> metrics::Result<PushController> {
    let export_config = ExporterConfig {
        endpoint: otlp_endpoint.to_string(),
        ..ExporterConfig::default()
    };
    opentelemetry_otlp::new_metrics_pipeline(tokio::spawn, delayed_interval)
        .with_export_config(export_config)
        .with_aggregator_selector(selectors::simple::Selector::Exact)
        .with_period(Duration::from_secs_f64(period))
        .build()
}

#[derive(Debug)]
struct Config {
    update_rate: f64,
    push_rate: f64,
    batching_enabled: bool,
    otlp_endpoint: String,
}

fn init_config() -> Result<Config, Box<dyn Error + Send + Sync + 'static>> {
    let matches = App::new("obs_agent_client")
        .arg(
            Arg::with_name("update-rate")
                .short("u")
                .long("update-rate")
                .help("Rate at which each instrument is updated with a new metric measurement (updates/sec)")
        )
        .arg(
            Arg::with_name("push-rate")
                .short("p")
                .long("push-rate")
                .help("Rate at which measurements are pushed out of the client (pushes/sec)")
        )
        .arg(
            Arg::with_name("batching-enabled")
                .short("b")
                .long("batching-enabled")
                .help("Enables or disables batch recording of measurements.")
        )
        .arg(
            Arg::with_name("otlp-endpoint")
                .short("e")
                .long("otlp-endpoint")
                .help("Endpoint to which OTLP messages will be sent.")
        )
        .get_matches();

    let config = Config {
        update_rate: if let Ok(v) = std::env::var("OBS_AGENT_CLIENT_UPDATE_RATE") {
            v.parse()?
        } else {
            value_t!(matches.value_of("update-rate"), f64).unwrap_or(1.0)
        },
        push_rate: if let Ok(v) = std::env::var("OBS_AGENT_CLIENT_PUSH_RATE") {
            v.parse()?
        } else {
            value_t!(matches.value_of("push-rate"), f64).unwrap_or(0.2)
        },
        batching_enabled: if let Ok(v) = std::env::var("OBS_AGENT_CLIENT_BATCHING_ENABLED") {
            v.parse()?
        } else {
            value_t!(matches.value_of("batching-enabled"), bool).unwrap_or(false)
        },
        otlp_endpoint: if let Ok(v) = std::env::var("OBS_AGENT_OTLP_ENDPOINT") {
            v.parse()?
        } else {
            matches
                .value_of("otlp-endpoint")
                .unwrap_or("http://localhost:4317")
                .to_string()
        },
    };
    println!("Parsed config: {:?}", config);
    Ok(config)
}

#[tokio::main]
async fn main() -> Result<(), Box<dyn Error + Send + Sync + 'static>> {
    let config = init_config()?;
    let _started = init_meter(1.0 / config.push_rate, config.otlp_endpoint)?;
    let meter = global::meter("microsoft.com/azureiot-edge");

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
        let sum_clone = sum_clone.lock().unwrap();
        res.observe(*sum_clone, &[]);
    };
    let _sum_observer = meter
        .u64_sum_observer("u64_sum_observer_example", sum_observer_cb)
        .init();
    let ud_sum = Arc::new(Mutex::new(0));
    let ud_sum_clone = Arc::clone(&ud_sum);
    let ud_sum_observer_cb = move |res: ObserverResult<i64>| {
        let ud_sum_clone = ud_sum_clone.lock().unwrap();
        res.observe(*ud_sum_clone, &[]);
    };
    let _ud_sum_observer = meter
        .i64_up_down_sum_observer("i64_up_down_sum_observer_example", ud_sum_observer_cb)
        .init();
    let value = Arc::new(Mutex::new(0.0));
    let value_clone = Arc::clone(&value);
    let value_observer_cb = move |res: ObserverResult<f64>| {
        let value_clone = value_clone.lock().unwrap();
        res.observe(*value_clone, &[]);
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
        let mut sum = sum.lock().unwrap();
        *sum += 1;
        let mut ud_sum = ud_sum.lock().unwrap();
        *ud_sum += 1;
        let mut value = value.lock().unwrap();
        *value = rng.gen::<f64>();
        tokio::time::sleep(Duration::from_secs_f64(1.0 / config.update_rate)).await;
    }

    Ok(())
}
