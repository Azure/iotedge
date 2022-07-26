// Copyright (c) Microsoft. All rights reserved.
use std::time::Duration;

#[derive(Debug, serde::Deserialize, serde::Serialize, Clone)]
pub struct MIGCSettings {
    #[serde(with = "humantime_serde")]
    time_between_cleanup: Duration,
    #[serde(with = "humantime_serde")]
    min_age: Duration,
}

impl MIGCSettings {
    pub fn time_between_cleanup(&self) -> Duration {
        self.time_between_cleanup.clone()
    }

    pub fn min_age(&self) -> Duration {
        self.min_age.clone()
    }
}
