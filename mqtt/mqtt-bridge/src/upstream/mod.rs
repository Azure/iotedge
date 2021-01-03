//! Module contains code related to upstream bridge.

mod connectivity;
mod events;
mod rpc;

pub use connectivity::{ConnectivityError, ConnectivityMqttEventHandler, ConnectivityState};
pub use events::{
    LocalUpstreamPumpEvent, LocalUpstreamPumpEventHandler, RemoteUpstreamPumpEvent,
    RemoteUpstreamPumpEventHandler,
};
pub use rpc::{
    CommandId, LocalRpcMqttEventHandler, RemoteRpcMqttEventHandler, RpcCommand, RpcError,
    RpcPumpHandle, RpcSubscriptions,
};

use async_trait::async_trait;
use mqtt3::Event;

use crate::{
    bridge::BridgeError,
    client::{Handled, MqttEventHandler},
    messages::StoreMqttEventHandler,
    persist::StreamWakeableState,
};

/// Handles all events that local clients received for upstream bridge.
///
/// Contains several event handlers to process RPC and regular MQTT events
/// in a chain.
pub struct LocalUpstreamMqttEventHandler<S> {
    messages: StoreMqttEventHandler<S>,
    rpc: LocalRpcMqttEventHandler,
}

impl<S> LocalUpstreamMqttEventHandler<S> {
    pub fn new(messages: StoreMqttEventHandler<S>, rpc: LocalRpcMqttEventHandler) -> Self {
        Self { messages, rpc }
    }
}

#[async_trait]
impl<S> MqttEventHandler for LocalUpstreamMqttEventHandler<S>
where
    S: StreamWakeableState + Send,
{
    type Error = BridgeError;

    fn subscriptions(&self) -> Vec<String> {
        let mut subscriptions = self.rpc.subscriptions();
        subscriptions.extend(self.messages.subscriptions());
        subscriptions
    }

    async fn handle(&mut self, event: Event) -> Result<Handled, Self::Error> {
        // try to handle as RPC command first
        match self.rpc.handle(event).await? {
            Handled::Fully => Ok(Handled::Fully),
            Handled::Partially(event) | Handled::Skipped(event) => {
                // handle as an event for regular message handler
                self.messages.handle(event).await
            }
        }
    }
}

/// Handles all events that comes to remote clients received for upstream bridge.
///
/// Contains several event handlers to process Connectivity, RPC and regular
/// MQTT events in a chain.
pub struct RemoteUpstreamMqttEventHandler<S> {
    messages: StoreMqttEventHandler<S>,
    rpc: RemoteRpcMqttEventHandler,
    connectivity: ConnectivityMqttEventHandler,
}

impl<S> RemoteUpstreamMqttEventHandler<S> {
    pub fn new(
        messages: StoreMqttEventHandler<S>,
        rpc: RemoteRpcMqttEventHandler,
        connectivity: ConnectivityMqttEventHandler,
    ) -> Self {
        Self {
            messages,
            rpc,
            connectivity,
        }
    }
}

#[async_trait]
impl<S> MqttEventHandler for RemoteUpstreamMqttEventHandler<S>
where
    S: StreamWakeableState + Send,
{
    type Error = BridgeError;

    fn subscriptions(&self) -> Vec<String> {
        let mut subscriptions = self.messages.subscriptions();
        subscriptions.extend(self.rpc.subscriptions());
        subscriptions.extend(self.connectivity.subscriptions());
        subscriptions
    }

    async fn handle(&mut self, event: Event) -> Result<Handled, Self::Error> {
        // try to handle incoming connectivity event
        let event = match self.connectivity.handle(event).await? {
            Handled::Fully => return Ok(Handled::Fully),
            Handled::Partially(event) | Handled::Skipped(event) => event,
        };

        // try to handle incoming messages as RPC command
        let event = match self.rpc.handle(event).await? {
            Handled::Fully => return Ok(Handled::Fully),
            Handled::Partially(event) | Handled::Skipped(event) => event,
        };

        // handle as an event for regular message handler
        self.messages.handle(event).await
    }
}
