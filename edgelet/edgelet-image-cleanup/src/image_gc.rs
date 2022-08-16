// Copyright (c) Microsoft. All rights reserved.

use std::{collections::HashSet, time::Duration};

use chrono::Timelike;
use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_docker::ImagePruneData;
use edgelet_settings::base::image::ImagePruneSettings;

use crate::error::ImageCleanupError;

const TOTAL_MINS_IN_DAY: u32 = 1440;
const DEFAULT_CLEANUP_TIME: &str = "00:00"; // midnight
const DEFAULT_RECURRENCE_IN_SECS: std::time::Duration = Duration::from_secs(60 * 60 * 24); // 1 day

pub async fn image_garbage_collect(
    edge_agent_bootstrap: String,
    settings: ImagePruneSettings,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    image_use_data: ImagePruneData,
) -> Result<(), ImageCleanupError> {
    log::info!("Starting image garbage collection task...");

    let is_enabled: bool = *settings.is_enabled().get_or_insert(true);

    if !is_enabled {
        return std::future::pending().await;
    }

    let cleanup_time = &mut settings.cleanup_time();
    let cleanup_time = cleanup_time.get_or_insert(DEFAULT_CLEANUP_TIME.to_string());

    let diff_in_secs: u32 = get_sleep_time_mins(cleanup_time) * 60;
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
                    log::info!(
                        "Bootstrap Edge Agent image {} has ID {}",
                        edge_agent_bootstrap,
                        id
                    );
                    bootstrap_image_id_option = Some(id.clone());
                } else {
                    log::debug!("The bootstrap Edge Agent image was not found on this device");
                }
            } else {
                log::error!("Could not get bootstrap image id");
            }
        }

        if bootstrap_image_id_option.is_some()
            || (bootstrap_image_id_option.is_none() && is_bootstrap_image_deleted)
        {
            remove_unused_images(
                runtime,
                image_use_data.clone(),
                bootstrap_image_id_option.clone(),
            )
            .await?;
        }

        // sleep till it's time to wake up based on recurrence (and on current time post-last-execution to avoid time drift)
        let mut recurrence = settings.cleanup_recurrence();
        let recurrence = *recurrence.get_or_insert(DEFAULT_RECURRENCE_IN_SECS);
        let delay = recurrence
            - Duration::from_secs(
                ((TOTAL_MINS_IN_DAY - get_sleep_time_mins(cleanup_time)) * 60).into(),
            );
        tokio::time::sleep(delay).await;
    }
}

async fn remove_unused_images(
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    image_use_data: ImagePruneData,
    bootstrap_image_id_option: Option<String>,
) -> Result<(), ImageCleanupError> {
    log::info!("Image Garbage Collection starting scheduled run");

    let bootstrap_img_id = match bootstrap_image_id_option.clone() {
        Some(id) => id,
        None => String::default(),
    };

    // track images associated with extant containers
    let modules = ModuleRuntime::list_with_details(runtime)
        .await
        .map_err(ImageCleanupError::ListRunningModules)?;
    let mut in_use_image_ids: HashSet<String> = HashSet::new();
    if !bootstrap_img_id.is_empty() {
        // the bootstrap edge agent image should never be deleted.
        in_use_image_ids.insert(bootstrap_img_id.clone());
    }

    for module in modules {
        // this is effectively the result of calling GET on /containers/json
        // image_hash() is created from ImageId (ImageId is filled in by GET /containers/json)
        let id = edgelet_core::Module::config(&module.0)
            .image_hash()
            .ok_or(ImageCleanupError::GetImageId())?;
        in_use_image_ids.insert(id.to_string());
    }

    let image_map = image_use_data
        .prune_images_from_file(in_use_image_ids)
        .map_err(ImageCleanupError::PruneImages)?;

    // delete images
    for key in image_map.keys() {
        if let Err(e) = ModuleRegistry::remove(runtime, key).await {
            log::error!("Could not delete image {} : {}", key, e);
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
) -> Result<(String, bool), ImageCleanupError> {
    let image_name_to_id = ModuleRuntime::list_images(runtime)
        .await
        .map_err(ImageCleanupError::ListImages)?;
    let image_id_option = image_name_to_id.get(&edge_agent_bootstrap);

    let bootstrap_image_id =
        image_id_option.map_or_else(String::default, |image_id| image_id.to_string());

    Ok((bootstrap_image_id.clone(), bootstrap_image_id.is_empty()))
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
    use super::get_sleep_time_mins;
    use chrono::Timelike;

    const TOTAL_MINS_IN_DAY: u32 = 1440;

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
