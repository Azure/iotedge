use futures_util::future::{self, join_all};
use tracing::{error, info, info_span};
use tracing_futures::Instrument;

use crate::{bridge::Bridge, settings::BridgeSettings};

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
pub struct BridgeController {}

impl BridgeController {
    pub async fn run(system_address: String, device_id: String, settings: BridgeSettings) {
        info!("starting bridge controller...");

        let mut bridge_handles = vec![];
        if let Some(upstream_settings) = settings.upstream() {
            let upstream_settings = upstream_settings.clone();

            let span = info_span!("upstream bridge");
            let upstream_bridge = async move {
                let bridge =
                    Bridge::new(system_address, device_id, upstream_settings.clone()).await;

                match bridge {
                    Ok(bridge) => {
                        if let Err(e) = bridge.run().await {
                            error!(err = %e, "failed running {} bridge", upstream_settings.name());
                        }
                    }
                    Err(e) => {
                        error!(err = %e, "failed to create {} bridge", upstream_settings.name());
                    }
                };
            }
            .instrument(span);

            bridge_handles.push(upstream_bridge);
        } else {
            info!("No upstream settings detected. Not starting bridge controller.")
        };

        // join_all is fine because the bridge shouldn't fail and exit
        // if a pump in the bridge fails, it should internally recreate it
        // this means that if a bridge stops, then shutdown was triggered
        join_all(bridge_handles).await;

        // TODO: bridge controller will eventually listen for updates via the twin
        //       until this is complete we need to wait here indefinitely
        //       if we stop the bridge controller, our startup/shutdown logic will shut eveything down
        future::pending::<()>().await;
    }
}
