use serde_json::error::Error as SerdeError;
use tracing::{info};

use mqtt3::ReceivedPublication;
use mqtt_broker::{BrokerHandle, ClientId, Error, Message, ServiceIdentity, SystemEvent};

pub struct Command {
    pub topic: String,
    pub handle: fn(&mut BrokerHandle, &ReceivedPublication) -> Result<(), HandleEventError>,
}

pub fn handle_disconnect(
    broker_handle: &mut BrokerHandle,
    publication: &ReceivedPublication,
) -> Result<(), HandleEventError> {
    let client_id: ClientId =
        serde_json::from_slice(&publication.payload).map_err(HandleEventError::ParseClientId)?;

    info!("received disconnection request for client {}", client_id);

    if let Err(e) = broker_handle.send(Message::System(SystemEvent::ForceClientDisconnect(
        client_id.clone(),
    ))) {
        return Err(HandleEventError::DisconnectSignal(e));
    }

    info!(
        "succeeded sending broker signal to disconnect client{}",
        client_id
    );
    Ok(())
}

pub fn handle_authorized_identities(
    broker_handle: &mut BrokerHandle,
    publication: &ReceivedPublication,
) -> Result<(), HandleEventError> {
    let array: Vec<ServiceIdentity> =
        serde_json::from_slice(&publication.payload).map_err(HandleEventError::ParseClientId)?;
    if let Err(e) = broker_handle.send(Message::System(SystemEvent::IdentityScopesUpdate(array))) {
        return Err(HandleEventError::SendAuthorizedIdentitiesToBroker(e));
    }

    info!("succeeded sending authorized identity scopes to broker",);
    Ok(())
}

#[derive(Debug, thiserror::Error)]
pub enum HandleEventError {
    #[error("failed to parse client id from message payload")]
    ParseClientId(#[from] SerdeError),

    #[error("failed while sending authorized identities to broker")]
    SendAuthorizedIdentitiesToBroker(Error),

    #[error("failed sending broker signal to disconnect client")]
    DisconnectSignal(Error),
}
