// Copyright (c) Microsoft. All rights reserved.

use std::time::Duration;

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

    tokio::time::sleep(Duration::from_secs(5)).await;

    let settings = match settings {
        Some(parsed) => parsed,
        None => {
            return Err(EdgedError::new("Could not start Image auto-pruning task; contaier images will not be cleaned up automatically".to_string()));
        }
    };

    loop {
        if let Err(err) = garbage_collector(runtime, migc_persistence.clone()).await {
            return Err(EdgedError::new(format!(
                "Error in image auto-pruning task: {}",
                err
            )));
        }

        tokio::time::sleep(settings.time_between_cleanup()).await;
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
            let mut module_ids: Vec<String> = Vec::new();
            for module in modules {
                let id = edgelet_core::Module::config(&module.0)
                    .image_hash()
                    .ok_or(edgelet_docker::Error::GetImageHash())
                    .unwrap();
                module_ids.push(id.to_string());
            }
            module_ids
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
        .await
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
