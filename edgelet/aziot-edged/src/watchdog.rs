// Copyright (c) Microsoft. All rights reserved.

use edgelet_core::ModuleRegistry;

use crate::error::Error as EdgedError;

pub(crate) async fn run_until_shutdown(
    settings: impl edgelet_settings::RuntimeSettings,
    runtime: impl edgelet_core::ModuleRuntime,
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
                if let Err(err) = watchdog(&settings, &runtime, &identity_client).await {
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

                return Ok(shutdown_reason);
            }
        }
    }
}

async fn watchdog(
    settings: &impl edgelet_settings::RuntimeSettings,
    runtime: &impl edgelet_core::ModuleRuntime,
    identity_client: &aziot_identity_client_async::Client,
) -> Result<(), EdgedError> {
    log::info!("Watchdog checking Edge runtime status");
    let agent_name = settings.agent().name();

    match runtime.get(agent_name).await {
        Ok((_, agent_status)) => {
            let agent_status = agent_status.status();

            if let edgelet_core::ModuleStatus::Running = agent_status {
                log::info!("Edge runtime is running");
            } else {
                log::info!(
                    "Edge runtime status is {}; starting runtime now...",
                    agent_status
                );

                runtime
                    .start(agent_name)
                    .await
                    .map_err(|err| EdgedError::from_err("Failed to start Edge runtime", err))?;
            }
        }

        Err(_) => {
            log::info!(
                "Creating and starting Edge runtime module {}...",
                agent_name
            );

            let gen_id = agent_gen_id(identity_client).await?;

            let mut agent_spec = settings.agent().clone();
            agent_spec
                .env_mut()
                .insert("IOTEDGE_MODULEGENERATIONID".to_string(), gen_id);

            // if let edgelet_settings::module::ImagePullPolicy::OnCreate =
            //     agent_spec.image_pull_policy()
            // {
            //     runtime
            //         .registry()
            //         .pull(agent_spec.config())
            //         .await
            //         .map_err(|err| {
            //             EdgedError::from_err("Failed to pull Edge runtime module", err)
            //         })?;
            // }

            // runtime
            //     .create(agent_spec)
            //     .await
            //     .map_err(|err| EdgedError::from_err("Failed to create Edge runtime module", err))?;

            runtime
                .start(agent_name)
                .await
                .map_err(|err| EdgedError::from_err("Failed to start Edge runtime", err))?;
        }
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
        if let Some(gen_id) = identity.gen_id {
            Ok(gen_id.0)
        } else {
            Err(EdgedError::new("$edgeAgent identity missing generation ID"))
        }
    } else {
        Err(EdgedError::new("Invalid identity type for $edgeAgent"))
    }
}
