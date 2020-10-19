use tracing::info;

use mqtt3::ReceivedPublication;
use mqtt_broker::{BrokerHandle, Message, SystemEvent};

use crate::{auth::PolicyUpdate, command::Command};

const POLICY_UPDATE_TOPIC: &str = "$internal/authorization/policy";

/// `PolicyUpdateCommand` is executed when `EdgeHub` sends a special packet
/// to notify the broker that the customer authorization policy has changed,
/// and that we need to update the authorizer in the broker.
pub struct PolicyUpdateCommand {
    broker_handle: BrokerHandle,
}

impl PolicyUpdateCommand {
    pub fn new(broker_handle: &BrokerHandle) -> Self {
        Self {
            broker_handle: broker_handle.clone(),
        }
    }
}

impl Command for PolicyUpdateCommand {
    type Error = Error;

    fn topic(&self) -> &str {
        POLICY_UPDATE_TOPIC
    }

    fn handle(&mut self, publication: &ReceivedPublication) -> Result<(), Self::Error> {
        info!("received policy update from EdgeHub.");
        let identities: PolicyUpdate =
            serde_json::from_slice(&publication.payload).map_err(Error::ParsePolicyUpdate)?;

        let message = Message::System(SystemEvent::AuthorizationUpdate(Box::new(identities)));
        self.broker_handle
            .send(message)
            .map_err(Error::SendPolicyUpdate)?;
        Ok(())
    }
}

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("failed to parse policy update from message payload")]
    ParsePolicyUpdate(serde_json::Error),

    #[error("failed while sending policy updates to the broker")]
    SendPolicyUpdate(mqtt_broker::Error),
}
