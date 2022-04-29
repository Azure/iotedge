// Copyright (c) Microsoft. All rights reserved.

use std::env;

use failure::Fail;
use log::{log, Level, LevelFilter};

pub fn init() {
    env_logger::Builder::new()
        .filter_level(LevelFilter::Info)
        .format_module_path(false)
        .parse_filters(&env::var("PROXY_LOG").unwrap_or_default())
        .init();
}

pub fn failure(fail: &dyn Fail) {
    log!(Level::Error, "{}", fail);
    for cause in fail.iter_causes() {
        log!(Level::Error, "\tcaused by: {}", cause);
    }
}
