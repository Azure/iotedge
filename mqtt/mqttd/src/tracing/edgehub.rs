use std::env;

use tracing::{log::LevelFilter, Level};
use tracing_log::LogTracer;
use tracing_subscriber::{fmt, EnvFilter};

use super::Format;

const EDGE_HUB_LOG_LEVEL_ENV: &str = "RuntimeLogLevel";

const EDGEHUB_2_RUST_LOG_LEVELS: [(&str, &str); 4] = [
    ("verbose", "trace"),
    ("information", "info"),
    ("warning", "warn"),
    ("fatal", "error"),
];

/// Edge Hub log level can be set via `RuntimeLogLevel` env var.
/// The following values are allowed: fatal, error, warning, info, debug, verbose.
///
/// To make it work with rust log levels, we do a simple pre-processing.
/// E.g: this string: `warning,mqtt_broker::broker=debug` becomes `warn,mqtt_broker::broker=debug`.
pub fn init() {
    let mut log_level =
        env::var(EDGE_HUB_LOG_LEVEL_ENV).map_or_else(|_| "info".into(), sanitize_log_level);

    log_level = sanitize_log_level(log_level);

    let subscriber = fmt::Subscriber::builder()
        .with_max_level(Level::TRACE)
        .event_format(Format::default())
        .with_env_filter(EnvFilter::new(log_level.clone()))
        .finish();
    let _ = tracing::subscriber::set_global_default(subscriber);

    let filter = log_level.parse().unwrap_or(LevelFilter::Info);
    let _ = LogTracer::builder().with_max_level(filter).init();
}

// make sure to replace all edgehub-specific log levels to rust-compatible
fn sanitize_log_level(log_level: impl Into<String>) -> String {
    let mut log_level: String = log_level.into().to_lowercase();
    for (key, value) in &EDGEHUB_2_RUST_LOG_LEVELS {
        if log_level.contains(key) {
            log_level = log_level.replace(key, value);
        }
    }
    log_level
}

#[cfg(test)]
mod tests {
    use super::sanitize_log_level;

    #[test]
    fn known_log_level_test() {
        assert_eq!("trace", sanitize_log_level("verbose"));
        assert_eq!("debug", sanitize_log_level("debug"));
        assert_eq!("info", sanitize_log_level("information"));
        assert_eq!("info", sanitize_log_level("info"));
        assert_eq!("warn", sanitize_log_level("warning"));
        assert_eq!("warn", sanitize_log_level("warn"));
        assert_eq!("error", sanitize_log_level("error"));
        assert_eq!("error", sanitize_log_level("fatal"));
    }

    #[test]
    fn unknown_log_level_test() {
        assert_eq!("unknownlevel", sanitize_log_level("unknownlevel"));
    }

    #[test]
    fn log_filter_test() {
        assert_eq!(
            "trace,mqtt_broker::broker=debug",
            sanitize_log_level("verbose,mqtt_broker::broker=debug")
        );
        assert_eq!(
            "warn,mqtt_broker::broker=debug",
            sanitize_log_level("warning,mqtt_broker::broker=debug")
        );
    }
}
