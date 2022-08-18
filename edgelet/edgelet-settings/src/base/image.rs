// Copyright (c) Microsoft. All rights reserved.
use std::time::Duration;

#[derive(Debug, serde::Deserialize, serde::Serialize, Clone)]

/// This struct is a wrapper for options that allow a user to override the defaults of
/// the image gabage collection job and customize their settings.
pub struct ImagePruneSettings {
    #[serde(with = "humantime_serde")]
    #[serde(default = "default_cleanup_recurrence")]
    /// how frequently images should be garbage collected
    cleanup_recurrence: Duration,
    #[serde(with = "humantime_serde")]
    #[serde(default = "default_image_age_cleanup_threshold")]
    /// minimum (unused) image "age" to be eligible for garbage collection
    image_age_cleanup_threshold: Duration,
    /// time in "HH::MM" format when cleanup job runs
    #[serde(default = "default_cleanup_time")]
    cleanup_time: String,
    // is image garbage collection enabled
    #[serde(default = "default_enabled")]
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

// 1 day
fn default_cleanup_recurrence() -> Duration {
    Duration::from_secs(60 * 60 * 24)
}

// 7 days
fn default_image_age_cleanup_threshold() -> Duration {
    Duration::from_secs(60 * 60 * 24 * 7)
}

// midnight
fn default_cleanup_time() -> String {
    "00:00".to_string()
}

fn default_enabled() -> bool {
    true
}

impl Default for ImagePruneSettings {
    fn default() -> Self {
        ImagePruneSettings {
            cleanup_recurrence: default_cleanup_recurrence(),
            image_age_cleanup_threshold: default_image_age_cleanup_threshold(),
            cleanup_time: default_cleanup_time(),
            enabled: default_enabled(),
        }
    }
}
