#![allow(dead_code)] // TODO remove when ready

use mqtt3::Event;
use tokio::sync::mpsc::UnboundedSender;
use tracing::{debug, info, warn};

use crate::bridge::{BridgeError, BridgeMessage, ConnectivityState};

impl ToString for ConnectivityState {
    fn to_string(&self) -> String {
        match self {
            ConnectivityState::Connected => "Connected".to_string(),
            ConnectivityState::Disconnected => "Disconnected".to_string(),
        }
    }
}

/// Handles connection and disconnection events and sends a notification when status changes
pub struct ConnectivityHandler {
    state: ConnectivityState,
    sender: UnboundedSender<BridgeMessage>,
}

impl ConnectivityHandler {
    pub fn new(sender: UnboundedSender<BridgeMessage>) -> Self {
        ConnectivityHandler {
            state: ConnectivityState::Disconnected,
            sender,
        }
    }

    pub async fn handle_connectivity_event(&mut self, event: Event) -> Result<(), BridgeError> {
        match event {
            Event::Disconnected(reason) => {
                debug!("Received disconnected state {}", reason);
                match self.state {
                    ConnectivityState::Connected => {
                        self.state = ConnectivityState::Disconnected;
                        self.sender
                            .send(BridgeMessage::ConnectivityUpdate(
                                ConnectivityState::Disconnected,
                            ))
                            .map_err(BridgeError::SenderError)?;
                        info!("Sent disconnected state");
                    }
                    ConnectivityState::Disconnected => {
                        debug!("Already disconnected");
                    }
                }
            }
            Event::NewConnection { reset_session: _ } => match self.state {
                ConnectivityState::Connected => {
                    debug!("Already connected");
                }
                ConnectivityState::Disconnected => {
                    self.state = ConnectivityState::Connected;
                    self.sender
                        .send(BridgeMessage::ConnectivityUpdate(
                            ConnectivityState::Connected,
                        ))
                        .map_err(BridgeError::SenderError)?;
                    info!("Sent connected state")
                }
            },
            _ => warn!("Can only handle connectivity event"),
        }

        Ok(())
    }
}
