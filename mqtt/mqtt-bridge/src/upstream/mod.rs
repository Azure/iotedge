mod rpc;

pub use rpc::{CommandId, LocalRpcHandler, RpcCommand, RpcError};

use async_trait::async_trait;
use mqtt3::Event;

use crate::{
    bridge::BridgeError,
    client::{EventHandler, Handled},
    messages::MessageHandler,
    persist::StreamWakeableState,
};

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
