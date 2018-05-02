// Copyright (c) Microsoft. All rights reserved.

use std::env;
use std::io::Write;

use env_logger;
use log::{Level, LevelFilter};

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
