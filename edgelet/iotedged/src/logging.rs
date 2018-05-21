// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::io::Write;

use env_logger;
use failure::Fail;
use log::{Level, LevelFilter};

use error::Error;

const ENV_LOG: &str = "IOTEDGE_LOG";

pub fn init() {
    env_logger::Builder::new()
        .format(|fmt, record| {
            let level = match record.level() {
                Level::Trace => "TRCE",
                Level::Debug => "DBUG",
                Level::Info => "INFO",
                Level::Warn => "WARN",
                Level::Error => "ERR!",
            };
            let timestamp = fmt.timestamp();

            if record.level() >= Level::Debug {
                writeln!(
                    fmt,
                    "<{}>{} [{}] - [{}] {}",
                    syslog_level(&record.level()),
                    timestamp,
                    level,
                    record.target(),
                    record.args()
                )
            } else {
                writeln!(
                    fmt,
                    "<{}>{} [{}] - {}",
                    syslog_level(&record.level()),
                    timestamp,
                    level,
                    record.args()
                )
            }
        })
        .filter_level(LevelFilter::Info)
        .parse(&env::var(ENV_LOG).unwrap_or_default())
        .init();
}

fn syslog_level(level: &Level) -> i8 {
    match level {
        Level::Error => 3,
        Level::Warn => 4,
        Level::Info => 6,
        Level::Debug => 7,
        Level::Trace => 7,
    }
}

pub fn log_error(error: &Error) {
    let mut fail: &Fail = error;
    error!("{}", error.to_string());
    while let Some(cause) = fail.cause() {
        error!("\tcaused by: {}", cause.to_string());
        fail = cause;
    }
}
