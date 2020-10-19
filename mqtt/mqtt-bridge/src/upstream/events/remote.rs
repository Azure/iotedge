use async_trait::async_trait;

use crate::{
    pump::PumpMessageHandler,
    upstream::{CommandId, RpcCommand},
};

/// Pump control event for a remote upstream bridge pump.
#[derive(Debug, PartialEq)]
pub enum RemoteUpstreamPumpEvent {
    RpcCommand(CommandId, RpcCommand),
}

/// Handles control event received by a remote upstream bridge pump.
///
/// It handles follwing events:
/// * RPC command - emitted when `EdgeHub` requested RPC command to be executed
/// against remote broker.
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
