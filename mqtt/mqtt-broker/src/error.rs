use std::{
    error::Error as StdError,
    fmt::{Display, Formatter, Result as FmtResult},
    net::SocketAddr,
    path::PathBuf,
};

use thiserror::Error;

use mqtt3::proto::Packet;

use crate::Message;

#[derive(Debug, Error)]
pub enum Error {
    #[error("An error occurred sending a message to the broker. {0}")]
    SendBrokerMessage(#[source] tokio::sync::mpsc::error::SendError<Message>),

    #[error("An error occurred sending a message to a connection. {0}")]
    SendConnectionMessage(#[source] tokio::sync::mpsc::error::SendError<Message>),

    #[error("An error occurred sending a message to a snapshotter. {0:?}")]
    SendSnapshotMessage(#[source] Box<dyn StdError + Send + Sync>),

    #[error("An error occurred decoding a packet. {0}")]
    DecodePacket(#[from] mqtt3::proto::DecodeError),

    #[error("An error occurred encoding a packet. {0}")]
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

    #[error("An error occurred joining a task. {0}")]
    TaskJoin(#[from] tokio::task::JoinError),

    #[error("An error occurred signaling the event loop of a thread shutdown. {0}")]
    ThreadShutdown(#[from] tokio::sync::oneshot::error::RecvError),

    #[error("An error occurred persisting state. {0}")]
    Persist(#[from] crate::persist::PersistError),

    #[error("Unable to obtain peer certificate. {0}")]
    PeerCertificate(#[source] Box<dyn StdError + Send + Sync>),

    #[error("Unable to obtain peer address. {0}")]
    PeerAddr(#[source] std::io::Error),

    #[error("Unable to start broker. {0}")]
    InitializeBroker(#[from] InitializeBrokerError),

    #[error("An error occurred when constructing state change: {0}")]
    StateChange(#[from] serde_json::Error),

    #[error("An error occurred when processing packet. {0}")]
    PacketProcessing(#[source] Box<dyn StdError + Send + Sync>),
}

/// Represents errors occurred while bootstrapping broker.
#[derive(Debug, Error)]
pub enum InitializeBrokerError {
    #[error("An error occurred converting to a socker address {0}.")]
    SocketAddr(String, #[source] std::io::Error),

    #[error("Missing socker address {0}.")]
    MissingSocketAddr(String),

    #[error("An error occurred binding the server's listening socket on {0}.")]
    BindServer(SocketAddr, #[source] std::io::Error),

    #[error("An error occurred getting local address. {0}")]
    ConnectionLocalAddress(#[source] tokio::io::Error),

    #[error("An error occurred loading identity from file {0}.")]
    LoadIdentity(PathBuf, #[source] std::io::Error),

    #[error("An error occurred  bootstrapping TLS. {0}")]
    Tls(#[from] openssl::error::ErrorStack),
}

pub struct DetailedErrorValue<'a, E>(pub &'a E);

impl<'a, E: StdError> Display for DetailedErrorValue<'a, E> {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.0)?;
        let mut current: &dyn StdError = self.0;
        while let Some(source) = current.source() {
            write!(f, " Caused by: {}", source)?;
            current = source;
        }
        Ok(())
    }
}
