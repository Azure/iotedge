// Copyright (c) Microsoft. All rights reserved.
use std::time::Duration;

#[derive(Debug, serde::Deserialize, serde::Serialize, Clone)]
pub struct ImagePruneSettings {
    #[serde(with = "humantime_serde")]
    cleanup_recurrence: Duration,
    #[serde(with = "humantime_serde")]
    image_age_cleanup_threshold: Duration,
    cleanup_time: String,
    enabled: bool,
}

impl ImagePruneSettings {
    pub fn new(
        cleanup_recurrence: Duration,
        image_age_cleanup_threshold: Duration,
        cleanup_time: String,
        enabled: bool,
    ) -> ImagePruneSettings {
        ImagePruneSettings {
            cleanup_recurrence,
            image_age_cleanup_threshold,
            cleanup_time,
            enabled,
        }
    }

    pub fn cleanup_recurrence(&self) -> Duration {
        self.cleanup_recurrence
    }

    pub fn image_age_cleanup_threshold(&self) -> Duration {
        self.image_age_cleanup_threshold
    }

    pub fn cleanup_time(&self) -> String {
        self.cleanup_time.clone()
    }

    pub fn is_enabled(&self) -> bool {
        self.enabled
    }
}
