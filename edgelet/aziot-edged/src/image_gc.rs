// Copyright (c) Microsoft. All rights reserved.

use crate::error::Error as EdgedError;
use edgelet_core::ModuleRegistry;
use edgelet_settings::RuntimeSettings;

pub(crate) const BOOTSTRAP_LABEL: &str = "$bootstrap";

pub(crate) fn make_filter(
    expect_bootstrap: bool,
    until: impl std::fmt::Display,
) -> serde_json::Value {
    serde_json::json!({
        "label": [{
            BOOTSTRAP_LABEL: expect_bootstrap
        }],
        "until": until.to_string()
    })
}

pub(crate) async fn image_garbage_collect(
    settings: edgelet_settings::Settings,
    runtime: edgelet_docker::DockerModuleRuntime<http_common::Connector>,
) -> Result<(), EdgedError> {
    log::info!("Starting image auto-pruning task...");

    let gc_settings = settings.image_garbage_collection();
    let interval = gc_settings.cleanup_recurrence();
    // NOTE: Use Go duration format so Docker uses relative offset for "until".
    let threshold_string = format!("{}s", gc_settings.image_age_cleanup_threshold().as_secs());
    let filter_string = make_filter(false, &threshold_string);
    let mut timer =
        tokio::time::interval_at((std::time::Instant::now() + interval).into(), interval);
    timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);
    loop {
        timer.tick().await;
        match runtime.prune(&filter_string).await {
            Ok(images) => log::debug!("Pruned images: {:?}", images),
            Err(e) => log::error!("Error pruning unused images: {}", e),
        }
    }
}

#[cfg(test)]
mod tests {}
