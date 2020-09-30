use tracing::{debug, info};

use mqtt3::ReceivedPublication;
use mqtt_broker::{BrokerHandle, Message, SystemEvent};

use crate::{auth::ServiceIdentity, command::Command};

const AUTHORIZED_IDENTITIES_TOPIC: &str = "$internal/identities";

pub struct AuthorizedIdentities {
    broker_handle: BrokerHandle,
}

impl AuthorizedIdentities {
    pub fn new(broker_handle: &BrokerHandle) -> Self {
        Self {
            broker_handle: broker_handle.clone(),
        }
    }
}

impl Command for AuthorizedIdentities {
    type Error = Error;

    fn topic(&self) -> &str {
        AUTHORIZED_IDENTITIES_TOPIC
    }

    fn handle(&mut self, publication: &ReceivedPublication) -> Result<(), Self::Error> {
        info!("received authorized identities from edgeHub.");
        let identities: Vec<ServiceIdentity> = serde_json::from_slice(&publication.payload)
            .map_err(Error::ParseAuthorizedIdentities)?;

        debug!("authorized identities: {:?}.", identities);

        let message = Message::System(SystemEvent::AuthorizationUpdate(Box::new(identities)));
        self.broker_handle
            .send(message)
            .map_err(Error::SendAuthorizedIdentitiesToBroker)?;

        info!("succeeded sending authorized identity scopes to broker",);
        Ok(())
    }
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("failed to parse authorized identities from message payload")]
    ParseAuthorizedIdentities(serde_json::Error),

    #[error("failed while sending authorized identities to broker")]
    SendAuthorizedIdentitiesToBroker(mqtt_broker::Error),
}
