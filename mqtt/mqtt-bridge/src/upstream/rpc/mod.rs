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
mod remote;

pub use local::LocalRpcMqttEventHandler;
use parking_lot::Mutex;
pub use remote::{RemoteRpcMqttEventHandler, RpcPumpHandle};

use std::{
    collections::HashMap, fmt::Display, fmt::Formatter, fmt::Result as FmtResult, sync::Arc,
};

use bson::doc;
use serde::{Deserialize, Serialize};
use tracing::error;

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

    #[error("unable to send nack for {0}. {1}")]
    SendNack(CommandId, #[source] PumpError),

    #[error("unable to send ack for {0}. {1}")]
    SendAck(CommandId, #[source] PumpError),

    #[error("unable to send command for {0} to remote pump. {1}")]
    SendToRemotePump(CommandId, #[source] PumpError),

    #[error("unable to send publication on {0} to remote pump. {1}")]
    SendPublicationToLocalPump(String, #[source] PumpError),
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

impl Display for RpcCommand {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            Self::Subscribe { topic_filter } => write!(f, "SUB {}", topic_filter),
            Self::Unsubscribe { topic_filter } => write!(f, "UNSUB {}", topic_filter),
            Self::Publish { topic, .. } => write!(f, "PUB {}", topic),
        }
    }
}

/// Represents a mapping of RPC subscription topic filter to unique command
/// identifier. It is shared between remote pump event handler and remote
/// pump message processor of upstream bridge.
///
/// It is shared due to `mqtt3::Client` implementation details when
/// subscription has been made with `UpdateSubscriptionHandle` but the server
/// response comes back as `mqtt3::Event` type which handled with the event
/// handler.
#[derive(Debug, Clone, Default)]
pub struct RpcSubscriptions(Arc<Mutex<HashMap<String, CommandId>>>);

impl RpcSubscriptions {
    /// Stores topic filter to command identifier mapping.
    pub fn insert(&self, topic_filter: &str, id: CommandId) -> Option<CommandId> {
        self.0.lock().insert(topic_filter.into(), id)
    }

    /// Removes topic filter to command identifier mapping and returns
    /// `CommandId` if exists.
    pub fn remove(&self, topic_filter: &str) -> Option<CommandId> {
        self.0.lock().remove(topic_filter)
    }
}
