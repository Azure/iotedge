// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{ModuleRegistry, ModuleRuntime};
use edgelet_settings::RuntimeSettings;

use crate::error::Error as EdgedError;

pub(crate) async fn run_until_shutdown(
    settings: edgelet_settings::docker::Settings,
    device_info: &aziot_identity_common::AzureIoTSpec,
    runtime: edgelet_docker::DockerModuleRuntime,
    identity_client: &aziot_identity_client_async::Client,
    mut shutdown_rx: tokio::sync::mpsc::UnboundedReceiver<edgelet_core::ShutdownReason>,
) -> Result<edgelet_core::ShutdownReason, EdgedError> {
    // Run the watchdog every 60 seconds while waiting for any running task to send a
    // shutdown signal.
    let watchdog_period = std::time::Duration::from_secs(60);
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
            futures_util::future::Either::Left((_, shutdown)) => {
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

            futures_util::future::Either::Right((shutdown_reason, _)) => {
                let shutdown_reason = shutdown_reason.expect("shutdown channel closed");
                log::info!("{}", shutdown_reason);
                log::info!("Watchdog stopped");

                log::info!("Stopping all modules...");
                if let Err(err) = runtime
                    .stop_all(Some(std::time::Duration::from_secs(30)))
                    .await
                {
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
    settings: &edgelet_settings::docker::Settings,
    device_info: &aziot_identity_common::AzureIoTSpec,
    runtime: &edgelet_docker::DockerModuleRuntime,
    identity_client: &aziot_identity_client_async::Client,
) -> Result<(), EdgedError> {
    log::info!("Watchdog checking Edge runtime status");
    let agent_name = settings.agent().name();

    let start = if let Ok((_, agent_status)) = runtime.get(agent_name).await {
        let agent_status = agent_status.status();

        if let edgelet_core::ModuleStatus::Running = agent_status {
            log::info!("Edge runtime is running");

            false
        } else {
            log::info!(
                "Edge runtime status is {}; starting runtime now...",
                agent_status
            );

            true
        }
    } else {
        log::info!(
            "Creating and starting Edge runtime module {}...",
            agent_name
        );

        let mut agent_spec = settings.agent().clone();

        let gen_id = agent_gen_id(identity_client).await?;
        let mut env = agent_env(gen_id, settings, device_info);
        agent_spec.env_mut().append(&mut env);

        if let edgelet_settings::module::ImagePullPolicy::OnCreate = agent_spec.image_pull_policy()
        {
            runtime
                .registry()
                .pull(agent_spec.config())
                .await
                .map_err(|err| EdgedError::from_err("Failed to pull Edge runtime module", err))?;
        }

        runtime
            .create(agent_spec)
            .await
            .map_err(|err| EdgedError::from_err("Failed to create Edge runtime module", err))?;

        log::info!("Created Edge runtime module {}", agent_name);

        true
    };

    if start {
        runtime
            .start(agent_name)
            .await
            .map_err(|err| EdgedError::from_err("Failed to start Edge runtime", err))?;

        log::info!("Started Edge runtime module {}", agent_name);
    }

    Ok(())
}

async fn agent_gen_id(
    identity_client: &aziot_identity_client_async::Client,
) -> Result<String, EdgedError> {
    let identity = identity_client
        .update_module_identity("$edgeAgent")
        .await
        .map_err(|err| EdgedError::from_err("Failed to update $edgeAgent identity", err))?;

    if let aziot_identity_common::Identity::Aziot(identity) = identity {
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
    settings: &edgelet_settings::docker::Settings,
    device_info: &aziot_identity_common::AzureIoTSpec,
) -> std::collections::BTreeMap<String, String> {
    let mut env = std::collections::BTreeMap::new();

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
