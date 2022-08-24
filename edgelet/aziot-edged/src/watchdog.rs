// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::{Module, ModuleRuntime};
use edgelet_settings::RuntimeSettings;

use crate::error::Error as EdgedError;

pub(crate) async fn run_until_shutdown(
    settings: edgelet_settings::docker::Settings,
    device_info: &aziot_identity_common::AzureIoTSpec,
    runtime: edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    identity_client: &aziot_identity_client_async::Client,
    mut action_rx: tokio::sync::mpsc::UnboundedReceiver<edgelet_core::WatchdogAction>,
) -> Result<edgelet_core::WatchdogAction, EdgedError> {
    // Run the watchdog every 60 seconds while waiting for any running task to send a
    // watchdog action.
    let watchdog_period = std::time::Duration::from_secs(60);
    let watchdog_retries = settings.watchdog().max_retries();
    let mut watchdog_errors = 0;

    let mut watchdog_timer = tokio::time::interval(watchdog_period);
    watchdog_timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Delay);

    log::info!("Starting watchdog with 60 second period...");

    loop {
        let watchdog_next = watchdog_timer.tick();
        tokio::pin!(watchdog_next);

        let action_next = action_rx.recv();
        tokio::pin!(action_next);

        match futures_util::future::select(watchdog_next, action_next).await {
            futures_util::future::Either::Left((_, _)) => {
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
            }

            futures_util::future::Either::Right((action, _)) => {
                let action = action.expect("shutdown channel closed");
                log::info!("{}", action);

                if let edgelet_core::WatchdogAction::EdgeCaRenewal = action {
                    restart_modules(&settings, &runtime).await;
                } else {
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

                    return Ok(action);
                }
            }
        }
    }
}

async fn watchdog(
    settings: &edgelet_settings::docker::Settings,
    device_info: &aziot_identity_common::AzureIoTSpec,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    identity_client: &aziot_identity_client_async::Client,
) -> Result<(), EdgedError> {
    log::info!("Watchdog checking Edge runtime status");
    let agent_name = settings.agent().name();

    if let Ok((_, agent_status)) = runtime.get(agent_name).await {
        let agent_status = agent_status.status();

        match agent_status {
            edgelet_core::ModuleStatus::Running => {
                log::info!("Edge runtime is running");
            }

            edgelet_core::ModuleStatus::Stopped | edgelet_core::ModuleStatus::Failed => {
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

            edgelet_core::ModuleStatus::Dead | edgelet_core::ModuleStatus::Unknown => {
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

async fn restart_modules(
    settings: &edgelet_settings::docker::Settings,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
) {
    let agent_name = settings.agent().name();

    // Check if edgeAgent is running. If edgeAgent does not exist or is not running,
    // return and let the periodic watchdog create and start it.
    if let Ok((_, agent_status)) = runtime.get(agent_name).await {
        if agent_status.status() != &edgelet_core::ModuleStatus::Running {
            log::info!("Agent not running; skipping module restart");

            return;
        }
    } else {
        log::info!("Agent not found; skipping module restart");

        return;
    }

    // List and stop modules.
    let modules = if let Ok(modules) = runtime.list().await {
        modules
    } else {
        log::warn!("Failed to list modules");

        return;
    };

    if let Err(err) = runtime.stop_all(None).await {
        log::warn!("Edge CA renewal failed to stop modules: {}", err);

        return;
    }

    log::info!("Edge CA renewal stopped all modules");

    // Restart all modules. edgeAgent should be restarted last so that it does not
    // also attempt to start modules.
    for module in modules {
        let module_name = module.name();

        if module_name != agent_name {
            if let Err(err) = runtime.start(module_name).await {
                log::warn!("Edge CA renewal failed to restart {}: {}", module_name, err);
            } else {
                log::info!("Edge CA renewal restarted {}", module_name);
            }
        }
    }

    if let Err(err) = runtime.start(agent_name).await {
        log::warn!("Edge CA renewal failed to restart {}: {}", agent_name, err);
    } else {
        log::info!("Edge CA renewal restarted {}", agent_name);
    }
}

async fn create_and_start_agent(
    settings: &edgelet_settings::docker::Settings,
    device_info: &aziot_identity_common::AzureIoTSpec,
    runtime: &edgelet_docker::DockerModuleRuntime<http_common::Connector>,
    identity_client: &aziot_identity_client_async::Client,
) -> Result<(), EdgedError> {
    let agent_name = settings.agent().name();
    let mut agent_spec = settings.agent().clone();

    let gen_id = agent_gen_id(identity_client).await?;
    let mut env = agent_env(gen_id, settings, device_info);
    agent_spec.env_mut().append(&mut env);

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
        edgelet_http::ApiVersion::V2022_08_03.to_string(),
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
