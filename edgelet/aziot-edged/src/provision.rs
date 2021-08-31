// Copyright (c) Microsoft. All rights reserved.

use sha2::Digest;

use crate::error::Error as EdgedError;

pub(crate) fn identity_client(
    settings: &impl edgelet_settings::RuntimeSettings,
) -> Result<aziot_identity_client_async::Client, EdgedError> {
    let identity_connector =
        http_common::Connector::new(settings.endpoints().aziot_identityd_url())
            .map_err(|err| EdgedError::from_err("Invalid Identity Service URL", err))?;

    let identity_client = aziot_identity_client_async::Client::new(
        aziot_identity_common_http::ApiVersion::V2020_09_01,
        identity_connector,
    );

    Ok(identity_client)
}

pub(crate) async fn get_device_info(
    identity_client: &aziot_identity_client_async::Client,
    auto_reprovisioning_mode: edgelet_settings::aziot::AutoReprovisioningMode,
    cache_dir: &std::path::Path,
) -> Result<aziot_identity_common::AzureIoTSpec, EdgedError> {
    if let edgelet_settings::aziot::AutoReprovisioningMode::AlwaysOnStartup =
        auto_reprovisioning_mode
    {
        reprovision(identity_client, cache_dir)
            .await
            .map_err(|err| EdgedError::from_err("Reprovision on startup failed: {}", err))?;
    }

    loop {
        log::info!("Obtaining Edge device provisioning data...");

        match identity_client.get_device_identity().await {
            Ok(device_info) => match device_info {
                aziot_identity_common::Identity::Aziot(device_info) => {
                    log::info!(
                        "Device is {} on {}",
                        device_info.device_id.0,
                        device_info.hub_name
                    );

                    return Ok(device_info);
                }
                aziot_identity_common::Identity::Local(..) => {
                    // Identity Service should never return an invalid device identity.
                    // Treat this as a fatal error.
                    return Err(EdgedError::new("Invalid device identity"));
                }
            },
            Err(err) => {
                log::error!("Failed to obtain device identity: {}", err);

                // Reprovision device since device identity is not available.
                log::info!("Requesting device reprovision");

                if let Err(err) = reprovision(identity_client, cache_dir).await {
                    log::warn!("Failed to reprovision: {}", err);
                }

                tokio::time::sleep(std::time::Duration::from_secs(5)).await;
            }
        }
    }
}

pub(crate) async fn update_device_cache(
    cache_dir: &std::path::Path,
    device_info: &aziot_identity_common::AzureIoTSpec,
    runtime: &impl edgelet_core::ModuleRuntime,
) -> Result<(), EdgedError> {
    log::info!("Detecting if device information has changed...");

    let cache_path = cache_dir.join("provisioning_state");

    let cached_device = match std::fs::read_to_string(cache_path.clone()) {
        Ok(cache) => cache,
        Err(err) => match err.kind() {
            std::io::ErrorKind::NotFound => String::default(),
            _ => {
                return Err(EdgedError::from_err(
                    "Failed to read cached provisioning state",
                    err,
                ));
            }
        },
    };

    let current_device = device_digest(device_info);

    if current_device == cached_device {
        log::info!("Device information has not changed");
    } else {
        log::info!("Change to device information detected");

        log::info!("Removing all modules...");
        runtime
            .remove_all()
            .await
            .map_err(|err| EdgedError::from_err("Failed to remove old runtime modules", err))?;
        log::info!("Removed all modules");

        log::info!("Updating cached device information");
        std::fs::write(cache_path, current_device)
            .map_err(|err| EdgedError::from_err("Failed to save provisioning cache", err))?;
    }

    Ok(())
}

pub(crate) async fn reprovision(
    identity_client: &aziot_identity_client_async::Client,
    cache_dir: &std::path::Path,
) -> Result<(), std::io::Error> {
    if let Err(err) = std::fs::remove_dir_all(cache_dir) {
        log::warn!(
            "Failed to clear provisioning cache before reprovision: {}",
            err
        );
    }

    identity_client.reprovision().await
}

fn device_digest(device: &aziot_identity_common::AzureIoTSpec) -> String {
    let json = serde_json::json!({
        "device_id": device.device_id.0,
        "gateway_host_name": device.gateway_host,
        "hub_name": device.hub_name,
    })
    .to_string();

    let digest = sha2::Sha256::digest(json.as_bytes());

    base64::encode(digest)
}
