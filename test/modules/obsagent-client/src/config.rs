use std::error::Error;

use clap::{value_t, App, Arg};

#[derive(Debug)]
pub struct Config {
    pub update_rate: f64,
    pub prom_config: PromConfig,
    pub otel_config: OTelConfig,
}

#[derive(Debug)]
pub struct OTelConfig {
    pub push_rate: f64,
    pub otlp_endpoint: String,
}

#[derive(Debug)]
pub struct PromConfig {
    pub endpoint: String,
}

pub fn init_config() -> Result<Config, Box<dyn Error + Send + Sync + 'static>> {
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

    let otel_config = OTelConfig {
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
    let prom_config = PromConfig {
        endpoint: std::env::var("PROMETHEUS_ENDPOINT").map_or_else(
            |_e| {
                Ok(matches
                    .value_of("prometheus-endpoint")
                    .unwrap_or("127.0.0.1:9600")
                    .to_string())
            },
            |v| v.parse(),
        )?
    };

    let config = Config {
        update_rate: std::env::var("UPDATE_RATE").map_or_else(
            |_e| Ok(value_t!(matches.value_of("update-rate"), f64).unwrap_or(1.0)),
            |v| v.parse(),
        )?,
        otel_config,
        prom_config,
    };
    Ok(config)
}
