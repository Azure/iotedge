use std::collections::HashMap;

use futures_util::future::{self, join_all};
use serde::{Deserialize, Serialize};
use thiserror::Error;
use tokio::sync::mpsc::{self, UnboundedSender};
use tracing::{error, info};

use crate::bridge::{Bridge, BridgeError};
use crate::settings::Settings;

/// Controller that handles the settings and monitors changes, spawns new Bridges and monitors shutdown signal.
pub struct BridgeController {
    bridges: HashMap<String, Bridge>,
    handle: BridgeControllerHandle,
}

impl BridgeController {
    pub fn new() -> Self {
        let (sender, _updates) = mpsc::unbounded_channel();
        let handle = BridgeControllerHandle { sender };

        Self {
            bridges: HashMap::new(),
            handle,
        }
    }

    pub fn handle(&self) -> BridgeControllerHandle {
        self.handle.clone()
    }

    pub async fn init(
        &mut self,
        system_address: String,
        device_id: &str,
    ) -> Result<(), BridgeError> {
        info!("initializing bridge controller...");
        let settings = Settings::new().map_err(BridgeError::LoadingSettings)?;

        if let Some(upstream) = settings.upstream() {
            let bridge = Bridge::new(system_address, device_id.into(), upstream.clone());
            self.bridges.insert(upstream.name().to_string(), bridge);
        } else {
            info!("No upstream settings detected. Not starting bridge controller.")
        };
        Ok(())
    }

    pub async fn run(self) {
        info!("starting bridge controller...");

        let mut bridge_handles = vec![];
        for (_, bridge) in self.bridges {
            let bridge_handle = tokio::spawn(async move { bridge.start().await });
            bridge_handles.push(bridge_handle);
        }

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
