use futures_util::future::{self, join_all};
use tokio::task;
use tracing::{error, info};

use crate::bridge::Bridge;
use crate::settings::BridgeSettings;

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
pub struct BridgeController {}

impl BridgeController {
    pub async fn run(system_address: String, device_id: String, settings: BridgeSettings) {
        info!("starting bridge controller...");

        let mut bridge_handles = vec![];
        if let Some(upstream_settings) = settings.upstream() {
            let upstream_settings = upstream_settings.clone();
            let upstream_bridge = async move {
                let bridge =
                    Bridge::new(system_address, device_id.into(), upstream_settings.clone()).await;

                // TODO PRE: find way to parameterize logs
                match bridge {
                    Ok(bridge) => {
                        if let Err(e) = bridge.run().await {
                            error!(message = "failed running upstream bridge", err = %e);
                        }
                    }
                    Err(e) => {
                        error!(message = "failed to create upstream bridge", err = %e);
                    }
                };
            };

            let upstream_bridge_handle = task::spawn_local(upstream_bridge);
            bridge_handles.push(upstream_bridge_handle);
        } else {
            info!("No upstream settings detected. Not starting bridge controller.")
        };

        let bridges_status = join_all(bridge_handles).await;
        for status in bridges_status {
            if let Err(e) = status {
                error!(message = "error while running bridge", err = %e);
            }
        }

        // TODO: bridge controller will eventually listen for updates via the twin
        //       until this is complete we need to wait here indefinitely
        //       if we stop the bridge controller, our startup/shutdown logic will shut eveything down
        future::pending::<()>().await;
    }
}
