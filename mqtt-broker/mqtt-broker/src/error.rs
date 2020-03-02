use std::fmt;

use failure::{Backtrace, Context, Fail};
use mqtt::proto::Packet;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "A general error occurred in the server.")]
    General,

    #[fail(display = "An error occurred trying to connect.")]
    Connect,

    #[fail(display = "An error occurred sending a message to the broker.")]
    SendBrokerMessage,

    #[fail(display = "An error occurred sending a message to a connection.")]
    SendConnectionMessage,

    #[fail(display = "An error occurred sending a message to a snapshotter.")]
    SendSnapshotMessage,

    #[fail(display = "An error occurred binding the server's listening socket.")]
    BindServer,

    #[fail(display = "An error occurred getting a connection's peer address.")]
    ConnectionPeerAddress,

    #[fail(display = "An error occurred configuring a connection.")]
    ConnectionConfiguration,

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

    #[fail(display = "An error occurred joining the broker task.")]
    BrokerJoin,
}

impl Fail for Error {
    fn cause(&self) -> Option<&dyn Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        fmt::Display::fmt(&self.inner, f)
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
