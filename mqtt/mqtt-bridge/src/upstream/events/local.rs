use async_trait::async_trait;
use bson::doc;
use bytes::Bytes;
use mqtt3::{proto::Publication, proto::QoS};
use serde_json::json;
use tracing::{debug, error};

// Import and use mocks when run tests, real implementation when otherwise
#[cfg(test)]
pub use crate::client::MockPublishHandle as PublishHandle;

#[cfg(not(test))]
use crate::client::PublishHandle;

use crate::{
    client::InFlightPublishHandle,
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
}

/// Handles control event received by a local upstream bridge pump.
///
/// It handles follwing events:
/// * connectivity update - emitted when the connection to remote broker changed
///   (connected/disconnected). It should publish corresponding MQTT message to the
///   local broker.
/// * RPC command acknowledgement - emitted when the RPC command executed with
///   success result.
/// * RPC command negative acknowledgement - emitted when the RPC command failed
///   to execute.
pub struct LocalUpstreamPumpEventHandler {
    publish_handle: InFlightPublishHandle<PublishHandle>,
}

impl LocalUpstreamPumpEventHandler {
    pub fn new(publish_handle: InFlightPublishHandle<PublishHandle>) -> Self {
        Self {
            publish_handle: publish_handle,
        }
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
                    topic_name: format!("$edgehub/rpc/ack/{}", command_id),
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
                        topic_name: format!("$edgehub/rpc/nack/{}", command_id),
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
        };

        println!("---PUBLICATION FOUND?---");
        if let Some(publication) = maybe_publication {
            println!("---PUBLICATION FOUND---");
            let topic = publication.topic_name.clone();
            let publish_fut = self.publish_handle.publish_future(publication).await;
            if let Err(e) = publish_fut.await {
                println!("---ERROR FOUND---: {:?}", e);
                error!(err = %e, "failed to publish on topic {}", topic);
            }
        }
    }
}

#[cfg(test)]
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

        pub_handle.expect_clone().once();

        let in_flight_handle = InFlightPublishHandle::new(pub_handle, 5);
        let mut handler = LocalUpstreamPumpEventHandler::new(in_flight_handle);

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
                publication.topic_name == "$edgehub/rpc/ack/1" && publication.payload.is_empty()
            })
            .returning(|_| Ok(()));

        let in_flight_handle = InFlightPublishHandle::new(pub_handle, 5);
        let mut handler = LocalUpstreamPumpEventHandler::new(in_flight_handle);

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
                publication.topic_name == "$edgehub/rpc/nack/1" && publication.payload == payload
            })
            .returning(|_| Ok(()));

        let in_flight_handle = InFlightPublishHandle::new(pub_handle, 5);
        let mut handler = LocalUpstreamPumpEventHandler::new(in_flight_handle);

        let event = LocalUpstreamPumpEvent::RpcNack("1".into(), "error".into());
        handler.handle(event).await;
    }
}
