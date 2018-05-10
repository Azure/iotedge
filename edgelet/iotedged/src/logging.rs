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
                    "{} [{}] - [{}] {}",
                    timestamp,
                    level,
                    record.target(),
                    record.args()
                )
            } else {
                writeln!(fmt, "{} [{}] - {}", timestamp, level, record.args())
            }
        })
        .filter_level(LevelFilter::Info)
        .parse(&env::var(ENV_LOG).unwrap_or_default())
        .init();
}

pub fn log_error(error: &Error) {
    let mut fail: &Fail = error;
    error!("{}", error.to_string());
    while let Some(cause) = fail.cause() {
        error!("\tcaused by: {}", cause.to_string());
        fail = cause;
    }
}
