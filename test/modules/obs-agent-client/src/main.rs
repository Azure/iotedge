use futures::stream::Stream;
use futures::StreamExt;
// use opentelemetry::global::shutdown_tracer_provider;
use opentelemetry::sdk::metrics::{selectors, PushController};
// use opentelemetry::trace::TraceError;
use opentelemetry::{
    // baggage::BaggageExt,
    metrics,//::{self, ObserverResult},
    // trace::{TraceContextExt, Tracer},
    // Context, Key, KeyValue,
};
use opentelemetry::{global, 
    // sdk::trace as sdktrace
};
use opentelemetry_otlp::ExporterConfig;
use std::error::Error;
use std::time::Duration;

use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};

// Skip first immediate tick from tokio, not needed for async_std.
fn delayed_interval(duration: Duration) -> impl Stream<Item = tokio::time::Instant> {
    opentelemetry::util::tokio_interval_stream(duration).skip(1)
}

fn init_meter() -> metrics::Result<PushController> {
    let export_config = ExporterConfig {
        endpoint: "http://localhost:4317".to_string(),
        ..ExporterConfig::default()
    };
    opentelemetry_otlp::new_metrics_pipeline(tokio::spawn, delayed_interval)
        .with_export_config(export_config)
        .with_aggregator_selector(selectors::simple::Selector::Exact)
        .with_period(Duration::from_secs_f64(0.1))
        .build()
}


/* Parameters:
 *
 * 1. metrics_update_rate (updates-per-second)
 * 2. metrics_export_rate (exports-per-second)
 * 3. enable_batch_recording? - later
 * 4. labels? -later
*/

#[tokio::main]
async fn main() -> Result<(), Box<dyn Error + Send + Sync + 'static>> {
    let _started = init_meter()?;
    let meter = global::meter("microsoft.com/azureiot-edge");
    let counter = meter.f64_counter("f64_counter_example").init();

    let term = Arc::new(AtomicBool::new(false));
    signal_hook::flag::register(signal_hook::consts::SIGTERM, Arc::clone(&term))?;
    while !term.load(Ordering::Relaxed) {
        counter.add(1.0, &[]);
        tokio::time::sleep(Duration::from_millis(10)).await;
    }

    Ok(())
}
