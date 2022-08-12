// Copyright (c) Microsoft. All rights reserved.

use std::{collections::HashSet, time::Duration};

use crate::error::Error as EdgedError;
use chrono::{NaiveTime, Timelike};
use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_docker::ImagePruneData;
use edgelet_settings::{base::image::ImagePruneSettings, RuntimeSettings};

const TOTAL_MINS_IN_DAY: u32 = 1440;
const DEFAULT_CLEANUP_TIME: &str = "00:00"; // midnight
const DEFAULT_RECURRENCE_IN_SECS: u64 = 60 * 60 * 24; // 1 day
const DEFAULT_MIN_AGE_IN_SECS: u64 = 60 * 60 * 24 * 7; // 7 days

pub(crate) async fn image_garbage_collect(
    settings: edgelet_settings::Settings,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    image_use_data: ImagePruneData,
) -> Result<(), EdgedError> {
    log::info!("Starting image auto-pruning task...");

    let edge_agent_bootstrap: String = settings.agent().config().image().to_string();

    let defaults = ImagePruneSettings::new(
        Duration::from_secs(DEFAULT_RECURRENCE_IN_SECS),
        Duration::from_secs(DEFAULT_MIN_AGE_IN_SECS),
        DEFAULT_CLEANUP_TIME.to_string(),
        true,
    );

    // TODO: Can be left as option
    let settings = match settings.image_garbage_collection() {
        Some(parsed) => parsed,
        None => {
            log::info!("No [image_garbage_collection] settings found in config.toml, using default settings");
            &defaults
        }
    };

    // If settings are present in the config, they will always be validated (even if auto-pruning is disabled).
    // TODO: should be moved either to settings struct or image_prune_data_struct <--- probably here
    if validate_settings(settings).is_err() {
        std::process::exit(exitcode::CONFIG);
    }

    let diff_in_secs: u32 = get_sleep_time_mins(&settings.cleanup_time()) * 60;
    tokio::time::sleep(Duration::from_secs(diff_in_secs.into())).await;

    let mut bootstrap_image_id_option = None;
    let mut is_bootstrap_image_deleted: bool = false;

    loop {
        if bootstrap_image_id_option.is_none() {
            // bootstrap edge agent image should never be deleted
            if let Ok((id, is_image_deleted)) =
                get_bootstrap_image_id(runtime, edge_agent_bootstrap.clone()).await
            {
                is_bootstrap_image_deleted = is_image_deleted;
                if !is_image_deleted {
                    log::info!("Bootstrap EdgeAgent {} has ID {}", edge_agent_bootstrap, id);
                    bootstrap_image_id_option = Some(id.clone());
                }
            } else {
                log::error!("Could not get bootstrap image id");
            }
        }

        if settings.is_enabled() {
            if let Err(err) = remove_unused_images(
                runtime,
                image_use_data.clone(),
                bootstrap_image_id_option.clone(),
                is_bootstrap_image_deleted,
            )
            .await
            {
                return Err(EdgedError::new(format!(
                    "Error in image auto-pruning task: {}",
                    err
                )));
            };
        }

        // sleep till it's time to wake up based on recurrence (and on current time post-last-execution to avoid time drift)
        let delay = settings.cleanup_recurrence()
            - Duration::from_secs(
                ((TOTAL_MINS_IN_DAY - get_sleep_time_mins(&settings.cleanup_time())) * 60).into(),
            );
        tokio::time::sleep(delay).await;
    }
}

async fn remove_unused_images(
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    image_use_data: ImagePruneData,
    bootstrap_image_id_option: Option<String>,
    is_bootstrap_image_deleted: bool,
) -> Result<(), EdgedError> {
    log::info!("Image Garbage Collection starting scheduled run");

    let bootstrap_img_id = match bootstrap_image_id_option.clone() {
        Some(id) => id,
        None => String::default(),
    };

    // track images associated with extant containers
    let in_use_image_ids = match ModuleRuntime::list_with_details(runtime).await {
        Ok(modules) => {
            let mut image_ids: HashSet<String> = HashSet::new();
            if !bootstrap_img_id.is_empty() {
                // the bootstrap edge agent image should never be deleted.
                image_ids.insert(bootstrap_img_id.clone());
            }

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

    if bootstrap_image_id_option.is_some()
        || (bootstrap_image_id_option.is_none() && is_bootstrap_image_deleted)
    {
        let image_map = image_use_data
            .prune_images_from_file(in_use_image_ids)
            .map_err(|e| {
                EdgedError::from_err(
                    "Image garbage collection failed to prune images from file.",
                    e,
                )
            })?;

        // delete images
        for key in image_map.keys() {
            if let Err(e) = ModuleRegistry::remove(runtime, key).await {
                log::error!("Could not delete image {} : {}", key, e);
            }
        }
    }

    Ok(())
}

/* ================================================ HELPER METHODS ================================================ */

// This is a helper method that gets the imageID of the bootstrap edge agent, if it is present on the box.
// This image is used as a fallback in certain scenarios and we have to make sure that we never delete it
// (though the customer can still do so).
//
// To wit, there are 4 possibilities (pertaining to this method's return values):
//    Image Id found       Has image been deleted?     Interpretation
//        yes                        yes               N/A [can't happen]
//        yes                        no                prune images, as per usual
//        no                         yes               customer deleted bootstrap; prune images, as per usual
//        no                         no                (Implicit) Docker Engine API error; update persistence file but
//                                                     do not prune images to ensure EA bootstrap isn't deleted

async fn get_bootstrap_image_id(
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    edge_agent_bootstrap: String,
) -> Result<(String, bool), EdgedError> {
    let mut is_bootstrap_deleted: bool = false;

    let bootstrap_image_id: String = match ModuleRuntime::list_images(runtime).await {
        Ok(image_name_to_id) => {
            let image_id_option = image_name_to_id.get(&edge_agent_bootstrap);

            if image_id_option.is_none() {
                is_bootstrap_deleted = true;
                String::default()
            } else {
                image_id_option
                    .ok_or_else(|| {
                        EdgedError::from_err(
                            format!(
                                "error getting image id for edge agent bootstrap image {}",
                                edge_agent_bootstrap
                            ),
                            edgelet_docker::Error::GetImageHash(),
                        )
                    })?
                    .to_string()
            }
        }
        Err(e) => {
            log::error!("Could not get list of docker images: {}", e);
            EdgedError::from_err("Could not get list of docker images: {}", e).to_string()
        }
    };

    Ok((bootstrap_image_id, is_bootstrap_deleted))
}

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

    let times = NaiveTime::parse_from_str(&settings.cleanup_time(), "%H:%M");
    if times.is_err() {
        log::error!("invalid settings provided in config: invalid cleanup time, expected format is \"HH:MM\" in 24-hour format.");
        return Err(EdgedError::new(edgelet_docker::Error::InvalidSettings(
            "invalid cleanup time, expected format is \"HH:MM\" in 24-hour format".to_string(),
        )));
    }

    Ok(())
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

        settings =
            ImagePruneSettings::new(Duration::MAX, Duration::MAX, "2:033".to_string(), false);
        result = validate_settings(&settings);
        assert!(result.is_err());

        settings =
            ImagePruneSettings::new(Duration::MAX, Duration::MAX, ":::00".to_string(), false);
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
