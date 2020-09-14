use tracing::info;

use mqtt3::ReceivedPublication;
use mqtt_broker::{BrokerHandle, ClientId, Message, SystemEvent};

use crate::command::Command;

const DISCONNECT_TOPIC: &str = "$edgehub/disconnect";

pub struct Disconnect {
    broker_handle: BrokerHandle,
}

impl Disconnect {
    pub fn new(broker_handle: &BrokerHandle) -> Self {
        Self {
            broker_handle: broker_handle.clone(),
        }
    }
}

impl Command for Disconnect {
    type Error = Error;

    fn topic(&self) -> &str {
        DISCONNECT_TOPIC
    }

    fn handle(&mut self, publication: &ReceivedPublication) -> Result<(), Self::Error> {
        let client_id: ClientId =
            serde_json::from_slice(&publication.payload).map_err(Error::ParseClientId)?;

        info!("received disconnection request for client {}", client_id);

        let message = Message::System(SystemEvent::ForceClientDisconnect(client_id.clone()));
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
    #[error("failed to parse client id from message payload")]
    ParseClientId(serde_json::Error),

    #[error("failed while sending broker signal to disconnect client")]
    DisconnectSignal(mqtt_broker::Error),
}
