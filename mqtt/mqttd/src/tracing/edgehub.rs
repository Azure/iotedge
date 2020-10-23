use std::env;

use tracing::{log::LevelFilter, Level};
use tracing_log::LogTracer;
use tracing_subscriber::{fmt, EnvFilter};

use super::Format;

const BROKER_LOG_LEVEL_ENV: &str = "BROKER_LOG";

const EDGE_HUB_LOG_LEVEL_ENV: &str = "RuntimeLogLevel";

pub fn init() {
    let log_level = env::var(BROKER_LOG_LEVEL_ENV)
        .or_else(|_| env::var(EDGE_HUB_LOG_LEVEL_ENV))
        .or_else(|_| env::var(EnvFilter::DEFAULT_ENV))
        .map_or_else(|_| "info".into(), |level| level.to_lowercase());

    let subscriber = fmt::Subscriber::builder()
        .with_max_level(Level::TRACE)
        .on_event(Format::default())
        .with_env_filter(EnvFilter::new(log_level.clone()))
        .finish();
    let _ = tracing::subscriber::set_global_default(subscriber);

    let filter = log_level.parse().unwrap_or(LevelFilter::Info);
    let _ = LogTracer::builder().with_max_level(filter).init();
}
