use tracing::info;

use mqtt3::ReceivedPublication;
use mqtt_broker::{BrokerHandle, Message, SystemEvent};

use crate::{auth::AuthorizerUpdate, command::Command};

/// `AuthorizedIdentitiesCommand` is executed when `EdgeHub` sends a special packet
/// to notify the broker that the list of authorized `IoTHub` identities has changed.
/// That can happen by several reasons:
/// - devices added/removed/updated;
/// - nested edge hierarchy changed.
///
/// If the list of identities has changed, we need to update authorizer
/// in the broker, that's what this command is doing.
pub struct AuthorizedIdentitiesCommand {
    broker_handle: BrokerHandle,
}

impl AuthorizedIdentitiesCommand {
    pub fn new(broker_handle: &BrokerHandle) -> Self {
        Self {
            broker_handle: broker_handle.clone(),
        }
    }
}

impl Command for AuthorizedIdentitiesCommand {
    type Error = Error;

    fn topic(&self) -> &str {
        super::AUTHORIZED_IDENTITIES_TOPIC
    }

    fn handle(&mut self, publication: &ReceivedPublication) -> Result<(), Self::Error> {
        info!("received authorized identities from EdgeHub.");
        let update: AuthorizerUpdate = serde_json::from_slice(&publication.payload)
            .map_err(Error::ParseAuthorizedIdentities)?;

        let message = Message::System(SystemEvent::AuthorizationUpdate(Box::new(update)));
        self.broker_handle
            .send(message)
            .map_err(Error::SendAuthorizedIdentitiesToBroker)?;

        Ok(())
    }
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("failed to parse authorized identities from message payload: {0}")]
    ParseAuthorizedIdentities(#[source] serde_json::Error),

    #[error("failed while sending authorized identities to broker: {0}")]
    SendAuthorizedIdentitiesToBroker(#[source] mqtt_broker::Error),
}
