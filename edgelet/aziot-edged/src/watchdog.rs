// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::time::Duration;

use aziot_identity_client_async::Client as IdentityClient;
use aziot_identity_common::{AzureIoTSpec, Identity};
use futures_util::future::Either;

use edgelet_core::{ModuleRuntime, ModuleStatus, ShutdownReason};
use edgelet_docker::DockerModuleRuntime;
use edgelet_settings::docker::Settings as DockerSettings;
use edgelet_settings::RuntimeSettings;

use crate::error::Error as EdgedError;

pub(crate) async fn run_until_shutdown(
    settings: DockerSettings,
    device_info: &AzureIoTSpec,
    runtime: DockerModuleRuntime,
    identity_client: &IdentityClient,
    mut shutdown_rx: tokio::sync::mpsc::UnboundedReceiver<ShutdownReason>,
) -> Result<ShutdownReason, EdgedError> {
    // Run the watchdog every 60 seconds while waiting for any running task to send a
    // shutdown signal.
    let watchdog_period = Duration::from_secs(60);
    let watchdog_retries = settings.watchdog().max_retries();
    let mut watchdog_errors = 0;

    let mut watchdog_timer = tokio::time::interval(watchdog_period);
    watchdog_timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);

    let shutdown_loop = shutdown_rx.recv();
    futures_util::pin_mut!(shutdown_loop);

    log::info!("Starting watchdog with 60 second period...");

    loop {
        let watchdog_next = watchdog_timer.tick();
        futures_util::pin_mut!(watchdog_next);

        match futures_util::future::select(watchdog_next, shutdown_loop).await {
            Either::Left((_, shutdown)) => {
                if let Err(err) = watchdog(&settings, device_info, &runtime, identity_client).await
                {
                    log::warn!("Error in watchdog: {}", err);

                    watchdog_errors += 1;

                    if watchdog_retries <= watchdog_errors {
                        return Err(EdgedError::new(
                            "Watchdog error count has exceeded allowed retries",
                        ));
                    }
                }

                shutdown_loop = shutdown;
            }

            Either::Right((shutdown_reason, _)) => {
                let shutdown_reason = shutdown_reason.expect("shutdown channel closed");
                log::info!("{}", shutdown_reason);
                log::info!("Watchdog stopped");

                log::info!("Stopping all modules...");
                if let Err(err) = runtime.stop_all(Some(Duration::from_secs(30))).await {
                    log::warn!("Failed to stop modules on shutdown: {}", err);
                } else {
                    log::info!("All modules stopped");
                }

                return Ok(shutdown_reason);
            }
        }
    }
}

async fn watchdog(
    settings: &DockerSettings,
    device_info: &AzureIoTSpec,
    runtime: &DockerModuleRuntime,
    identity_client: &IdentityClient,
) -> Result<(), EdgedError> {
    log::info!("Watchdog checking Edge runtime status");
    let agent_name = settings.agent().name();

    if let Ok((_, agent_status)) = runtime.get(agent_name).await {
        let agent_status = agent_status.status();

        match agent_status {
            ModuleStatus::Running => {
                log::info!("Edge runtime is running");
            }

            ModuleStatus::Stopped | ModuleStatus::Failed => {
                log::info!(
                    "Edge runtime status is {}, starting module now...",
                    agent_status
                );

                runtime
                    .start(agent_name)
                    .await
                    .map_err(|err| EdgedError::from_err("Failed to start Edge runtime", err))?;

                log::info!("Started Edge runtime module {}", agent_name);
            }

            ModuleStatus::Dead | ModuleStatus::Unknown => {
                log::info!(
                    "Edge runtime status is {}, removing and recreating module...",
                    agent_status
                );

                runtime
                    .remove(agent_name)
                    .await
                    .map_err(|err| EdgedError::from_err("Failed to remove Edge runtime", err))?;

                create_and_start_agent(settings, device_info, runtime, identity_client).await?;
            }
        }
    } else {
        create_and_start_agent(settings, device_info, runtime, identity_client).await?;
    }

    Ok(())
}

async fn create_and_start_agent(
    settings: &DockerSettings,
    device_info: &AzureIoTSpec,
    runtime: &DockerModuleRuntime,
    identity_client: &IdentityClient,
) -> Result<(), EdgedError> {
    let agent_name = settings.agent().name();
    let mut agent_spec = settings.agent().clone();

    let gen_id = agent_gen_id(identity_client).await?;
    let mut env = agent_env(gen_id, settings, device_info);
    agent_spec.env_mut().append(&mut env);

    let pinfo = if let Some(path) = settings.product_info() {
        Ok(
            crate::product_info::ProductInfo::try_load(path).map_err(|err| {
                EdgedError::from_err(
                    format!("malformed product info at path {}", path.display()),
                    err,
                )
            })?,
        )
    } else {
        crate::product_info::ProductInfo::from_system()
    };

    match pinfo {
        Ok(pinfo) => {
            let prev = agent_spec
                .env_mut()
                .insert("IOTEDGE_PRODUCTINFO".to_owned(), pinfo.to_string());

            if prev.is_some() {
                log::warn!("overrode old IOTEDGE_PRODUCTINFO environment variable");
            }
        }
        Err(err) => {
            log::warn!("failed to infer system product info: {}", err);
        }
    }

    log::info!(
        "Creating and starting Edge runtime module {}...",
        agent_name
    );

    if let edgelet_settings::module::ImagePullPolicy::OnCreate = agent_spec.image_pull_policy() {
        edgelet_core::ModuleRegistry::pull(runtime.registry(), agent_spec.config())
            .await
            .map_err(|err| EdgedError::from_err("Failed to pull Edge runtime module", err))?;
    }

    runtime
        .create(agent_spec)
        .await
        .map_err(|err| EdgedError::from_err("Failed to create Edge runtime module", err))?;

    log::info!("Created Edge runtime module {}", agent_name);

    runtime
        .start(agent_name)
        .await
        .map_err(|err| EdgedError::from_err("Failed to start Edge runtime", err))?;

    log::info!("Started Edge runtime module {}", agent_name);

    Ok(())
}

async fn agent_gen_id(identity_client: &IdentityClient) -> Result<String, EdgedError> {
    let identity = identity_client
        .update_module_identity("$edgeAgent")
        .await
        .map_err(|err| EdgedError::from_err("Failed to update $edgeAgent identity", err))?;

    if let Identity::Aziot(identity) = identity {
        identity.gen_id.map_or_else(
            || Err(EdgedError::new("$edgeAgent identity missing generation ID")),
            |gen_id| Ok(gen_id.0),
        )
    } else {
        Err(EdgedError::new("Invalid identity type for $edgeAgent"))
    }
}

fn agent_env(
    gen_id: String,
    settings: &DockerSettings,
    device_info: &AzureIoTSpec,
) -> BTreeMap<String, String> {
    let mut env = BTreeMap::new();

    env.insert(
        "EdgeDeviceHostName".to_string(),
        settings.hostname().to_string(),
    );

    env.insert(
        "IOTEDGE_APIVERSION".to_string(),
        format!("{}", edgelet_http::ApiVersion::V2020_10_10),
    );

    env.insert("IOTEDGE_AUTHSCHEME".to_string(), "sasToken".to_string());

    env.insert(
        "IOTEDGE_DEVICEID".to_string(),
        device_info.device_id.0.clone(),
    );

    env.insert(
        "IOTEDGE_IOTHUBHOSTNAME".to_string(),
        device_info.hub_name.clone(),
    );

    if device_info.gateway_host.to_lowercase() != device_info.hub_name.to_lowercase() {
        env.insert(
            "IOTEDGE_GATEWAYHOSTNAME".to_string(),
            device_info.gateway_host.clone(),
        );
    }

    env.insert("IOTEDGE_MODULEID".to_string(), "$edgeAgent".to_string());
    env.insert("IOTEDGE_MODULEGENERATIONID".to_string(), gen_id);

    let (workload_uri, management_uri) = (
        settings.connect().workload_uri().to_string(),
        settings.connect().management_uri().to_string(),
    );
    let workload_mnt_uri = {
        // Home directory was used before this function was called, so it should be valid.
        let mut path = settings
            .homedir()
            .canonicalize()
            .expect("Invalid homedir path");

        path.push("mnt");

        let path = path.to_str().expect("invalid path");

        format!("unix://{}", path)
    };

    env.insert("IOTEDGE_WORKLOADURI".to_string(), workload_uri);
    env.insert("IOTEDGE_MANAGEMENTURI".to_string(), management_uri);
    env.insert(
        "IOTEDGE_WORKLOADLISTEN_MNTURI".to_string(),
        workload_mnt_uri,
    );

    env.insert("Mode".to_string(), "iotedged".to_string());

    env
}
