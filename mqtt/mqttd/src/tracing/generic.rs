use std::io;

use tracing::Level;
use tracing_subscriber::{fmt, EnvFilter};

const BROKER_LOG_LEVEL_ENV: &str = "BROKER_LOG";

pub fn init() {
    let log_level = EnvFilter::try_from_env(BROKER_LOG_LEVEL_ENV)
        .or_else(|_| EnvFilter::try_from_default_env())
        .unwrap_or_else(|_| EnvFilter::new("info"));

    let subscriber = fmt::Subscriber::builder()
        .with_ansi(atty::is(atty::Stream::Stderr))
        .with_max_level(Level::TRACE)
        .with_writer(io::stderr)
        .with_env_filter(log_level)
        .finish();
    let _ = tracing::subscriber::set_global_default(subscriber);
}
