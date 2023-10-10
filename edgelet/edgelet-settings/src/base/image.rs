// Copyright (c) Microsoft. All rights reserved.
use std::time::Duration;

use serde::{Deserialize, Serialize};

/// This struct is a wrapper for options that allow a user to override the defaults of
/// the image gabage collection job and customize their settings.
#[derive(Clone, Debug, Deserialize, Serialize, Eq, PartialEq)]
pub struct ImagePruneSettings {
    #[serde(
        default = "default_cleanup_recurrence",
        deserialize_with = "validate_recurrence",
        serialize_with = "humantime_serde::serialize"
    )]
    /// how frequently images should be garbage collected
    cleanup_recurrence: Duration,
    #[serde(
        default = "default_image_age_cleanup_threshold",
        with = "humantime_serde"
    )]
    /// minimum (unused) image "age" to be eligible for garbage collection
    image_age_cleanup_threshold: Duration,
    /// time in "HH::MM" format when cleanup job runs
    #[serde(default, with = "hhmm_as_minutes")]
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

    pub fn is_default(value: &Self) -> bool {
        value == &Self::default()
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

mod hhmm_as_minutes {
    use chrono::Timelike;
    use serde::{Deserialize, Serialize};

    const TIME_FORMAT: &str = "%H:%M";

    pub fn deserialize<'de, D>(de: D) -> Result<u64, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let value = String::deserialize(de)?;
        let time = chrono::NaiveTime::parse_from_str(&value, TIME_FORMAT)
            .map_err(<D::Error as serde::de::Error>::custom)?;
        Ok((time.hour() * 60 + time.minute()).into())
    }

    // NOTE: Reference required by serde
    #[allow(clippy::trivially_copy_pass_by_ref)]
    pub fn serialize<S>(cleanup_time: &u64, ser: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        chrono::NaiveTime::from_num_seconds_from_midnight_opt(
            u32::try_from(*cleanup_time * 60).expect("cleanup_time * 60 < 86400 < u32::MAX"),
            0,
        )
        .ok_or_else(|| <S::Error as serde::ser::Error>::custom("invalid timestamp"))?
        .format(TIME_FORMAT)
        .to_string()
        .serialize(ser)
    }
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
