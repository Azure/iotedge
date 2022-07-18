// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Module, ModuleRuntime};
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
    log::info!("MIGC job starting daily run");

    // Step 1: read MIGC persistence file into in-mem map
    // this map now contains all images deployed to the device
    let res = fs::read_to_string(FILE_NAME);
    if res.is_err() {
        log::error!("Could not read MIGC store");
        return Err(EdgedError::new("Could not read MIGC store"));
    }

    let contents = res.unwrap();

    let mut image_map: HashMap<String, Duration> = HashMap::new(); // all images in MIGC store
    let mut carry_over: HashMap<String, Duration> = HashMap::new(); // all images to NOT be deleted by MIGC in this run

    contents
        .lines()
        .map(|line| line.split(' ').collect::<Vec<&str>>())
        .map(|vec| (vec[0].to_string(), vec[1]))
        .fold((), |_, (k, v)| {
            image_map.insert(k, Duration::from_secs(v.parse::<u64>().unwrap()));
        });

    /* ============================== */

    // Step 2: track images associated with extant containers

    // first get list of containers on the device, running or otherwise
    let running_modules = ModuleRuntime::list_with_details(runtime).await.unwrap();

    // then, based on ID, keep track of images currently being used (in map: carry_over)
    for module in running_modules {
        let key = module.0.config().image_hash();
        let value = image_map.get(key.unwrap()).unwrap();
        carry_over.insert(key.unwrap().to_string(), *value);
    }

    /* ============================== */

    // Step 3: track entries younger than min age

    let current_time = std::time::SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap();

    // TODO: read min_age from settings, let's assume min_age as 1 day for now
    for (key, value) in &image_map {
        if current_time.as_secs() - value.as_secs() < 86400 {
            carry_over.insert(key.to_string(), *value);
        }
    }

    /* ============================== */

    // Step 4: delete images
    
    // clean up image map to make sure entries that need to be preserved are not removed
    for key in carry_over.keys() {
        image_map.remove(key);
    }

    for key in image_map.keys() {
        let result = ModuleRuntime::remove(runtime, key).await;
        if result.is_err() {
            log::error!("Could not delete image {}", key);
        }
    }

    /* ============================== */

    // Step 5: write previously removed entried back to file

    // instead of deleting existing entries from file, we just recreate it
    // TODO: handle synchronization
    let mut file = std::fs::File::create(FILE_NAME).unwrap();

    for (key, value) in &carry_over {
        let image_details = format!("{} {:?}\n", key, value);
        let res = write!(file, "{}", image_details);
        if res.is_err() {
            log::error!("Could not write image:{} with timestamp:{} to MIGC store", key, value.as_secs());
        }
    }

    Ok(())
}
