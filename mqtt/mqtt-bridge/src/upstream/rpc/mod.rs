//! Downstream MQTT client event handler to react on RPC commands that
//! `EdgeHub` sends to execute.
//!
//! The main purpose of this handler is to establish a communication channel
//! between `EdgeHub` and the upstream bridge.
//! `EdgeHub` will use low level commands SUB, UNSUB, PUB. In turn the bridge
//! sends corresponding MQTT packet to upstream broker and waits for an ack
//! from the upstream. After ack is received it sends a special publish to
//! downstream broker.

mod local;
// mod remote; TODO uncomment when it is ready

pub use local::LocalRpcHandler;
pub use remote::RemoteRpcHandler;

use std::{fmt::Display, fmt::Formatter, fmt::Result as FmtResult, sync::Arc};

use bson::doc;
use serde::{Deserialize, Serialize};
use tracing::error;

use mqtt3::PublishError;

use crate::pump::PumpError;

/// RPC command unique identificator.
#[derive(Debug, Clone, PartialEq)]
pub struct CommandId(Arc<String>);

impl<C> From<C> for CommandId
where
    C: Into<String>,
{
    fn from(command_id: C) -> Self {
        Self(Arc::new(command_id.into()))
    }
}

impl Display for CommandId {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.0)
    }
}

/// RPC command execution error.
#[derive(Debug, thiserror::Error)]
pub enum RpcError {
    #[error("failed to deserialize command from received publication")]
    DeserializeCommand(#[from] bson::de::Error),

    #[error("failed to serialize ack")]
    SerializeAck(#[from] bson::ser::Error),

    #[error("unable to send nack for {0}. {1}")]
    SendNack(CommandId, #[source] PublishError),

    #[error("unable to send ack for {0}. {1}")]
    SendAck(CommandId, #[source] PublishError),

    #[error("unable to command for {0} to remote pump. {1}")]
    SendToRemotePump(CommandId, #[source] PumpError),
}

/// RPC command to be executed against upstream broker.
#[derive(Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", tag = "cmd")]
pub enum RpcCommand {
    /// A RPC command to subscribe to the topic.
    #[serde(rename = "sub")]
    Subscribe {
        #[serde(rename = "topic")]
        topic_filter: String,
    },

    /// A RPC command to unsubscribe from the topic.
    #[serde(rename = "unsub")]
    Unsubscribe {
        #[serde(rename = "topic")]
        topic_filter: String,
    },

    /// A RPC command to publish a message to a given topic.
    #[serde(rename = "pub")]
    Publish {
        topic: String,

        #[serde(with = "serde_bytes")]
        payload: Vec<u8>,
    },
}

mod remote {
    use async_trait::async_trait;

    use mqtt3::Event;

    use crate::client::{EventHandler, Handled};

    use super::RpcError;

    pub struct RemoteRpcHandler;

    #[async_trait]
    impl EventHandler for RemoteRpcHandler {
        type Error = RpcError;

        async fn handle(&mut self, _: &Event) -> Result<Handled, Self::Error> {
            Ok(Handled::Skipped)
        }
    }
}
