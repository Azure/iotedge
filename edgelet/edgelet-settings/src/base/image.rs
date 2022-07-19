// Copyright (c) Microsoft. All rights reserved.
use std::time::Duration;

#[derive(Debug, serde::Deserialize, serde::Serialize, Clone)]
pub struct Settings {
    #[serde(with = "humantime_serde")]
    cleanup_schedule: Duration,
    #[serde(with = "humantime_serde")]
    min_age: Duration,
}
