// Copyright (c) Microsoft. All rights reserved.
use std::time::Duration;

#[derive(Debug, serde::Deserialize, serde::Serialize, Clone)]
pub struct MIGCSettings {
    #[serde(with = "humantime_serde")]
    time_between_cleanup: Duration,
    #[serde(with = "humantime_serde")]
    min_age: Duration,
    is_enabled: bool,
}

impl MIGCSettings {
    pub fn new(
        time_between_cleanup: Duration,
        min_age: Duration,
        is_enabled: bool,
    ) -> MIGCSettings {
        MIGCSettings {
            time_between_cleanup,
            min_age,
            is_enabled,
        }
    }

    pub fn time_between_cleanup(&self) -> Duration {
        self.time_between_cleanup
    }

    pub fn min_age(&self) -> Duration {
        self.min_age
    }

    pub fn is_enabled(&self) -> bool {
        self.is_enabled
    }
}
