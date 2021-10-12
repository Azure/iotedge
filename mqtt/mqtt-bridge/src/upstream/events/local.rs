use async_trait::async_trait;
use bson::doc;
use bytes::Bytes;
use mockall_double::double;
use mqtt3::{proto::Publication, proto::QoS};
use serde_json::json;
use tracing::{debug, error};

#[double]
use crate::client::PublishHandle;

use crate::{
    pump::PumpMessageHandler,
    upstream::{CommandId, ConnectivityState},
};

const CONNECTIVITY_TOPIC: &str = "$internal/connectivity";

/// Pump control event for a local upstream bridge pump.
#[derive(Debug, PartialEq)]
pub enum LocalUpstreamPumpEvent {
    /// Connectivity update event.
    ConnectivityUpdate(ConnectivityState),

    /// RPC command acknowledgement event.
    RpcAck(CommandId),

    /// RPC command negative acknowledgement event.
    RpcNack(CommandId, String),

    /// Forward incoming upstream publication event.
    Publication(Publication),
}

/// Handles control event received by a local upstream bridge pump.
///
/// It handles following events:
/// * connectivity update - emitted when the connection to remote broker changed
///   (connected/disconnected). It should publish corresponding MQTT message to the
///   local broker.
/// * RPC command acknowledgement - emitted when the RPC command executed with
///   success result.
/// * RPC command negative acknowledgement - emitted when the RPC command failed
///   to execute.
pub struct LocalUpstreamPumpEventHandler {
    publish_handle: PublishHandle,
}

impl LocalUpstreamPumpEventHandler {
    pub fn new(publish_handle: PublishHandle) -> Self {
        Self { publish_handle }
    }
}

#[async_trait]
impl PumpMessageHandler for LocalUpstreamPumpEventHandler {
    type Message = LocalUpstreamPumpEvent;

    async fn handle(&mut self, message: Self::Message) {
        let maybe_publication = match message {
            LocalUpstreamPumpEvent::ConnectivityUpdate(status) => {
                debug!("changed connectivity status to {}", status);

                let payload = json!({ "status": status });
                match serde_json::to_string(&payload) {
                    Ok(payload) => Some(Publication {
                        topic_name: CONNECTIVITY_TOPIC.to_owned(),
                        qos: QoS::AtLeastOnce,
                        retain: true,
                        payload: payload.into(),
                    }),
                    Err(e) => {
                        error!("unable to convert to JSON. {}", e);
                        None
                    }
                }
            }
            LocalUpstreamPumpEvent::RpcAck(command_id) => {
                debug!("sending rpc command ack {}", command_id);

                Some(Publication {
                    topic_name: format!("$downstream/rpc/ack/{}", command_id),
                    qos: QoS::AtLeastOnce,
                    retain: false,
                    payload: Bytes::default(),
                })
            }
            LocalUpstreamPumpEvent::RpcNack(command_id, reason) => {
                debug!("sending rpc command nack {}", command_id);

                let mut payload = Vec::new();
                let doc = doc! { "reason": reason };
                match doc.to_writer(&mut payload) {
                    Ok(_) => Some(Publication {
                        topic_name: format!("$downstream/rpc/nack/{}", command_id),
                        qos: QoS::AtLeastOnce,
                        retain: false,
                        payload: payload.into(),
                    }),
                    Err(e) => {
                        error!("unable to convert to BSON. {}", e);
                        None
                    }
                }
            }
            LocalUpstreamPumpEvent::Publication(publication) => {
                debug!("sending incoming message on {}", publication.topic_name);
                Some(publication)
            }
        };

        if let Some(publication) = maybe_publication {
            let topic = publication.topic_name.clone();
            if let Err(e) = self.publish_handle.publish(publication).await {
                error!(error = %e, "failed to publish on topic {}", topic);
            }
        }
    }
}

#[cfg(test)]
#[allow(clippy::semicolon_if_nothing_returned)]
mod tests {
    use crate::client::MockPublishHandle;

    use super::*;

    #[tokio::test]
    async fn it_sends_connectivity_update_when_connected() {
        it_sends_connectivity_update_when_changed(ConnectivityState::Connected).await;
    }

    #[tokio::test]
    async fn it_sends_connectivity_update_when_disconnected() {
        it_sends_connectivity_update_when_changed(ConnectivityState::Disconnected).await;
    }

    async fn it_sends_connectivity_update_when_changed(state: ConnectivityState) {
        let payload = json!({ "status": state });
        let payload = serde_json::to_vec(&payload).unwrap();

        let mut pub_handle = MockPublishHandle::new();
        pub_handle
            .expect_publish()
            .once()
            .withf(move |publication| {
                publication.topic_name == "$internal/connectivity" && publication.payload == payload
            })
            .returning(|_| Ok(()));

        let mut handler = LocalUpstreamPumpEventHandler::new(pub_handle);

        let event = LocalUpstreamPumpEvent::ConnectivityUpdate(state);
        handler.handle(event).await;
    }

    #[tokio::test]
    async fn it_sends_rpc_ack() {
        let mut pub_handle = MockPublishHandle::new();
        pub_handle
            .expect_publish()
            .once()
            .withf(move |publication| {
                publication.topic_name == "$downstream/rpc/ack/1" && publication.payload.is_empty()
            })
            .returning(|_| Ok(()));

        let mut handler = LocalUpstreamPumpEventHandler::new(pub_handle);

        let event = LocalUpstreamPumpEvent::RpcAck("1".into());
        handler.handle(event).await;
    }

    #[tokio::test]
    async fn it_sends_rpc_nack() {
        let mut payload = Vec::new();
        let doc = doc! { "reason": "error" };
        doc.to_writer(&mut payload).unwrap();

        let mut pub_handle = MockPublishHandle::new();
        pub_handle
            .expect_publish()
            .once()
            .withf(move |publication| {
                publication.topic_name == "$downstream/rpc/nack/1" && publication.payload == payload
            })
            .returning(|_| Ok(()));

        let mut handler = LocalUpstreamPumpEventHandler::new(pub_handle);

        let event = LocalUpstreamPumpEvent::RpcNack("1".into(), "error".into());
        handler.handle(event).await;
    }

    #[tokio::test]
    async fn it_sends_incoming_publication() {
        let mut pub_handle = MockPublishHandle::new();
        pub_handle
            .expect_publish()
            .once()
            .withf(move |publication| {
                publication.topic_name == "$downstream/device_1/module_a/twin/res/200"
            })
            .returning(|_| Ok(()));

        let mut handler = LocalUpstreamPumpEventHandler::new(pub_handle);

        let event = LocalUpstreamPumpEvent::Publication(Publication {
            topic_name: "$downstream/device_1/module_a/twin/res/200".into(),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: "hello".into(),
        });
        handler.handle(event).await;
    }
}
