use tracing::info;

use mqtt3::ReceivedPublication;

use mqtt_bridge::{BridgeControllerHandle, BridgeControllerUpdate};

use crate::command::Command;

const BRIDGE_UPDATE_TOPIC: &str = "$internal/bridge/settings";

/// `BridgeUpdateCommand` is executed when `EdgeHub` sends a special packet
/// to notify the broker that the bridge settings has changed.
pub struct BridgeUpdateCommand {
    controller_handle: BridgeControllerHandle,
}

impl BridgeUpdateCommand {
    pub fn new(controller_handle: BridgeControllerHandle) -> Self {
        Self { controller_handle }
    }
}

impl Command for BridgeUpdateCommand {
    type Error = Error;

    fn topic(&self) -> &str {
        BRIDGE_UPDATE_TOPIC
    }

    fn handle(&mut self, publication: &ReceivedPublication) -> Result<(), Self::Error> {
        info!("received bridge update from EdgeHub.");
        let update: BridgeControllerUpdate =
            serde_json::from_slice(&publication.payload).map_err(Error::ParseBridgeUpdate)?;

        self.controller_handle
            .send_update(update)
            .map_err(Error::SendBridgeUpdate)?;
        Ok(())
    }
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("failed to parse bridge update from message payload: {0}")]
    ParseBridgeUpdate(#[source] serde_json::Error),

    #[error("failed while sending bridge updates to bridge controller: {0}")]
    SendBridgeUpdate(#[source] mqtt_bridge::Error),
}
