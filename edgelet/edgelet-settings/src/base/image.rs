// Copyright (c) Microsoft. All rights reserved.
use chrono::Timelike;
use serde::Deserialize;
use std::time::Duration;

#[derive(Debug, serde::Deserialize, serde::Serialize, Clone)]

/// This struct is a wrapper for options that allow a user to override the defaults of
/// the image gabage collection job and customize their settings.
pub struct ImagePruneSettings {
    #[serde(
        default = "default_cleanup_recurrence",
        deserialize_with = "validate_recurrence",
        serialize_with = "humantime_serde::serialize"
    )]
    /// how frequently images should be garbage collected
    cleanup_recurrence: Duration,
    #[serde(with = "humantime_serde")]
    #[serde(default = "default_image_age_cleanup_threshold")]
    /// minimum (unused) image "age" to be eligible for garbage collection
    image_age_cleanup_threshold: Duration,
    /// time in "HH::MM" format when cleanup job runs
    #[serde(default, deserialize_with = "hhmm_to_minutes")]
    cleanup_time: u64,
    // is image garbage collection enabled
    #[serde(default = "default_enabled")]
    enabled: bool,
}

impl ImagePruneSettings {
    pub fn new(
        cleanup_recurrence: Duration,
        image_age_cleanup_threshold: Duration,
        cleanup_time: u64,
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

    pub fn cleanup_time(&self) -> u64 {
        self.cleanup_time
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

fn default_enabled() -> bool {
    true
}

fn validate_recurrence<'de, D>(de: D) -> Result<Duration, D::Error>
where
    D: serde::Deserializer<'de>,
{
    const MIN_CLEANUP_RECURRENCE: u128 = 60 * 60 * 24 * 1_000_000_000; // 1 day in nanoseconds
    let recurrence: Duration = humantime_serde::deserialize(de)?;

    if (recurrence.as_nanos() % MIN_CLEANUP_RECURRENCE) != 0 {
        return Err(<D::Error as serde::de::Error>::invalid_value(
            serde::de::Unexpected::Other(&format!("{:?}", recurrence)),
            &"duration that is a multiple of days",
        ));
    }

    Ok(recurrence)
}

fn hhmm_to_minutes<'de, D>(de: D) -> Result<u64, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(de)?;
    let time = chrono::NaiveTime::parse_from_str(&value, "%H:%M")
        .map_err(<D::Error as serde::de::Error>::custom)?;
    Ok((time.hour() * 60 + time.minute()).into())
}

impl Default for ImagePruneSettings {
    fn default() -> Self {
        ImagePruneSettings {
            cleanup_recurrence: default_cleanup_recurrence(),
            image_age_cleanup_threshold: default_image_age_cleanup_threshold(),
            cleanup_time: 0,
            enabled: default_enabled(),
        }
    }
}
