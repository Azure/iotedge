// Copyright (c) Microsoft. All rights reserved.
use std::time::Duration;

#[derive(Debug, serde::Deserialize, serde::Serialize, Clone)]

/// This struct is a wrapper for options that allow a user to override the defaults of
/// the image gabage collection job and customize their settings.
pub struct ImagePruneSettings {
    #[serde(with = "humantime_serde")]
    /// how frequently images should be garbage collected
    cleanup_recurrence: Option<Duration>,
    #[serde(with = "humantime_serde")]
    /// minimum (unused) image "age" to be eligible for garbage collection
    image_age_cleanup_threshold: Option<Duration>,
    /// time in "HH::MM" format when cleanup job runs
    cleanup_time: Option<String>,
    // is image garbage collection enabled
    enabled: Option<bool>,
}

impl ImagePruneSettings {
    pub fn new(
        cleanup_recurrence: Option<Duration>,
        image_age_cleanup_threshold: Option<Duration>,
        cleanup_time: Option<String>,
        enabled: Option<bool>,
    ) -> ImagePruneSettings {
        ImagePruneSettings {
            cleanup_recurrence,
            image_age_cleanup_threshold,
            cleanup_time,
            enabled,
        }
    }

    pub fn cleanup_recurrence(&self) -> Option<Duration> {
        self.cleanup_recurrence
    }

    pub fn image_age_cleanup_threshold(&self) -> Option<Duration> {
        self.image_age_cleanup_threshold
    }

    pub fn cleanup_time(&self) -> Option<String> {
        self.cleanup_time.clone()
    }

    pub fn is_enabled(&self) -> Option<bool> {
        self.enabled
    }
}
