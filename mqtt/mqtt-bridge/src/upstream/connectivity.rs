use std::fmt::{Display, Formatter, Result as FmtResult};

use async_trait::async_trait;
use serde::Serialize;
use tracing::{debug, info};

use mqtt3::Event;

use crate::{
    client::{Handled, MqttEventHandler},
    pump::{PumpError, PumpHandle, PumpMessage},
};

use super::LocalUpstreamPumpEvent;

#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
pub enum ConnectivityState {
    Connected,
    Disconnected,
}

impl Display for ConnectivityState {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            Self::Connected => write!(f, "Connected"),
            Self::Disconnected => write!(f, "Disconnected"),
        }
    }
}

/// Handles connection and disconnection events and sends a notification when status changes
pub struct ConnectivityMqttEventHandler {
    state: ConnectivityState,
    sender: PumpHandle<LocalUpstreamPumpEvent>,
}

impl ConnectivityMqttEventHandler {
    pub fn new(sender: PumpHandle<LocalUpstreamPumpEvent>) -> Self {
        ConnectivityMqttEventHandler {
            state: ConnectivityState::Disconnected,
            sender,
        }
    }
}

#[async_trait]
impl MqttEventHandler for ConnectivityMqttEventHandler {
    type Error = ConnectivityError;

    async fn handle(&mut self, event: Event) -> Result<Handled, Self::Error> {
        let event = match event {
            Event::Disconnected(reason) => {
                debug!("received disconnected state {}", reason);
                match self.state {
                    ConnectivityState::Connected => {
                        self.state = ConnectivityState::Disconnected;

                        let event = LocalUpstreamPumpEvent::ConnectivityUpdate(
                            ConnectivityState::Disconnected,
                        );
                        let msg = PumpMessage::Event(event);
                        self.sender.send(msg).await?;

                        info!("sent disconnected state");
                    }
                    ConnectivityState::Disconnected => {
                        debug!("already disconnected");
                    }
                }

                return Ok(Handled::Fully);
            }

            Event::NewConnection { reset_session: _ } => {
                match self.state {
                    ConnectivityState::Connected => {
                        debug!("already connected");
                    }
                    ConnectivityState::Disconnected => {
                        self.state = ConnectivityState::Connected;

                        let event = LocalUpstreamPumpEvent::ConnectivityUpdate(
                            ConnectivityState::Connected,
                        );
                        let msg = PumpMessage::Event(event);
                        self.sender.send(msg).await?;

                        info!("sent connected state")
                    }
                }
                return Ok(Handled::Fully);
            }

            event => event,
        };

        Ok(Handled::Skipped(event))
    }
}

#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct ConnectivityError(#[from] PumpError);

#[cfg(test)]
mod tests {
    use futures_util::FutureExt;
    use matches::assert_matches;
    use mqtt3::{proto::QoS, proto::SubscribeTo, ConnectionError, Event, SubscriptionUpdateEvent};

    use crate::{
        client::Handled,
        pump::{self, PumpMessage},
    };

    use super::*;

    #[tokio::test]
    async fn sends_connected_state() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let mut ch = ConnectivityMqttEventHandler::new(handle);
        let event = Event::NewConnection {
            reset_session: true,
        };
        let res = ch.handle(event).await.unwrap();

        let msg = connectivity_receiver.recv().await.unwrap();
        assert_eq!(
            msg,
            PumpMessage::Event(LocalUpstreamPumpEvent::ConnectivityUpdate(
                ConnectivityState::Connected
            ))
        );
        assert_eq!(ch.state, ConnectivityState::Connected);
        assert_eq!(res, Handled::Fully);
    }

    #[tokio::test]
    async fn sends_disconnected_state() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let mut ch = ConnectivityMqttEventHandler::new(handle);

        let res_connected = ch
            .handle(Event::NewConnection {
                reset_session: true,
            })
            .await
            .unwrap();
        let _msg = connectivity_receiver.recv().await.unwrap();

        let res_disconnected = ch
            .handle(Event::Disconnected(ConnectionError::ServerClosedConnection))
            .await
            .unwrap();

        let msg = connectivity_receiver.recv().await.unwrap();

        assert_eq!(
            msg,
            PumpMessage::Event(LocalUpstreamPumpEvent::ConnectivityUpdate(
                ConnectivityState::Disconnected
            ))
        );
        assert_eq!(ch.state, ConnectivityState::Disconnected);
        assert_eq!(res_connected, Handled::Fully);
        assert_eq!(res_disconnected, Handled::Fully);
    }

    #[tokio::test]
    async fn not_sends_connected_state_when_already_connected() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let mut ch = ConnectivityMqttEventHandler::new(handle);

        let res_connected1 = ch
            .handle(Event::NewConnection {
                reset_session: true,
            })
            .await
            .unwrap();

        let res_connected2 = ch
            .handle(Event::NewConnection {
                reset_session: true,
            })
            .await
            .unwrap();

        let _msg = connectivity_receiver.recv().await.unwrap();
        assert!(connectivity_receiver.recv().now_or_never().is_none());
        assert_eq!(ch.state, ConnectivityState::Connected);
        assert_eq!(res_connected1, Handled::Fully);
        assert_eq!(res_connected2, Handled::Fully);
    }

    #[tokio::test]
    async fn not_sends_disconnected_state_when_already_disconnected() {
        let (handle, mut connectivity_receiver) = pump::channel();

        let mut ch = ConnectivityMqttEventHandler::new(handle);

        let res_disconnected = ch
            .handle(Event::Disconnected(ConnectionError::ServerClosedConnection))
            .await
            .unwrap();

        assert!(connectivity_receiver.recv().now_or_never().is_none());
        assert_eq!(ch.state, ConnectivityState::Disconnected);
        assert_eq!(res_disconnected, Handled::Fully)
    }

    #[tokio::test]
    async fn not_handles_other_events() {
        let (handle, _) = pump::channel();

        let mut ch = ConnectivityMqttEventHandler::new(handle);

        let event =
            Event::SubscriptionUpdates(vec![SubscriptionUpdateEvent::Subscribe(SubscribeTo {
                topic_filter: "/foo".into(),
                qos: QoS::AtLeastOnce,
            })]);

        let res = ch.handle(event).await.unwrap();

        assert_matches!(res, Handled::Skipped(_))
    }

    #[tokio::test]
    async fn default_disconnected_state() {
        let (handle, _) = pump::channel();

        let ch = ConnectivityMqttEventHandler::new(handle);

        assert_eq!(ch.state, ConnectivityState::Disconnected);
    }
}
