use futures_util::future::{self, join_all};
use serde::{Deserialize, Serialize};
use thiserror::Error;
use tokio::sync::mpsc::{self, UnboundedSender};
use tracing::{error, info, info_span};
use tracing_futures::Instrument;

use crate::{bridge::Bridge, settings::BridgeSettings};

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
pub struct BridgeController {
    handle: BridgeControllerHandle,
}

impl BridgeController {
    pub fn new() -> Self {
        let (sender, _updates) = mpsc::unbounded_channel();
        let handle = BridgeControllerHandle { sender };

        Self { handle }
    }

    pub fn handle(&self) -> BridgeControllerHandle {
        self.handle.clone()
    }

    pub async fn run(self, system_address: String, device_id: String, settings: BridgeSettings) {
        info!("starting bridge controller...");

        let mut bridge_handles = vec![];
        if let Some(upstream_settings) = settings.upstream() {
            let upstream_settings = upstream_settings.clone();

            let upstream_bridge = async move {
                let bridge =
                    Bridge::new_upstream(system_address, device_id, upstream_settings.clone())
                        .await;

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
            .instrument(info_span!("bridge", name = "upstream"));

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

impl Default for BridgeController {
    fn default() -> Self {
        Self::new()
    }
}

#[derive(Clone, Debug)]
pub struct BridgeControllerHandle {
    sender: UnboundedSender<BridgeControllerUpdate>,
}

impl BridgeControllerHandle {
    pub fn send(&mut self, message: BridgeControllerUpdate) -> Result<(), Error> {
        self.sender
            .send(message)
            .map_err(Error::SendControllerMessage)
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub struct BridgeControllerUpdate {
    // TODO: add settings
}

#[derive(Debug, Error)]
pub enum Error {
    #[error("An error occurred sending a message to the controller.")]
    SendControllerMessage(#[source] tokio::sync::mpsc::error::SendError<BridgeControllerUpdate>),
}
