use derive_more::Display;
use failure::{Backtrace, Context, Fail};
use mqtt3::proto::Packet;

#[derive(Debug, Display)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "An error occurred sending a message to the broker.")]
    SendBrokerMessage,

    #[fail(display = "An error occurred sending a message to a connection.")]
    SendConnectionMessage,

    #[fail(display = "An error occurred sending a message to a snapshotter.")]
    SendSnapshotMessage,

    #[fail(display = "An error occurred decoding a packet.")]
    DecodePacket,

    #[fail(display = "An error occurred encoding a packet.")]
    EncodePacket,

    #[fail(display = "Expected CONNECT packet as first packet, received {:?}", _0)]
    NoConnect(Packet),

    #[fail(display = "Connection closed before any packets received.")]
    NoPackets,

    #[fail(display = "No session.")]
    NoSession,

    #[fail(display = "Session is offline.")]
    SessionOffline,

    #[fail(display = "MQTT protocol violation occurred.")]
    ProtocolViolation,

    #[fail(display = "Provided topic filter is invalid: {}", _0)]
    InvalidTopicFilter(String),

    #[fail(display = "All packet identifiers are exhausted.")]
    PacketIdentifiersExhausted,

    #[fail(display = "An error occurred joining a task.")]
    TaskJoin,

    #[fail(display = "An error occurred persisting state: {}", _0)]
    Persist(crate::persist::ErrorReason),

    #[fail(display = "Unable to obtain peer leaf certificate.")]
    PeerCertificate,

    #[fail(display = "Unable to start broker: {}", _0)]
    InitializeBroker(InitializeBrokerReason),

    #[fail(display = "An error occurred checking client permissions: {}.", _0)]
    Auth(crate::auth::ErrorReason),
}

impl Fail for Error {
    fn cause(&self) -> Option<&dyn Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Error {
    pub fn new(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }

    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }
}

/// Represents reason for errors occurred while bootstrapping broker.
#[derive(Debug, Display, PartialEq)]
pub enum InitializeBrokerReason {
    #[display(fmt = "An error occurred binding the server's listening socket.")]
    BindServer,

    #[display(fmt = "An error occurred getting a connection's peer address.")]
    ConnectionPeerAddress,

    #[display(fmt = "An error occurred getting local address.")]
    ConnectionLocalAddress,

    #[display(fmt = "An error occurred configuring a connection.")]
    ConnectionConfiguration,

    #[display(fmt = "An error occurred loading configuration.")]
    LoadConfiguration,

    #[display(fmt = "An error occurred  obtaining service identity.")]
    IdentityConfiguration,

    #[display(fmt = "An error occurred  loading identity from file.")]
    LoadIdentity,

    #[display(fmt = "An error occurred  decoding identity content.")]
    DecodeIdentity,

    #[display(fmt = "An error occurred  starting listener.")]
    StartListener,

    #[display(fmt = "An error occurred  bootstrapping TLS")]
    Tls,
}
