// Copyright (c) Microsoft. All rights reserved.

use crate::error::Error as EdgedError;

pub(crate) async fn run_until_shutdown(
    settings: &impl edgelet_settings::RuntimeSettings,
    mut shutdown_rx: tokio::sync::mpsc::UnboundedReceiver<edgelet_core::ShutdownReason>,
) -> Result<(), EdgedError> {
    // Run the watchdog every 60 seconds while waiting for any running task to send a
    // shutdown signal.
    let watchdog_period = std::time::Duration::from_secs(5);
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
                if let Err(err) = watchdog() {
                    log::warn!("Error in watchdog: {}", err);

                    watchdog_errors += 1;

                    if watchdog_retries <= watchdog_errors {
                        return Err(EdgedError::new("Watchdog error count has exceeded allowed retries"));
                    }
                }

                shutdown_loop = shutdown;
            }

            futures_util::future::Either::Right((shutdown_reason, _)) => {
                let shutdown_reason = shutdown_reason.expect("shutdown channel closed");
                log::info!("{}", shutdown_reason);

                return Ok(());
            }
        }
    }
}

fn watchdog() -> Result<(), EdgedError> {
    todo!()
}

fn shutdown() {}
