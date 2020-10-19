use async_trait::async_trait;
use mqtt3::{proto::Publication, proto::QoS, PublishHandle};
use tracing::{debug, error};

use crate::{
    pump::PumpMessageHandler,
    upstream::{CommandId, ConnectivityState},
};

const CONNECTIVITY_TOPIC: &str = "$internal/connectivity";

#[derive(Debug, PartialEq)]
pub enum LocalUpstreamPumpEvent {
    ConnectivityUpdate(ConnectivityState),
    RpcAck(CommandId),
}

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
                let payload: bytes::Bytes = status.to_string().into();

                let publication = Publication {
                    topic_name: CONNECTIVITY_TOPIC.to_owned(),
                    qos: QoS::AtLeastOnce,
                    retain: true,
                    payload,
                };

                if let Err(e) = self.publish_handle.publish(publication).await {
                    error!(err = %e, "failed publish connectivity update event");
                }
            }
            LocalUpstreamPumpEvent::RpcAck(_) => {}
        }
    }
}
