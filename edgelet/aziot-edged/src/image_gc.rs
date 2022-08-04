// Copyright (c) Microsoft. All rights reserved.

use std::{collections::HashSet, time::Duration};

use chrono::Timelike;
use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_docker::MIGCPersistence;
use edgelet_settings::RuntimeSettings;

use crate::error::Error as EdgedError;

pub(crate) async fn image_garbage_collect(
    settings: edgelet_settings::Settings,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    migc_persistence: MIGCPersistence,
) -> Result<(), EdgedError> {
    log::info!("Starting image auto-pruning task...");

    let edge_agent_bootstrap: String = settings.agent().config().image().to_string();

    // bootstrap edge agent image should never be deleted
    let bootstrap_image_id: String = match ModuleRuntime::list_images(runtime).await {
        Ok(image_name_to_id) => {
            if image_name_to_id.is_empty() {
                log::info!("No docker images present on device");
                String::default()
            } else {
                image_name_to_id
                    .get(&edge_agent_bootstrap)
                    .unwrap_or(&String::default())
                    .to_string()
            }
        }
        Err(e) => {
            log::error!("Could not get list of docker images: {}", e);
            String::default()
        }
    };

    let settings = match settings.module_image_garbage_collection() {
        Some(parsed) => parsed,
        None => {
            return Err(EdgedError::new("Could not start Image auto-pruning task; contaier images will not be cleaned up automatically".to_string()));
        }
    };

    // sleep till it's time for the first execution
    let diff: u32 = get_initial_sleep_time_mins(&settings.cleanup_time()) * 60;

    tokio::time::sleep(Duration::from_secs(diff.into())).await;

    loop {
        if let Err(err) = garbage_collector(
            runtime,
            migc_persistence.clone(),
            bootstrap_image_id.clone(),
        )
        .await
        {
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
    bootstrap_image_id: String,
) -> Result<(), EdgedError> {
    log::info!("Module Image Garbage Collection starting daily run");

    // track images associated with extant containers

    // first get list of containers on the device, running or otherwise
    let running_modules = match ModuleRuntime::list_with_details(runtime).await {
        Ok(modules) => {
            let mut image_ids: HashSet<String> = HashSet::new();
            image_ids.insert(bootstrap_image_id); // bootstrap edge agent image should never be deleted
            for module in modules {
                let id = edgelet_core::Module::config(&module.0).image_hash().ok_or(
                    EdgedError::from_err(
                        "error getting image id for running container",
                        edgelet_docker::Error::GetImageHash(),
                    ),
                )?;
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

    const TOTAL_MINS_IN_DAY: u32 = 1440;

    // if string is empty, or if there's an input error, we fall back to default (midnight)
    if times.is_empty() || !times.contains(':') || times.len() > 5 {
        cleanup_mins = TOTAL_MINS_IN_DAY;
    } else {
        let cleanup_time: Vec<&str> = times.split(':').collect();
        let hour = cleanup_time.get(0).unwrap().parse::<u32>().unwrap();
        let minute = cleanup_time.get(1).unwrap().parse::<u32>().unwrap();

        // u32, so no negative comparisons
        if hour > 23 || minute > 59 {
            cleanup_mins = TOTAL_MINS_IN_DAY;
        } else {
            cleanup_mins = 60 * hour + minute;
        }
    }

    let current_hour = chrono::Local::now().hour();
    let current_minute = chrono::Local::now().minute();
    let current_time_in_mins = 60 * current_hour + current_minute;

    let mut diff = 0;
    if current_time_in_mins < cleanup_mins {
        diff = cleanup_mins - current_time_in_mins;
    } else {
        diff = TOTAL_MINS_IN_DAY - (current_time_in_mins - cleanup_mins);
    }

    diff
}

#[cfg(test)]
mod tests {
    use chrono::Timelike;

    use super::get_initial_sleep_time_mins;

    const TOTAL_MINS_IN_DAY: u32 = 1440;

    #[test]
    fn test_validations() {
        let mut result = get_initial_sleep_time_mins(String::default().as_str());
        assert!(
            result
                == (TOTAL_MINS_IN_DAY
                    - 60 * chrono::Local::now().hour()
                    - chrono::Local::now().minute())
        );

        result = get_initial_sleep_time_mins("12345");
        assert!(
            result
                == (TOTAL_MINS_IN_DAY
                    - 60 * chrono::Local::now().hour()
                    - chrono::Local::now().minute())
        );

        result = get_initial_sleep_time_mins("abcde");
        assert!(
            result
                == (TOTAL_MINS_IN_DAY
                    - 60 * chrono::Local::now().hour()
                    - chrono::Local::now().minute())
        );

        result = get_initial_sleep_time_mins("26:30");
        assert!(
            result
                == (TOTAL_MINS_IN_DAY
                    - 60 * chrono::Local::now().hour()
                    - chrono::Local::now().minute())
        );

        result = get_initial_sleep_time_mins("16:61");
        assert!(
            result
                == (TOTAL_MINS_IN_DAY
                    - 60 * chrono::Local::now().hour()
                    - chrono::Local::now().minute())
        );

        result = get_initial_sleep_time_mins("23:333");
        assert!(
            result
                == (TOTAL_MINS_IN_DAY
                    - 60 * chrono::Local::now().hour()
                    - chrono::Local::now().minute())
        );

        let cleanup_minutes = 12 * 60 + 39;
        result = get_initial_sleep_time_mins("12:39");

        let hour = chrono::Local::now().hour();
        let min = chrono::Local::now().minute();

        let curr_minutes: u32 = hour * 60 + min;
        let mut answer = 0;

        if curr_minutes < cleanup_minutes {
            answer = cleanup_minutes - curr_minutes;
        } else {
            answer = TOTAL_MINS_IN_DAY - (curr_minutes - cleanup_minutes);
        }

        assert!(answer == result);
    }
}
