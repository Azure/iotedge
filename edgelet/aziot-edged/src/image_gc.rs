// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Module, ModuleRegistry, ModuleRuntime};
use std::collections::HashMap;
use std::fs;
use std::io::Write;
use std::time::{Duration, UNIX_EPOCH};

use crate::error::Error as EdgedError;

// TODO: fix file name with correct path
const FILE_NAME: &str = "abc.txt";

pub(crate) async fn run_until_shutdown(
    settings: edgelet_settings::docker::Settings,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
) -> Result<edgelet_core::WatchdogAction, EdgedError> {
    // Run the GC once a day while waiting for any running task
    let gc_period = std::time::Duration::from_secs(86400);

    let mut gc_timer = tokio::time::interval(gc_period);
    gc_timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);

    log::info!("Starting MIGC...");

    loop {
        if let Err(err) = garbage_collector(&settings, runtime).await {
            return Err(EdgedError::new(format!("Error in MIGC: {}", err)));
        }
    }
}

async fn garbage_collector(
    _settings: &edgelet_settings::docker::Settings,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
) -> Result<(), EdgedError> {
    log::info!("Module Image Garbage Collection starting daily run");

    // track images associated with extant containers

    // first get list of containers on the device, running or otherwise
    let running_modules = ModuleRuntime::list_with_details(runtime).await.unwrap();

    let image_map = process_and_update_file(running_modules).await;

    // delete images
    for key in image_map.keys() {
        let result = ModuleRegistry::remove(runtime, key).await;
        if result.is_err() {
            log::error!("Could not delete image {}", key);
        }
    }

    Ok(())
}

async fn process_and_update_file(
    running_modules: std::vec::Vec<(
        edgelet_docker::DockerModule<http_common::Connector>,
        edgelet_core::ModuleRuntimeState,
    )>,
) -> HashMap<String, Duration> {
    // read MIGC persistence file into in-mem map
    // this map now contains all images deployed to the device (through an IoT Edge deployment)
    let image_map = get_images_with_timestamp().map_err(|e| e).unwrap();

    /* ============================== */

    // process maps
    let (images_to_delete, carry_over) = process_state(image_map, running_modules);

    /* ============================== */

    // write previously removed entries back to file
    write_images_with_timestamp(&carry_over).map_err(|e| e);

    /* ============================== */

    // these are the images we need to prune; MIGC file has already been updated
    images_to_delete
}

fn get_images_with_timestamp() -> Result<HashMap<String, Duration>, EdgedError> {
    let res = fs::read_to_string(FILE_NAME);
    if res.is_err() {
        log::error!("Could not read MIGC store");
        return Err(EdgedError::new("Could not read MIGC store"));
    }

    let contents = res.unwrap();

    let mut image_map: HashMap<String, Duration> = HashMap::new(); // all images in MIGC store

    // TL;DR: this dumps MIGC store contents into the image_map, where
    // Key: Image hash, Value: Timestamp when image was last used (in epoch)
    contents
        .lines()
        .map(|line| line.split(' ').collect::<Vec<&str>>())
        .map(|vec| (vec[0].to_string(), vec[1]))
        .fold((), |_, (k, v)| {
            image_map.insert(k, Duration::from_secs(v.parse::<u64>().unwrap()));
        });

    Ok(image_map)
}

fn write_images_with_timestamp(
    state_to_persist: &HashMap<String, Duration>,
) -> Result<(), EdgedError> {
    // instead of deleting existing entries from file, we just recreate it
    // TODO: handle synchronization
    let mut file = std::fs::File::create(FILE_NAME).unwrap();

    for (key, value) in state_to_persist {
        let image_details = format!("{} {:?}\n", key, value);
        let res = write!(file, "{}", image_details);
        if res.is_err() {
            let msg = format!(
                "Could not write image:{} with timestamp:{} to MIGC store",
                key,
                value.as_secs()
            );
            log::error!("{}", msg);
            return Err(EdgedError::new(msg));
        }
    }

    Ok(())
}

fn process_state(
    mut image_map: HashMap<String, Duration>,
    running_modules: Vec<(
        edgelet_docker::DockerModule<http_common::Connector>,
        edgelet_core::ModuleRuntimeState,
    )>,
) -> (HashMap<String, Duration>, HashMap<String, Duration>) {
    let current_time = std::time::SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap();

    let mut carry_over: HashMap<String, Duration> = HashMap::new(); // all images to NOT be deleted by MIGC in this run

    // then, based on ID, keep track of images currently being used (in map: carry_over)
    for module in running_modules {
        let key = module.0.config().image_hash();

        // Since the images are currently being used, we update the timestamp to the current time
        // This avoids the case where a container crash just as MIGC is kicking off removes a needed image
        carry_over.insert(key.unwrap().to_string(), current_time);
    }

    // track entries younger than min age
    // TODO: read min_age from settings, let's assume min_age as 1 day for now
    for (key, value) in &image_map {
        if current_time.as_secs() - value.as_secs() < 86400 {
            carry_over.insert(key.to_string(), *value);
        }
    }

    // clean up image map to make sure entries that need to be preserved are not removed
    for key in carry_over.keys() {
        image_map.remove(key);
    }

    (image_map, carry_over)
}

#[cfg(test)]
mod tests {
    use super::*;
}
