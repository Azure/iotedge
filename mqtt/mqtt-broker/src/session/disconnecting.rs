use mqtt3::proto;
use mqtt_broker_core::ClientInfo;

use crate::{ClientEvent, ClientId, ConnectionHandle, Error, Message};

#[derive(Debug)]
pub struct DisconnectingSession {
    client_info: ClientInfo,
    client_id: ClientId,
    will: Option<proto::Publication>,
    handle: ConnectionHandle,
}

impl DisconnectingSession {
    pub fn new(
        client_id: ClientId,
        client_info: ClientInfo,
        will: Option<proto::Publication>,
        handle: ConnectionHandle,
    ) -> Self {
        Self {
            client_id,
            client_info,
            will,
            handle,
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_id
    }

    pub fn client_info(&self) -> &ClientInfo {
        &self.client_info
    }

    pub fn into_will(self) -> Option<proto::Publication> {
        self.will
    }

    pub fn send(&mut self, event: ClientEvent) -> Result<(), Error> {
        let message = Message::Client(self.client_id.clone(), event);
        self.handle.send(message)
    }
}
