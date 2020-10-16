use async_trait::async_trait;

use crate::{
    pump::PumpMessageHandler,
    upstream::{CommandId, ConnectivityState},
};

#[derive(Debug, PartialEq)]
pub enum LocalUpstreamPumpEvent {
    ConnectivityUpdate(ConnectivityState),
    RpcAck(CommandId),
}

pub struct LocalUpstreamPumpEventHandler;

#[async_trait]
impl PumpMessageHandler for LocalUpstreamPumpEventHandler {
    type Message = LocalUpstreamPumpEvent;

    async fn handle(&self, message: Self::Message) {
        match message {
            LocalUpstreamPumpEvent::ConnectivityUpdate(_) => {}
            LocalUpstreamPumpEvent::RpcAck(_) => {}
        }
    }
}
