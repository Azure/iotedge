// Copyright (c) Microsoft. All rights reserved.

use failure::Fail;
use log::{log, Level};

pub fn log_failure(level: Level, fail: &dyn Fail) {
    log!(level, "{}", fail);
    for cause in fail.iter_causes() {
        log!(level, "\tcaused by: {}", cause);
    }
}
