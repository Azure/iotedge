// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;
use log::Level;

pub fn log_failure<F: Fail>(level: Level, error: &F) {
    let mut fail: &Fail = error;
    log!(level, "{}", fail.to_string());
    while let Some(cause) = fail.cause() {
        log!(level, "\tcaused by: {}", cause.to_string());
        fail = cause;
    }
}
