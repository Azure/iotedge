#![allow(dead_code)] // TODO remove when finished

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

use std::sync::Arc;

use bson::doc;
use serde::{Deserialize, Serialize};
use tracing::error;

use mqtt3::PublishError;

use crate::pump::PumpError;

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

impl std::fmt::Display for CommandId {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.0)
    }
}

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

#[derive(Debug, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", tag = "cmd")]
pub enum RpcCommand {
    #[serde(rename = "sub")]
    Subscribe {
        #[serde(rename = "topic")]
        topic_filter: String,
    },

    #[serde(rename = "unsub")]
    Unsubscribe {
        #[serde(rename = "topic")]
        topic_filter: String,
    },

    #[serde(rename = "pub")]
    Publish {
        topic: String,

        #[serde(with = "serde_bytes")]
        payload: Vec<u8>,
    },
}
