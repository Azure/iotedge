use std::{num::ParseFloatError, string::ParseError, time::Duration};

use clap::{value_t, App, Arg};
use thiserror::Error;

#[derive(Debug, Clone)]
pub struct Config {
    pub update_period: Duration,
    pub prom_config: PromConfig,
    pub otel_config: OTelConfig,
}

#[derive(Debug, Clone)]
pub struct OTelConfig {
    pub push_period: Duration,
    pub otlp_endpoint: String,
}

#[derive(Debug, Clone)]
pub struct PromConfig {
    pub endpoint: String,
}

#[derive(Debug, Error)]
pub enum ConfigError {
    #[error("error parsing command line argument into string: {0:?}")]
    StringArgParseError(#[source] ParseError),
    #[error("error parsing command line argument into float: {0:?}")]
    FloatArgParseError(#[source] ParseFloatError),
    #[error("invalid (negative) configuration parameter provided: {0:?}")]
    InvalidNegativeArgVal(f64),
    #[error("invalid (zero) configuration parameter provided: {0:?}")]
    InvalidZeroArgVal(f64),
    #[error("invalid (infinite) configuration parameter provided: {0:?}")]
    InvalidInfiniteArgVal(f64),
}

impl Config {
    fn new(
        update_period: f64,
        prom_config: PromConfig,
        otel_config: OTelConfig,
    ) -> Result<Config, ConfigError> {
        if update_period < 0.0 {
            return Err(ConfigError::InvalidNegativeArgVal(update_period));
        } else if update_period == 0.0 {
            return Err(ConfigError::InvalidZeroArgVal(update_period));
        } else if update_period == f64::INFINITY {
            return Err(ConfigError::InvalidInfiniteArgVal(update_period));
        }

        Ok(Config {
            update_period: Duration::from_secs_f64(update_period),
            prom_config,
            otel_config,
        })
    }
}
impl OTelConfig {
    fn new(push_period: f64, otlp_endpoint: String) -> Result<OTelConfig, ConfigError> {
        if push_period < 0.0 {
            return Err(ConfigError::InvalidNegativeArgVal(push_period));
        } else if push_period == 0.0 {
            return Err(ConfigError::InvalidZeroArgVal(push_period));
        } else if push_period == f64::INFINITY {
            return Err(ConfigError::InvalidInfiniteArgVal(push_period));
        }

        Ok(OTelConfig {
            push_period: Duration::from_secs_f64(push_period),
            otlp_endpoint,
        })
    }
}

pub fn init_config() -> Result<Config, ConfigError> {
    let default_update_period = "1.0";
    let default_push_period = "5.0";
    let matches = App::new("obs_agent_client")
        .arg(
            Arg::with_name("update-period")
                .short("u")       
                .long("update-period")
                .default_value(default_update_period)
                .help("Period in seconds betweeen successive updates of each instrument with a new metric measurement.")
        )
        .arg(
            Arg::with_name("push-period")
                .short("p")
                .long("push-period")
                .default_value(default_push_period)
                .help("Period in seconds between successive pushes of  measurements  out of the client.")
        )
        .arg(
            Arg::with_name("otlp-endpoint")
                .short("e")
                .long("otlp-endpoint")
                .takes_value(true)
                .help("Endpoint to which OTLP messages will be sent.")
        )
        .get_matches();

    let push_period = std::env::var("PUSH_PERIOD")
        .map_or_else(
            |_e| {
                Ok(value_t!(matches.value_of("push-period"), f64)
                    .unwrap_or(default_push_period.parse()?))
            },
            |v| v.parse(),
        )
        .map_err(ConfigError::FloatArgParseError)?;
    let otlp_endpoint = std::env::var("OTLP_ENDPOINT")
        .map_or_else(
            |_e| {
                Ok(matches
                    .value_of("otlp-endpoint")
                    .unwrap_or("http://localhost:4317")
                    .to_string())
            },
            |v| v.parse(),
        )
        .map_err(ConfigError::StringArgParseError)?;
    let otel_config = OTelConfig::new(push_period, otlp_endpoint)?;

    let prom_config = PromConfig {
        endpoint: std::env::var("PROMETHEUS_ENDPOINT")
            .map_or_else(
                |_e| {
                    Ok(matches
                        .value_of("prometheus-endpoint")
                        .unwrap_or("127.0.0.1:9600")
                        .to_string())
                },
                |v| v.parse(),
            )
            .map_err(ConfigError::StringArgParseError)?,
    };

    let update_period = std::env::var("UPDATE_PERIOD")
        .map_or_else(
            |_e| {
                Ok(value_t!(matches.value_of("update-period"), f64)
                    .unwrap_or(default_update_period.parse()?))
            },
            |v| v.parse(),
        )
        .map_err(ConfigError::FloatArgParseError)?;
    let config = Config::new(update_period, prom_config, otel_config)?;
    Ok(config)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn create_config(
        prom_endpoint: &str,
        otlp_endpoint: &str,
        push_period: f64,
        update_period: f64,
    ) -> Result<Config, ConfigError> {
        let prom_config = PromConfig {
            endpoint: String::from(prom_endpoint),
        };
        let otel_config = OTelConfig::new(push_period, String::from(otlp_endpoint)).unwrap();
        Config::new(update_period, prom_config, otel_config)
    }

    #[test]
    fn test_invalid_zero_push_period() {
        let otel_config = OTelConfig::new(0.0, String::from("localhost:4317"));
        assert!(otel_config.is_err());
    }

    #[test]
    fn test_invalid_negative_push_period() {
        let otel_config = OTelConfig::new(-1.0, String::from("localhost:4317"));
        assert!(otel_config.is_err());
    }

    #[test]
    fn test_invalid_infinite_push_period() {
        let otel_config = OTelConfig::new(f64::INFINITY, String::from("localhost:4317"));
        assert!(otel_config.is_err());
    }

    #[test]
    fn test_epsilon_push_period() {
        let otel_config = OTelConfig::new(f64::EPSILON, String::from("localhost:4317"));
        assert!(otel_config.is_ok());
    }

    #[test]
    fn test_invalid_zero_update_period() {
        let config = create_config("http://localhost:4317", "http://localhost:9600", 1.0, 0.0);
        assert!(config.is_err());
    }

    #[test]
    fn test_invalid_negative_update_period() {
        let config = create_config("http://localhost:4317", "http://localhost:9600", 1.0, -1.0);
        assert!(config.is_err());
    }

    #[test]
    fn test_invalid_infinite_update_period() {
        let config = create_config(
            "http://localhost:4317",
            "http://localhost:9600",
            1.0,
            f64::INFINITY,
        );
        assert!(config.is_err());
    }

    #[test]
    fn test_epsilon_update_period() {
        let config = create_config(
            "http://localhost:4317",
            "http://localhost:9600",
            1.0,
            f64::EPSILON,
        );
        assert!(config.is_ok());
    }
}
