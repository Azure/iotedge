use async_trait::async_trait;
use mqtt3::{proto::Publication, proto::QoS, PublishHandle};
use serde_json::json;
use tracing::{debug, error};

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
    RpcNack(CommandId),
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
        match message {
            LocalUpstreamPumpEvent::ConnectivityUpdate(status) => {
                debug!("changed connectivity status to {}", status);

                let payload = json!({
                    "status": status.to_string(),
                });

                let publication = Publication {
                    topic_name: CONNECTIVITY_TOPIC.to_owned(),
                    qos: QoS::AtLeastOnce,
                    retain: true,
                    payload: payload.to_string().into(),
                };

                if let Err(e) = self.publish_handle.publish(publication).await {
                    error!(err = %e, "failed publish connectivity update event");
                }
            }
            LocalUpstreamPumpEvent::RpcAck(_) => {}
            LocalUpstreamPumpEvent::RpcNack(_) => {}
        }
    }
}
