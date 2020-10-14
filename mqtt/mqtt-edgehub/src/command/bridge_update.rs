use tracing::info;

use mqtt3::ReceivedPublication;
use mqtt_broker::{BrokerHandle, Message, SystemEvent};

use crate::{bridge::BridgeUpdate, command::Command};

const BRIDGE_UPDATE_TOPIC: &str = "$internal/bridge/settings";

/// `BridgeUpdateCommand` is executed when `EdgeHub` sends a special packet
/// to notify the broker that the bridge settings has changed.
pub struct BridgeUpdateCommand {
    broker_handle: BrokerHandle,
}

impl BridgeUpdateCommand {
    pub fn new(broker_handle: &BrokerHandle) -> Self {
        Self {
            broker_handle: broker_handle.clone(),
        }
    }
}

impl Command for BridgeUpdateCommand {
    type Error = Error;

    fn topic(&self) -> &str {
        BRIDGE_UPDATE_TOPIC
    }

    fn handle(&mut self, publication: &ReceivedPublication) -> Result<(), Self::Error> {
        info!("received bridge update from EdgeHub.");
        let identities: Vec<BridgeUpdate> =
            serde_json::from_slice(&publication.payload).map_err(Error::ParseBridgeUpdate)?;

        let message = Message::System(SystemEvent::AuthorizationUpdate(Box::new(identities)));
        self.broker_handle
            .send(message)
            .map_err(Error::SendBridgeUpdate)?;
        Ok(())
    }
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("failed to parse bridge update from message payload")]
    ParseBridgeUpdate(serde_json::Error),

    #[error("failed while sending bridge updates to the bridge controller")]
    SendBridgeUpdate(mqtt_broker::Error),
}
