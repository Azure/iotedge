use std::{error::Error as StdError, path::PathBuf};

use thiserror::Error;

use mqtt3::proto::Packet;

use crate::Message;

#[derive(Debug, Error)]
pub enum Error {
    #[error("An error occurred sending a message to the broker.")]
    SendBrokerMessage(#[source] tokio::sync::mpsc::error::SendError<Message>),

    #[error("An error occurred sending a message to a connection.")]
    SendConnectionMessage(#[source] tokio::sync::mpsc::error::SendError<Message>),

    #[error("An error occurred sending a message to a snapshotter.")]
    SendSnapshotMessage,

    #[error("An error occurred decoding a packet.")]
    DecodePacket(#[from] mqtt3::proto::DecodeError),

    #[error("An error occurred encoding a packet.")]
    EncodePacket(#[from] mqtt3::proto::EncodeError),

    #[error("Expected CONNECT packet as first packet, received {0:?}")]
    NoConnect(Packet),

    #[error("Connection closed before any packets received.")]
    NoPackets,

    #[error("Session is offline.")]
    SessionOffline,

    #[error("MQTT protocol violation occurred.")]
    ProtocolViolation,

    #[error("Provided topic filter is invalid: {0}")]
    InvalidTopicFilter(String),

    #[error("All packet identifiers are exhausted.")]
    PacketIdentifiersExhausted,

    #[error("An error occurred joining a task.")]
    TaskJoin(#[from] tokio::task::JoinError),

    #[error("An error occurred signaling the event loop of a thread shutdown.")]
    ThreadShutdown(#[from] tokio::sync::oneshot::error::RecvError),

    #[error("An error occurred persisting state")]
    Persist(#[from] crate::persist::PersistError),

    #[error("Unable to obtain peer certificate.")]
    PeerCertificate(#[source] native_tls::Error),

    #[error("Unable to obtain peer address.")]
    PeerAddr(#[source] std::io::Error),

    #[error("Unable to start broker.")]
    InitializeBroker(#[from] InitializeBrokerError),

    #[error("An error occurred when constructing state change: {0}")]
    StateChange(#[from] serde_json::Error),
}

/// Represents errors occurred while bootstrapping broker.
#[derive(Debug, Error)]
pub enum InitializeBrokerError {
    #[error("An error occurred binding the server's listening socket on {0}.")]
    BindServer(String, #[source] std::io::Error),

    #[error("An error occurred getting a connection's peer address.")]
    ConnectionPeerAddress(#[source] std::io::Error),

    #[error("An error occurred getting local address.")]
    ConnectionLocalAddress(#[source] std::io::Error),

    #[error("An error occurred loading configuration.")]
    LoadConfiguration(#[source] config::ConfigError),

    #[error("An error occurred loading identity from file {0}.")]
    LoadIdentity(PathBuf, #[source] std::io::Error),

    #[error("An error occurred  decoding identity content.")]
    DecodeIdentity(#[source] native_tls::Error),

    #[error("An error occurred  bootstrapping TLS")]
    Tls(#[source] native_tls::Error),
}

pub struct DetailedErrorValue<'a, E>(pub &'a E);

impl<'a, E: StdError> std::fmt::Display for DetailedErrorValue<'a, E> {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.0)?;
        let mut current: &dyn StdError = self.0;
        while let Some(source) = current.source() {
            write!(f, " Caused by: {}", source)?;
            current = source;
        }
        Ok(())
    }
}
