use tracing::info;

use mqtt3::ReceivedPublication;
use mqtt_broker::{BrokerHandle, ClientEvent, ClientId, Message};

use crate::command::Command;

/// When executed, `DisconnectCommand` disconnects a client from the broker.
/// It is executed when `EdgeHub` sends a special packet to notify the broker
/// that a client needs to be disconnected for whatever reason (usually SAS/cert expired).
pub struct DisconnectCommand {
    broker_handle: BrokerHandle,
}

impl DisconnectCommand {
    pub fn new(broker_handle: &BrokerHandle) -> Self {
        Self {
            broker_handle: broker_handle.clone(),
        }
    }
}

impl Command for DisconnectCommand {
    type Error = Error;

    fn topic(&self) -> &str {
        super::DISCONNECT_TOPIC
    }

    fn handle(&mut self, publication: &ReceivedPublication) -> Result<(), Self::Error> {
        let client_id: ClientId =
            serde_json::from_slice(&publication.payload).map_err(Error::ParseClientId)?;

        info!("received disconnection request for client {}", client_id);

        let message = Message::Client(client_id.clone(), ClientEvent::DropConnection);
        self.broker_handle
            .send(message)
            .map_err(Error::DisconnectSignal)?;

        info!(
            "succeeded sending broker signal to disconnect client {}",
            client_id
        );
        Ok(())
    }
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("failed to parse client id from message payload: {0}")]
    ParseClientId(#[source] serde_json::Error),

    #[error("failed while sending broker signal to disconnect client: {0}")]
    DisconnectSignal(#[source] mqtt_broker::Error),
}
