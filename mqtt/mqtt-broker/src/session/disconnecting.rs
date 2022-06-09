use mqtt3::proto;

use crate::{ClientEvent, ClientInfo, ConnectionHandle, Error, Message};

#[derive(Debug)]
pub struct DisconnectingSession {
    client_info: ClientInfo,
    will: Option<proto::Publication>,
    handle: ConnectionHandle,
}

impl DisconnectingSession {
    pub fn new(
        client_info: ClientInfo,
        will: Option<proto::Publication>,
        handle: ConnectionHandle,
    ) -> Self {
        Self {
            client_info,
            will,
            handle,
        }
    }

    pub fn client_info(&self) -> &ClientInfo {
        &self.client_info
    }

    pub fn into_will(self) -> Option<proto::Publication> {
        self.will
    }

    pub fn send(&self, event: ClientEvent) -> Result<(), Error> {
        let message = Message::Client(self.client_info().client_id().clone(), event);
        self.handle.send(message)
    }
}
