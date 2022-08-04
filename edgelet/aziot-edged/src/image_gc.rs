// Copyright (c) Microsoft. All rights reserved.

use std::time::Duration;

use chrono::Timelike;
use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_docker::MIGCPersistence;
use edgelet_settings::base::image::MIGCSettings;

use crate::error::Error as EdgedError;

pub(crate) async fn image_garbage_collect(
    settings: Option<MIGCSettings>,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    migc_persistence: MIGCPersistence,
) -> Result<(), EdgedError> {
    log::info!("Starting image auto-pruning task...");

    let settings = match settings {
        Some(parsed) => parsed,
        None => {
            return Err(EdgedError::new("Could not start Image auto-pruning task; contaier images will not be cleaned up automatically".to_string()));
        }
    };

    // sleep till it's time for the first execution
    let diff: u32 = get_initial_sleep_time_mins(&settings.cleanup_time()) * 60;

    tokio::time::sleep(Duration::from_secs(diff.into())).await;

    loop {
        if let Err(err) = garbage_collector(runtime, migc_persistence.clone()).await {
            return Err(EdgedError::new(format!(
                "Error in image auto-pruning task: {}",
                err
            )));
        }

        // TODO: get cleanup_recurrence() - Utc::now()) and then sleep for that time
        tokio::time::sleep(settings.cleanup_recurrence()).await;
    }
}

async fn garbage_collector(
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    migc_persistence: MIGCPersistence,
) -> Result<(), EdgedError> {
    log::info!("Module Image Garbage Collection starting daily run");

    // track images associated with extant containers

    // first get list of containers on the device, running or otherwise
    let running_modules = match ModuleRuntime::list_with_details(runtime).await {
        Ok(modules) => {
            let mut image_ids: Vec<String> = Vec::new();
            for module in modules {
                let id = edgelet_core::Module::config(&module.0).image_hash().ok_or(
                    EdgedError::from_err(
                        "error getting image id for running container",
                        edgelet_docker::Error::GetImageHash(),
                    ),
                )?;
                image_ids.push(id.to_string());
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

    let image_map = migc_persistence
        .prune_images_from_file(running_modules)
        .map_err(|e| {
            EdgedError::from_err(
                "Module image garbage collection failed to prune images from file.",
                e,
            )
        })?;

    // delete images
    for key in image_map.keys() {
        let result = ModuleRegistry::remove(runtime, key).await;
        if result.is_err() {
            log::error!("Could not delete image {}", key);
        }
    }

    Ok(())
}

fn get_initial_sleep_time_mins(times: &str) -> u32 {
    let mut cleanup_mins = 0;

    // TODO: use regex?
    // if string is empty, or if there's an input error, we fall back to default (midnight)
    if times.is_empty() || !times.contains(':') || times.len() > 5 {
        cleanup_mins = 60 * 24;
    } else {
        let cleanup_time: Vec<&str> = times.split(':').collect();
        let hour = cleanup_time.get(0).unwrap().parse::<u32>().unwrap();
        let minute = cleanup_time.get(1).unwrap().parse::<u32>().unwrap();

        // u32, so no negative comparisons
        if hour > 23 || minute > 59 {
            cleanup_mins = 60 * 24;
        } else {
            cleanup_mins = 60 * hour + minute;
        }
    }

    let current_hour = chrono::Utc::now().hour();
    let current_minute = chrono::Utc::now().minute();
    let current_time_in_mins = 60 * current_hour + current_minute;

    let mut diff = 0;
    if current_time_in_mins < cleanup_mins {
        diff = cleanup_mins - current_time_in_mins;
    } else {
        diff = current_time_in_mins - cleanup_mins;
    }

    diff
}

#[cfg(test)]
mod tests {
    use chrono::Timelike;

    use super::get_initial_sleep_time_mins;

    #[test]
    fn test_validations() {
        let mut result = get_initial_sleep_time_mins(String::default().as_str());
        assert!(result == (60 * 24 - 60 * chrono::Utc::now().hour() - chrono::Utc::now().minute()));

        result = get_initial_sleep_time_mins("12345");
        assert!(result == (60 * 24 - 60 * chrono::Utc::now().hour() - chrono::Utc::now().minute()));

        result = get_initial_sleep_time_mins("abcde");
        assert!(result == (60 * 24 - 60 * chrono::Utc::now().hour() - chrono::Utc::now().minute()));

        result = get_initial_sleep_time_mins("26:30");
        assert!(result == (60 * 24 - 60 * chrono::Utc::now().hour() - chrono::Utc::now().minute()));

        result = get_initial_sleep_time_mins("16:61");
        assert!(result == (60 * 24 - 60 * chrono::Utc::now().hour() - chrono::Utc::now().minute()));

        result = get_initial_sleep_time_mins("23:333");
        assert!(result == (60 * 24 - 60 * chrono::Utc::now().hour() - chrono::Utc::now().minute()));

        let cleanup_minutes = 12 * 60 + 39;
        result = get_initial_sleep_time_mins("12:39");

        let hour = chrono::Utc::now().hour();
        let min = chrono::Utc::now().minute();

        let mut curr_minutes: u32 = 0;

        if hour < 12 {
            curr_minutes = hour * 60 + min;
            assert!(result == cleanup_minutes - curr_minutes);
        } else {
            curr_minutes = 60 * 24;
            assert!(result == curr_minutes - cleanup_minutes);
        }
    }
}
