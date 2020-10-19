use async_trait::async_trait;

use crate::{
    pump::PumpMessageHandler,
    upstream::{CommandId, ConnectivityState},
};

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
pub struct LocalUpstreamPumpEventHandler;

#[async_trait]
impl PumpMessageHandler for LocalUpstreamPumpEventHandler {
    type Message = LocalUpstreamPumpEvent;

    async fn handle(&self, message: Self::Message) {
        match message {
            LocalUpstreamPumpEvent::ConnectivityUpdate(_) => {}
            LocalUpstreamPumpEvent::RpcAck(_) => {}
            LocalUpstreamPumpEvent::RpcNack(_) => {}
        }
    }
}
