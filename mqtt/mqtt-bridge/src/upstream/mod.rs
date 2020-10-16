//! Module contains code related to upstream bridge.

mod rpc;

pub use rpc::{CommandId, LocalRpcHandler, RemoteRpcHandler, RpcCommand, RpcError};

use async_trait::async_trait;
use mqtt3::Event;

use crate::{
    bridge::BridgeError,
    client::{EventHandler, Handled},
    messages::MessageHandler,
    persist::StreamWakeableState,
};

/// Handles all events that local clients received for upstream bridge.
///
/// Contains several event handlers to process RPC and regular MQTT events
/// in a chain.
pub struct LocalUpstreamHandler<S> {
    messages: MessageHandler<S>,
    rpc: LocalRpcHandler,
}

impl<S> LocalUpstreamHandler<S> {
    pub fn new(messages: MessageHandler<S>, rpc: LocalRpcHandler) -> Self {
        Self { messages, rpc }
    }
}

#[async_trait]
impl<S> EventHandler for LocalUpstreamHandler<S>
where
    S: StreamWakeableState + Send,
{
    type Error = BridgeError;

    async fn handle(&mut self, event: &Event) -> Result<Handled, Self::Error> {
        // try to handle as RPC command first
        if self.rpc.handle(&event).await? == Handled::Fully {
            return Ok(Handled::Fully);
        }

        // handle as an event for regular message handler
        self.messages.handle(&event).await
    }
}

/// Handles all events that comes to remote clients received for upstream bridge.
///
/// Contains several event handlers to process Connectivity, RPC and regular
/// MQTT events in a chain.
pub struct RemoteUpstreamHandler<S> {
    messages: MessageHandler<S>,
    rpc: RemoteRpcHandler,
}

impl<S> RemoteUpstreamHandler<S> {
    pub fn new(messages: MessageHandler<S>, rpc: RemoteRpcHandler) -> Self {
        Self { messages, rpc }
    }
}

#[async_trait]
impl<S> EventHandler for RemoteUpstreamHandler<S>
where
    S: StreamWakeableState + Send,
{
    type Error = BridgeError;

    async fn handle(&mut self, event: &Event) -> Result<Handled, Self::Error> {
        // TODO add connectivity signals here

        // try to handle incoming messages as RPC command
        if self.rpc.handle(&event).await? == Handled::Fully {
            return Ok(Handled::Fully);
        }

        // handle as an event for regular message handler
        self.messages.handle(&event).await
    }
}
