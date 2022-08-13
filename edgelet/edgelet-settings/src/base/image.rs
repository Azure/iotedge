// Copyright (c) Microsoft. All rights reserved.
use std::time::Duration;

/// This struct is a wrapper for options that allow a user to override the defaults of
/// the image gabage collection job and customize their settings.
#[derive(Clone, Debug, Eq, PartialEq, serde::Deserialize, serde::Serialize)]
#[serde(default)]
pub struct ImagePruneSettings {
    /// Frequency of image garbage collection.
    #[serde(with = "humantime_serde")]
    cleanup_recurrence: Duration,
    /// Minimum (unused) image age to be eligible for garbage collection.
    #[serde(with = "humantime_serde")]
    image_age_cleanup_threshold: Duration,
    // Whether image garbage collection is enabled.
    enabled: bool,
}

impl Default for ImagePruneSettings {
    fn default() -> Self {
        Self {
            cleanup_recurrence: Duration::from_secs(60 * 60 * 24),
            image_age_cleanup_threshold: Duration::from_secs(60 * 60 * 24 * 7),
            enabled: false,
        }
    }
}

impl ImagePruneSettings {
    pub fn new(
        cleanup_recurrence: Duration,
        image_age_cleanup_threshold: Duration,
        enabled: bool,
    ) -> ImagePruneSettings {
        ImagePruneSettings {
            cleanup_recurrence,
            image_age_cleanup_threshold,
            enabled,
        }
    }

    pub fn is_default(&self) -> bool {
        self == &Self::default()
    }

    pub fn cleanup_recurrence(&self) -> Duration {
        self.cleanup_recurrence
    }

    pub fn image_age_cleanup_threshold(&self) -> Duration {
        self.image_age_cleanup_threshold
    }

    pub fn is_enabled(&self) -> bool {
        self.enabled
    }
}
