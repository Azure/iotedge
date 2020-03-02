#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms
)]

use std::fmt;
use std::sync::Arc;

use mqtt::*;

mod broker;
mod connection;
mod error;
mod persist;
mod server;
mod session;
mod snapshot;
mod subscription;

pub use crate::broker::{Broker, BrokerHandle, BrokerState};
pub use crate::connection::ConnectionHandle;
pub use crate::error::{Error, ErrorKind};
pub use crate::persist::{NullPersistor, Persist};
pub use crate::server::Server;
pub use crate::snapshot::{Snapshotter, StateSnapshotHandle};

#[derive(Clone, Debug, Eq, Hash, PartialEq)]
pub struct ClientId(Arc<String>);

impl ClientId {
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl fmt::Display for ClientId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.as_str())
    }
}

impl From<String> for ClientId {
    fn from(s: String) -> ClientId {
        ClientId(Arc::new(s))
    }
}

#[derive(Debug)]
pub struct ConnReq {
    client_id: ClientId,
    connect: proto::Connect,
    handle: ConnectionHandle,
}

impl ConnReq {
    pub fn new(client_id: ClientId, connect: proto::Connect, handle: ConnectionHandle) -> Self {
        Self {
            client_id,
            connect,
            handle,
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_id
    }

    pub fn connect(&self) -> &proto::Connect {
        &self.connect
    }

    pub fn handle(&self) -> &ConnectionHandle {
        &self.handle
    }

    pub fn handle_mut(&mut self) -> &mut ConnectionHandle {
        &mut self.handle
    }

    pub fn into_handle(self) -> ConnectionHandle {
        self.handle
    }

    pub fn into_parts(self) -> (proto::Connect, ConnectionHandle) {
        (self.connect, self.handle)
    }
}

#[derive(Clone, Debug)]
pub enum Publish {
    QoS0(proto::PacketIdentifier, proto::Publish),
    QoS12(proto::PacketIdentifier, proto::Publish),
}

#[derive(Debug)]
pub enum ClientEvent {
    /// Connect request
    ConnReq(ConnReq),

    /// Connect response
    ConnAck(proto::ConnAck),

    /// Graceful disconnect request
    Disconnect(proto::Disconnect),

    /// Non-graceful disconnect request,
    DropConnection,

    /// Close session - connection is already closed but session needs clean up
    CloseSession,

    /// Ping request
    PingReq(proto::PingReq),

    /// Ping response
    PingResp(proto::PingResp),

    /// Subscribe
    Subscribe(proto::Subscribe),

    /// SubAck
    SubAck(proto::SubAck),

    /// Unsubscribe
    Unsubscribe(proto::Unsubscribe),

    /// UnsubAck
    UnsubAck(proto::UnsubAck),

    /// PublishFrom - publish packet from a client
    PublishFrom(proto::Publish),

    /// PublishTo - publish packet to a client
    PublishTo(Publish),

    /// Publish acknowledgement (QoS 0)
    PubAck0(proto::PacketIdentifier),

    /// Publish acknowledgement (QoS 1)
    PubAck(proto::PubAck),

    /// Publish receive (QoS 2 publish, part 1)
    PubRec(proto::PubRec),

    /// Publish release (QoS 2 publish, part 2)
    PubRel(proto::PubRel),

    /// Publish complete (QoS 2 publish, part 3)
    PubComp(proto::PubComp),
}

#[derive(Debug)]
pub enum SystemEvent {
    Shutdown,
    StateSnapshot(StateSnapshotHandle),
    // ConfigUpdate,
}

#[derive(Debug)]
pub enum Message {
    Client(ClientId, ClientEvent),
    System(SystemEvent),
}

#[cfg(test)]
mod tests {
    #[test]
    fn it_works() {
        assert_eq!(2 + 2, 4);
    }
}
