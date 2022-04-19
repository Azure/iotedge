// Copyright (c) Microsoft. All rights reserved.

use log::{log, Level};

pub fn log_failure(level: Level, fail: &dyn std::error::Error) {
    log!(level, "{}", fail);
    let mut cur = fail;
    while let Some(cause) = cur.source() {
        log!(level, "\tcaused by: {}", cause);
        cur = cause;
    }
}
