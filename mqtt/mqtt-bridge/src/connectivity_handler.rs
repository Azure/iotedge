#![allow(dead_code)] // TODO remove when ready

use mqtt3::Event;
use tokio::sync::mpsc::UnboundedSender;
use tracing::{debug, info, warn};

use crate::bridge::BridgeError;

#[derive(Clone, Copy, Debug)]
pub enum ConnectivityState {
    Connected,
    Disconnecting,
    Disconnected,
}

/// Handles connection and disconnection events and sends a notification when status changes
pub struct ConnectivityHandler {
    state: ConnectivityState,
    sender: UnboundedSender<ConnectivityState>,
}

impl ConnectivityHandler {
    pub fn new(sender: UnboundedSender<ConnectivityState>) -> Self {
        ConnectivityHandler {
            state: ConnectivityState::Disconnected,
            sender,
        }
    }

    pub async fn handle_connectivity_event(&mut self, event: Event) -> Result<(), BridgeError> {
        match event {
            Event::Disconnected(reason) => {
                debug!("Received disconncted state {}", reason);
                match self.state {
                    ConnectivityState::Connected => self.state = ConnectivityState::Disconnecting,
                    ConnectivityState::Disconnecting => {
                        self.state = ConnectivityState::Disconnected;
                        self.sender
                            .send(self.state)
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
                ConnectivityState::Disconnecting => {
                    debug!("Status was disconnecting, now connected");
                    self.state = ConnectivityState::Connected
                }
                ConnectivityState::Disconnected => {
                    self.state = ConnectivityState::Connected;
                    self.sender
                        .send(self.state)
                        .map_err(BridgeError::SenderError)?;
                    info!("Sent connected state")
                }
            },
            _ => warn!("Can only handle connectivity event"),
        }

        Ok(())
    }
}
