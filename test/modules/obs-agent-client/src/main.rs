use futures::stream::Stream;
use futures::StreamExt;
// use opentelemetry::global::shutdown_tracer_provider;
use opentelemetry::sdk::metrics::{selectors, PushController};
// use opentelemetry::trace::TraceError;
use opentelemetry::{
    // baggage::BaggageExt,
    metrics::{self, ObserverResult},
    // trace::{TraceContextExt, Tracer},
    // Context, Key, KeyValue,
};
use opentelemetry::{global, 
    // sdk::trace as sdktrace
};
use opentelemetry_otlp::ExporterConfig;
use std::error::Error;
use std::time::Duration;

use std::sync::{Arc, Mutex};
use std::sync::atomic::{AtomicBool, Ordering};

use clap::{Arg, App, value_t};
use rand::Rng;

// Skip first immediate tick from tokio, not needed for async_std.
fn delayed_interval(duration: Duration) -> impl Stream<Item = tokio::time::Instant> {
    opentelemetry::util::tokio_interval_stream(duration).skip(1)
}

fn init_meter(period: f64) -> metrics::Result<PushController> {
    let export_config = ExporterConfig {
        endpoint: "http://localhost:4317".to_string(),
        ..ExporterConfig::default()
    };
    opentelemetry_otlp::new_metrics_pipeline(tokio::spawn, delayed_interval)
        .with_export_config(export_config)
        .with_aggregator_selector(selectors::simple::Selector::Exact)
        .with_period(Duration::from_secs_f64(period))
        .build()
}


struct Config {
    metrics_update_period: f64,
    metrics_push_period: f64,
    batch_recording_enabled: bool,
}

fn init_config() -> Result<Config, Box<dyn Error + Send + Sync + 'static>>  {
    let matches = App::new("obs_agent_client")
        .arg(Arg::with_name("metrics-update-period")
            .short("u")
            .long("metrics-update-period")
            .help("Seconds between successive updates of a metric.")
            .default_value("1.0"))
        .arg(Arg::with_name("metrics-push-period")
            .short("p")
            .long("metrics-push-period")
            .help("Seconds between pushing metrics to the collector.")
            .default_value("5.0"))
        .arg(Arg::with_name("batch_recording_enabled")
            .short("b")
            .long("batch-recording-enabled")
            .help("Enables or disables batch recording of measurements.")
            .default_value("false"))
        .get_matches();

    let config = Config {
        metrics_update_period: value_t!(matches.value_of("metrics-update-period"), f64).unwrap_or(1.0),
        metrics_push_period: value_t!(matches.value_of("metrics-push-period"), f64).unwrap_or(5.0),
        batch_recording_enabled: value_t!(matches.value_of("batch-recording-enabled"), bool).unwrap_or(false),
    };
    Ok(config)
}


#[tokio::main]
async fn main() -> Result<(), Box<dyn Error + Send + Sync + 'static>> {

    let config = init_config()?;
    let _started = init_meter(config.metrics_push_period)?;
    let meter = global::meter("microsoft.com/azureiot-edge");

    // Init synchronous instruments
    let counter = meter.u64_counter("u64_counter_example").init();
    let ud_counter = meter.i64_up_down_counter("i64_up_down_counter_example").init();
    let value_recorder = meter.f64_value_recorder("f64_value_recorder_example").init();

    // Init asynchronous instruments
    let sum = Arc::new(Mutex::new(0));    
    let sum_clone = Arc::clone(&sum);    
    let sum_observer_cb = move |res: ObserverResult<u64>| {
        let sum_clone = sum_clone.lock().unwrap();
        res.observe(*sum_clone, &[]);
    };
    let _sum_observer = meter.u64_sum_observer("u64_sum_observer_example", sum_observer_cb).init();
    let ud_sum = Arc::new(Mutex::new(0));
    let ud_sum_clone = Arc::clone(&ud_sum);
    let ud_sum_observer_cb = move |res: ObserverResult<i64>| {
        let ud_sum_clone = ud_sum_clone.lock().unwrap();
        res.observe(*ud_sum_clone, &[]);
    };
    let _ud_sum_observer = meter.i64_up_down_sum_observer("i64_up_down_sum_observer_example", ud_sum_observer_cb).init();

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
        tokio::time::sleep(Duration::from_secs_f64(config.metrics_update_period)).await;
    }

    Ok(())
}
