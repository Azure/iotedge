use async_trait::async_trait;

use crate::{
    pump::PumpMessageHandler,
    upstream::{CommandId, RpcCommand},
};

#[derive(Debug, PartialEq)]
pub enum RemoteUpstreamPumpEvent {
    RpcCommand(CommandId, RpcCommand),
}

pub struct RemoteUpstreamPumpEventHandler;

#[async_trait]
impl PumpMessageHandler for RemoteUpstreamPumpEventHandler {
    type Message = RemoteUpstreamPumpEvent;

    async fn handle(&self, message: Self::Message) {
        match message {
            RemoteUpstreamPumpEvent::RpcCommand(_, _) => {}
        }
    }
}
