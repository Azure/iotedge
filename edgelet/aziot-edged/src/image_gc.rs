// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_docker::MIGCPersistence;
use edgelet_settings::base::image::MIGCSettings;

use crate::error::Error as EdgedError;

pub(crate) async fn image_garbage_collect(
    settings: Option<MIGCSettings>,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    migc_persistence: MIGCPersistence,
) -> Result<(), EdgedError> {
    log::info!("Starting MIGC...");

    // Run the GC once a day while waiting for any running task
    // let gc_period = std::time::Duration::from_secs(86400);

    // let mut gc_timer = tokio::time::interval(gc_period);
    // gc_timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);

    // TODO: fix unwrap
    let settings = settings.unwrap();

    loop {
        tokio::time::sleep(settings.time_between_cleanup()).await;

        if let Err(err) = garbage_collector(runtime, migc_persistence.clone()).await {
            return Err(EdgedError::new(format!("Error in MIGC: {}", err)));
        }
    }
}

async fn garbage_collector(
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    migc_persistence: MIGCPersistence,
) -> Result<(), EdgedError> {
    log::info!("Module Image Garbage Collection starting daily run");

    // track images associated with extant containers

    // first get list of containers on the device, running or otherwise
    let running_modules = ModuleRuntime::list_with_details(runtime).await.unwrap();

    let image_map = migc_persistence
        .prune_images_from_file(running_modules)
        .await;

    // delete images
    for key in image_map.keys() {
        let result = ModuleRegistry::remove(runtime, key).await;
        if result.is_err() {
            log::error!("Could not delete image {}", key);
        }
    }

    Ok(())
}
