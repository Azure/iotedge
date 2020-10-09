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

#[cfg(test)]
mod tests {
    use mqtt3::Event;
    use tokio::sync::{mpsc, mpsc::error::TryRecvError};

    use crate::bridge::{BridgeMessage, ConnectivityState};

    use super::ConnectivityHandler;

    #[tokio::test]
    async fn sends_connected_state() {
        let (connectivity_sender, mut connectivity_receiver) =
            mpsc::unbounded_channel::<BridgeMessage>();

        let mut ch = ConnectivityHandler::new(connectivity_sender);

        let _ = ch
            .handle_connectivity_event(Event::NewConnection {
                reset_session: true,
            })
            .await;

        let msg = connectivity_receiver.try_recv().unwrap();
        assert_eq!(
            msg,
            BridgeMessage::ConnectivityUpdate(ConnectivityState::Connected)
        );
        assert_eq!(ch.state, ConnectivityState::Connected);
    }

    #[tokio::test]
    async fn sends_disconnected_state() {
        let (connectivity_sender, mut connectivity_receiver) =
            mpsc::unbounded_channel::<BridgeMessage>();

        let mut ch = ConnectivityHandler::new(connectivity_sender);

        let _ = ch
            .handle_connectivity_event(Event::NewConnection {
                reset_session: true,
            })
            .await;

        let _ = ch
            .handle_connectivity_event(Event::Disconnected("reason".to_owned()))
            .await;

        let _msg = connectivity_receiver.try_recv().unwrap();
        let msg = connectivity_receiver.try_recv().unwrap();

        assert_eq!(
            msg,
            BridgeMessage::ConnectivityUpdate(ConnectivityState::Disconnected)
        );
        assert_eq!(ch.state, ConnectivityState::Disconnected);
    }

    #[tokio::test]
    async fn not_sends_connected_state_when_already_connected() {
        let (connectivity_sender, mut connectivity_receiver) =
            mpsc::unbounded_channel::<BridgeMessage>();

        let mut ch = ConnectivityHandler::new(connectivity_sender);

        let _ = ch
            .handle_connectivity_event(Event::NewConnection {
                reset_session: true,
            })
            .await;

        let _ = ch
            .handle_connectivity_event(Event::NewConnection {
                reset_session: true,
            })
            .await;

        let _msg = connectivity_receiver.try_recv().unwrap();
        let msg = connectivity_receiver.try_recv();
        assert_eq!(msg, Err(TryRecvError::Empty));
        assert_eq!(ch.state, ConnectivityState::Connected);
    }

    #[tokio::test]
    async fn not_sends_disconnected_state_when_already_disconnected() {
        let (connectivity_sender, mut connectivity_receiver) =
            mpsc::unbounded_channel::<BridgeMessage>();

        let mut ch = ConnectivityHandler::new(connectivity_sender);

        let _ = ch
            .handle_connectivity_event(Event::Disconnected("reason".to_owned()))
            .await;

        let msg = connectivity_receiver.try_recv();
        assert_eq!(msg, Err(TryRecvError::Empty));
        assert_eq!(ch.state, ConnectivityState::Disconnected);
    }

    async fn default_disconnected_state() {
        let (connectivity_sender, _) = mpsc::unbounded_channel::<BridgeMessage>();

        let ch = ConnectivityHandler::new(connectivity_sender);

        assert_eq!(ch.state, ConnectivityState::Disconnected);
    }
}
