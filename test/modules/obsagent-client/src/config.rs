use std::{num::ParseFloatError, string::ParseError};

use clap::{value_t, App, Arg};
use thiserror::Error;

#[derive(Debug, Clone)]
pub struct Config {
    pub update_rate: f64,
    pub prom_config: PromConfig,
    pub otel_config: OTelConfig,
}

#[derive(Debug, Clone)]
pub struct OTelConfig {
    pub push_rate: f64,
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
}

impl Config {
    fn new(
        update_rate: f64,
        prom_config: PromConfig,
        otel_config: OTelConfig,
    ) -> Result<Config, ConfigError> {
        if update_rate < 0.0 {
            return Err(ConfigError::InvalidNegativeArgVal(update_rate));
        } else if update_rate == 0.0 {
            return Err(ConfigError::InvalidZeroArgVal(update_rate));
        }

        Ok(Config {
            update_rate,
            prom_config,
            otel_config,
        })
    }
}
impl OTelConfig {
    fn new(push_rate: f64, otlp_endpoint: String) -> Result<OTelConfig, ConfigError> {
        if push_rate < 0.0 {
            return Err(ConfigError::InvalidNegativeArgVal(push_rate));
        } else if push_rate == 0.0 {
            return Err(ConfigError::InvalidZeroArgVal(push_rate));
        }

        Ok(OTelConfig {
            push_rate,
            otlp_endpoint,
        })
    }
}

pub fn init_config() -> Result<Config, ConfigError> {
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

    let push_rate = std::env::var("PUSH_RATE")
        .map_or_else(
            |_e| Ok(value_t!(matches.value_of("push-rate"), f64).unwrap_or(0.2)),
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
    let otel_config = OTelConfig::new(push_rate, otlp_endpoint)?;

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

    let update_rate = std::env::var("UPDATE_RATE")
        .map_or_else(
            |_e| Ok(value_t!(matches.value_of("update-rate"), f64).unwrap_or(1.0)),
            |v| v.parse(),
        )
        .map_err(ConfigError::FloatArgParseError)?;
    let config = Config::new(update_rate, prom_config, otel_config)?;
    Ok(config)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_invalid_zero_push_rate() {
        let otel_config = OTelConfig::new(0.0, String::from("localhost:4317"));
        assert!(otel_config.is_err());
    }

    #[test]
    fn test_invalid_negative_push_rate() {
        let otel_config = OTelConfig::new(-1.0, String::from("localhost:4317"));
        assert!(otel_config.is_err());
    }

    #[test]
    fn test_invalid_zero_update_rate() {
        let prom_config = PromConfig {
            endpoint: String::from("localhost:9600"),
        };
        let otel_config = OTelConfig::new(1.0, String::from("localhost:4317")).unwrap();
        let config = Config::new(0.0, prom_config, otel_config);
        assert!(config.is_err());
    }

    #[test]
    fn test_invalid_negative_update_rate() {
        let prom_config = PromConfig {
            endpoint: String::from("localhost:9600"),
        };
        let otel_config = OTelConfig::new(1.0, String::from("localhost:4317")).unwrap();
        let config = Config::new(-1.0, prom_config, otel_config);
        assert!(config.is_err());
    }
}
