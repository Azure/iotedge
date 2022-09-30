// Copyright (c) Microsoft. All rights reserved.

use std::{collections::HashSet, time::Duration};

use chrono::Timelike;
use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_docker::ImagePruneData;
use edgelet_settings::base::image::ImagePruneSettings;

use crate::error::ImageCleanupError;

const TOTAL_MINS_IN_DAY: u64 = 1440;

/// <summary>
/// This method is the main controller loop for image garbage collection.
/// [Note: It delegates the actual image deletion to remove_unused_images()]
/// - If GC is not enabled, it will simply return a future that never completes.
/// - If GC is enabled, it will sleep till the first occurrence of the
///   'cleanup time' that has been specified by the user.
///   After waking up, it'll try to get the bootstrap image ID [if it doesn't
///   already have it from a previous run], and then calls remove_unused_images()
///   Finally, it puts itself back to sleep till it's time for the next run.
pub async fn image_garbage_collect(
    edge_agent_bootstrap: String,
    settings: ImagePruneSettings,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    image_use_data: ImagePruneData,
) -> Result<(), ImageCleanupError> {
    log::info!("Starting image garbage collection task...");

    if !settings.is_enabled() {
        return std::future::pending().await;
    }

    let cleanup_time_in_mins = &mut settings.cleanup_time();

    let diff_in_secs: u64 = get_sleep_time_mins(*cleanup_time_in_mins) * 60;
    tokio::time::sleep(Duration::from_secs(diff_in_secs)).await;

    let mut bootstrap_image_id_option = None;
    let mut is_bootstrap_image_deleted: bool = false;

    loop {
        // Try to get the bootstrap image id if we failed on the last(/all previous) run(s)
        if bootstrap_image_id_option.is_none() {
            // bootstrap edge agent image should never be deleted
            if let Ok((id_option, is_image_deleted)) =
                get_bootstrap_image_id(runtime, edge_agent_bootstrap.clone()).await
            {
                is_bootstrap_image_deleted = is_image_deleted;
                if !is_image_deleted {
                    bootstrap_image_id_option = id_option.clone();
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
        let recurrence = settings.cleanup_recurrence();
        let delay = recurrence
            - Duration::from_secs(
                (TOTAL_MINS_IN_DAY - get_sleep_time_mins(*cleanup_time_in_mins)) * 60,
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
// There are 4 possibilities (pertaining to this method's return values):
//    Image Id found       Has image been deleted?     Interpretation
//        yes                        yes               N/A [can't happen]
//        yes                        no                prune images, as per usual
//        no                         yes               customer deleted bootstrap; prune images, as per usual
//        no                         no                (Implicit) Docker Engine API error; update persistence file but
//                                                     do not prune images to ensure EA bootstrap isn't deleted

async fn get_bootstrap_image_id(
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    edge_agent_bootstrap: String,
) -> Result<(Option<String>, bool), ImageCleanupError> {
    let image_name_to_id = ModuleRuntime::list_images(runtime)
        .await
        .map_err(ImageCleanupError::ListImages)?;
    let image_id_option = image_name_to_id.get(&edge_agent_bootstrap);

    let bootstrap_image_id =
        image_id_option.map_or_else(String::default, |image_id| image_id.to_string());

    if bootstrap_image_id.is_empty() {
        log::debug!("The bootstrap Edge Agent image was not found on this device");
    } else {
        log::info!(
            "Bootstrap Edge Agent image {} has ID {}",
            edge_agent_bootstrap,
            bootstrap_image_id
        );
    }

    Ok((
        Some(bootstrap_image_id.clone()),
        bootstrap_image_id.is_empty(),
    ))
}

fn get_sleep_time_mins(cleanup_mins: u64) -> u64 {
    let current_hour = chrono::Local::now().hour();
    let current_minute = chrono::Local::now().minute();
    let current_time_in_mins: u64 = (60 * current_hour + current_minute).into();

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

    const TOTAL_MINS_IN_DAY: u64 = 1440;

    #[test]
    fn test_get_sleep_time_mins() {
        let cleanup_minutes: u64 = 12 * 60 + 39;
        let result = get_sleep_time_mins(cleanup_minutes);

        let hour = chrono::Local::now().hour();
        let min = chrono::Local::now().minute();

        let curr_minutes: u64 = (hour * 60 + min).into();

        let answer = if curr_minutes < cleanup_minutes {
            cleanup_minutes - curr_minutes
        } else {
            TOTAL_MINS_IN_DAY - (curr_minutes - cleanup_minutes)
        };

        assert!(answer == result);
    }
}
