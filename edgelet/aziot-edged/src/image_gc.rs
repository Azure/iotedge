// Copyright (c) Microsoft. All rights reserved.

use std::{collections::HashSet, time::Duration};

use crate::error::Error as EdgedError;
use chrono::Timelike;
use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_docker::ImagePruneData;
use edgelet_settings::{base::image::ImagePruneSettings, RuntimeSettings};

const TOTAL_MINS_IN_DAY: u32 = 1440;

pub(crate) async fn image_garbage_collect(
    settings: edgelet_settings::Settings,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    image_use_data: ImagePruneData,
) -> Result<(), EdgedError> {
    log::info!("Starting image auto-pruning task...");

    let edge_agent_bootstrap: String = settings.agent().config().image().to_string();

    // bootstrap edge agent image should never be deleted
    let bootstrap_image_id: String = match ModuleRuntime::list_images(runtime).await {
        Ok(image_name_to_id) => image_name_to_id
            .get(&edge_agent_bootstrap)
            .ok_or_else(|| {
                EdgedError::from_err(
                    "error getting image id for edge agent bootstrap image",
                    edgelet_docker::Error::GetImageHash(),
                )
            })?
            .to_string(),
        Err(e) => {
            log::error!("Could not get list of docker images: {}", e);
            std::process::exit(exitcode::UNAVAILABLE);
        }
    };

    let settings = match settings.image_garbage_collection() {
        Some(parsed) => parsed,
        None => {
            log::error!("Could not get settings for image auto-pruning task");
            std::process::exit(exitcode::CONFIG);
        }
    };

    // If settings are present in the config, they will always be validated (even if auto-pruning is disabled).
    if validate_settings(settings).is_err() {
        std::process::exit(exitcode::CONFIG);
    }

    let diff_in_secs: u32 = get_sleep_time_mins(&settings.cleanup_time()) * 60;
    tokio::time::sleep(Duration::from_secs(diff_in_secs.into())).await;

    loop {
        if let Err(err) =
            garbage_collector(runtime, image_use_data.clone(), bootstrap_image_id.clone()).await
        {
            return Err(EdgedError::new(format!(
                "Error in image auto-pruning task: {}",
                err
            )));
        }

        // sleep till it's time to wake up based on recrurrence (and on current time post-last-execution to avoid time drift)
        // total number of minutes in a day = 1440
        let delay = settings.cleanup_recurrence()
            - Duration::from_secs(
                ((1440 - get_sleep_time_mins(&settings.cleanup_time())) * 60).into(),
            );
        tokio::time::sleep(delay).await;
    }
}

async fn garbage_collector(
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    image_use_data: ImagePruneData,
    bootstrap_image_id: String,
) -> Result<(), EdgedError> {
    log::info!("Image Garbage Collection starting daily run");

    // track images associated with extant containers

    // first get list of containers on the device, running or otherwise
    let extant_containers = match ModuleRuntime::list_with_details(runtime).await {
        Ok(modules) => {
            let mut image_ids: HashSet<String> = HashSet::new();
            image_ids.insert(bootstrap_image_id); // bootstrap edge agent image should never be deleted

            for module in modules {
                // this is effectively the result of calling GET on /containers/json
                // image_hash() is created from ImageId (ImageId is filled in by GET /containers/json)
                let id = edgelet_core::Module::config(&module.0)
                    .image_hash()
                    .ok_or_else(|| {
                        EdgedError::from_err(
                            "error getting image id for running container",
                            edgelet_docker::Error::GetImageHash(),
                        )
                    })?;
                image_ids.insert(id.to_string());
            }
            image_ids
        }
        Err(err) => {
            return Err(EdgedError::new(format!(
                "Error in image auto-pruning task. Cannot get running modules. Skipping image auto pruning. {}",
                err
            )));
        }
    };

    let image_map = image_use_data
        .prune_images_from_file(extant_containers)
        .map_err(|e| {
            EdgedError::from_err(
                "Module image garbage collection failed to prune images from file.",
                e,
            )
        })?;

    // delete images
    for key in image_map.keys() {
        if let Err(e) = ModuleRegistry::remove(runtime, key).await {
            log::error!("Could not delete image {} : {}", key, e);
        }
    }

    Ok(())
}

/* ================================================ HELPER METHODS ================================================ */

fn validate_settings(settings: &ImagePruneSettings) -> Result<(), EdgedError> {
    if settings.cleanup_recurrence() < Duration::from_secs(60 * 60 * 24) {
        log::error!(
            "invalid settings provided in config: cleanup recurrence cannot be less than 1 day."
        );
        return Err(EdgedError::from_err(
            "invalid settings provided in config",
            edgelet_docker::Error::InvalidSettings(
                "cleanup recurrence cannot be less than 1 day".to_string(),
            ),
        ));
    }

    let times = settings.cleanup_time();
    if times.len() != 5 || !times.contains(':') {
        log::error!("invalid settings provided in config: invalid cleanup time, expected format is \"HH:MM\" in 24-hour format.");
        Err(EdgedError::from_err(
            "invalid settings provided in config:",
            edgelet_docker::Error::InvalidSettings(
                "invalid cleanup time, expected format is \"HH:MM\" in 24-hour format".to_string(),
            ),
        ))
    } else {
        let time_clone = times.clone();
        let cleanup_time: Vec<&str> = time_clone.split(':').collect();
        let hour = cleanup_time.get(0).unwrap().parse::<u32>().unwrap();
        let minute = cleanup_time.get(1).unwrap().parse::<u32>().unwrap();

        // u32, so no negative comparisons
        if hour > 23 || minute > 59 {
            log::error!(
                "invalid settings provided in config: invalid cleanup time {}",
                times
            );
            return Err(EdgedError::from_err(
                "invalid settings provided in config",
                edgelet_docker::Error::InvalidSettings(format!("invalid cleanup time {}", times)),
            ));
        }

        Ok(())
    }
}

fn get_sleep_time_mins(times: &str) -> u32 {
    // if string is empty, or if there's an input error, we fall back to default (midnight)
    let cleanup_mins = if times.is_empty() {
        TOTAL_MINS_IN_DAY
    } else {
        let cleanup_time: Vec<&str> = times.split(':').collect();
        let hour = cleanup_time.get(0).unwrap().parse::<u32>().unwrap();
        let minute = cleanup_time.get(1).unwrap().parse::<u32>().unwrap();

        60 * hour + minute
    };

    let current_hour = chrono::Local::now().hour();
    let current_minute = chrono::Local::now().minute();
    let current_time_in_mins = 60 * current_hour + current_minute;

    if current_time_in_mins < cleanup_mins {
        cleanup_mins - current_time_in_mins
    } else {
        TOTAL_MINS_IN_DAY - (current_time_in_mins - cleanup_mins)
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use chrono::Timelike;
    use edgelet_settings::base::image::ImagePruneSettings;

    use crate::image_gc::validate_settings;

    use super::get_sleep_time_mins;

    const TOTAL_MINS_IN_DAY: u32 = 1440;

    #[test]
    fn test_validate_settings() {
        let mut settings =
            ImagePruneSettings::new(Duration::MAX, Duration::MAX, "12345".to_string(), false);

        let mut result = validate_settings(&settings);
        assert!(result.is_err());

        settings =
            ImagePruneSettings::new(Duration::MAX, Duration::MAX, "abcde".to_string(), false);
        result = validate_settings(&settings);
        assert!(result.is_err());

        settings =
            ImagePruneSettings::new(Duration::MAX, Duration::MAX, "26:30".to_string(), false);
        result = validate_settings(&settings);
        assert!(result.is_err());

        settings =
            ImagePruneSettings::new(Duration::MAX, Duration::MAX, "16:61".to_string(), false);
        result = validate_settings(&settings);
        assert!(result.is_err());

        settings =
            ImagePruneSettings::new(Duration::MAX, Duration::MAX, "23:333".to_string(), false);
        result = validate_settings(&settings);
        assert!(result.is_err());
    }

    #[test]
    fn test_get_sleep_time_mins() {
        let cleanup_minutes = 12 * 60 + 39;
        let result = get_sleep_time_mins("12:39");

        let hour = chrono::Local::now().hour();
        let min = chrono::Local::now().minute();

        let curr_minutes: u32 = hour * 60 + min;

        let answer = if curr_minutes < cleanup_minutes {
            cleanup_minutes - curr_minutes
        } else {
            TOTAL_MINS_IN_DAY - (curr_minutes - cleanup_minutes)
        };

        assert!(answer == result);
    }
}
