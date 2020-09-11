#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms,
    clippy::must_use_candidate,
    clippy::missing_errors_doc
)]

pub mod auth;
mod broker;
mod connection;
mod error;
mod persist;
mod server;
mod session;
pub mod settings;
mod snapshot;
mod state_change;
mod stream;
mod subscription;
mod tls;
mod transport;

#[cfg(any(test, feature = "proptest"))]
pub mod proptest;

use std::{
    any::Any,
    fmt::{Display, Formatter, Result as FmtResult},
    net::SocketAddr,
    sync::Arc,
};

use serde::{Deserialize, Serialize};
use tokio::sync::OwnedSemaphorePermit;

use mqtt3::proto;

pub use crate::auth::{AuthId, Identity};
pub use crate::broker::{Broker, BrokerBuilder, BrokerHandle};
pub use crate::connection::{
    ConnectionHandle, IncomingPacketProcessor, MakeIncomingPacketProcessor,
    MakeMqttPacketProcessor, MakeOutgoingPacketProcessor, OutgoingPacketProcessor, PacketAction,
};
pub use crate::error::{DetailedErrorValue, Error, InitializeBrokerError};
pub use crate::persist::{
    FileFormat, FilePersistor, NullPersistor, Persist, PersistError, VersionedFileFormat,
};
pub use crate::server::Server;
pub use crate::session::SessionState;
pub use crate::settings::{BrokerConfig, SessionConfig};
pub use crate::snapshot::{
    BrokerSnapshot, SessionSnapshot, ShutdownHandle, Snapshotter, StateSnapshotHandle,
};
pub use crate::subscription::{Segment, Subscription, TopicFilter};
pub use crate::tls::ServerCertificate;

#[derive(Clone, Debug, Eq, Hash, PartialEq, Serialize, Deserialize)]
pub struct ClientId(Arc<String>);

impl ClientId {
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl<T: Into<String>> From<T> for ClientId {
    fn from(s: T) -> ClientId {
        ClientId(Arc::new(s.into()))
    }
}

impl Display for ClientId {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.as_str())
    }
}

#[derive(Debug, Clone)]
pub struct ClientInfo {
    peer_addr: SocketAddr,
    auth_id: AuthId,
}

impl ClientInfo {
    pub fn new(peer_addr: SocketAddr, auth_id: impl Into<AuthId>) -> Self {
        Self {
            peer_addr,
            auth_id: auth_id.into(),
        }
    }

    pub fn peer_addr(&self) -> SocketAddr {
        self.peer_addr
    }

    pub fn auth_id(&self) -> &AuthId {
        &self.auth_id
    }
}

#[derive(Debug)]
pub struct ConnReq {
    client_id: ClientId,
    peer_addr: SocketAddr,
    connect: proto::Connect,
    auth: Auth,
    handle: ConnectionHandle,
}

impl ConnReq {
    pub fn new(
        client_id: ClientId,
        peer_addr: SocketAddr,
        connect: proto::Connect,
        auth: Auth,
        handle: ConnectionHandle,
    ) -> Self {
        Self {
            client_id,
            peer_addr,
            connect,
            auth,
            handle,
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_id
    }

    pub fn peer_addr(&self) -> SocketAddr {
        self.peer_addr
    }

    pub fn connect(&self) -> &proto::Connect {
        &self.connect
    }

    pub fn handle(&self) -> &ConnectionHandle {
        &self.handle
    }

    pub fn auth(&self) -> &Auth {
        &self.auth
    }

    pub fn handle_mut(&mut self) -> &mut ConnectionHandle {
        &mut self.handle
    }

    pub fn into_handle(self) -> ConnectionHandle {
        self.handle
    }

    pub fn into_parts(self) -> (SocketAddr, proto::Connect, ConnectionHandle) {
        (self.peer_addr, self.connect, self.handle)
    }
}

#[derive(Debug)]
pub enum Auth {
    Identity(AuthId),
    Unknown,
    Failure,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
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
    /// Contains optional permit for managing max number of
    /// incoming messages per publisher.
    PublishFrom(proto::Publish, Option<OwnedSemaphorePermit>),

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
    ForceClientDisconnect(ClientId),
    AuthorizationUpdate(Box<dyn Any + Send + Sync>),
}

#[derive(Debug)]
pub enum Message {
    Client(ClientId, ClientEvent),
    System(SystemEvent),
}

#[cfg(test)]
pub(crate) mod tests {
    use std::net::SocketAddr;

    pub fn peer_addr() -> SocketAddr {
        "127.0.0.1:12345".parse().unwrap()
    }
}
