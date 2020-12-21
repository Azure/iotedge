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
mod ready;
mod server;
mod session;
pub mod settings;
pub mod sidecar;
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
    fmt::{Debug, Display, Formatter, Result as FmtResult},
    net::SocketAddr,
    sync::Arc,
};

use chrono::{DateTime, Utc};
use proto::Publication;
use serde::{Deserialize, Serialize};
use tokio::sync::OwnedSemaphorePermit;

use mqtt3::proto;

pub use crate::auth::{AuthId, Identity};
pub use crate::broker::{Broker, BrokerBuilder, BrokerHandle};
pub use crate::connection::{
    ConnectionHandle, IncomingPacketProcessor, MakeMqttPacketProcessor, MakePacketProcessor,
    OutgoingPacketProcessor, PacketAction,
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
pub use ready::BrokerReadyEvent;

pub type BrokerReady = ready::BrokerReady<ready::BrokerReadyEvent>;
pub type BrokerReadySignal = ready::BrokerReadySignal<ready::BrokerReadyEvent>;
pub type BrokerReadyHandle = ready::BrokerReadyHandle<ready::BrokerReadyEvent>;

#[derive(Clone, Debug, Eq, Hash, PartialEq, Serialize, Deserialize)]
pub struct ClientId(Arc<str>);

impl ClientId {
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl<T: AsRef<str>> From<T> for ClientId {
    fn from(id: T) -> Self {
        Self(id.as_ref().into())
    }
}

impl Display for ClientId {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(f, "{}", self.as_str())
    }
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ClientInfo {
    client_id: ClientId,
    peer_addr: SocketAddr,
    auth_id: AuthId,
}

impl ClientInfo {
    pub fn new(
        client_id: impl Into<ClientId>,
        peer_addr: SocketAddr,
        auth_id: impl Into<AuthId>,
    ) -> Self {
        Self {
            client_id: client_id.into(),
            peer_addr,
            auth_id: auth_id.into(),
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_id
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

    pub fn into_parts(self) -> (ClientId, SocketAddr, proto::Connect, ConnectionHandle) {
        (self.client_id, self.peer_addr, self.connect, self.handle)
    }
}

pub enum Auth {
    Identity(AuthId),
    Unknown,
    Failure,
}

impl Debug for Auth {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            Auth::Identity(id) => f.write_fmt(format_args!("\"{}\"", id)),
            Auth::Unknown => f.write_str("Unknown"),
            Auth::Failure => f.write_str("Failure"),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub enum Publish {
    QoS0(proto::PacketIdentifier, proto::Publish),
    QoS12(proto::PacketIdentifier, proto::Publish),
}

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

impl Debug for ClientEvent {
    #[allow(clippy::too_many_lines)]
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            ClientEvent::ConnReq(connreq) => f
                .debug_struct("ConnReq")
                .field("client_id", &connreq.client_id().as_str())
                .field("connect", &connreq.connect())
                .field("auth", &connreq.auth())
                .finish(),
            ClientEvent::ConnAck(connack) => f
                .debug_struct("ConnAck")
                .field("session_present", &connack.session_present)
                .field("return_code", &connack.return_code)
                .finish(),
            ClientEvent::Disconnect(_) => f.write_str("Disconnect"),
            ClientEvent::DropConnection => f.write_str("DropConnection"),
            ClientEvent::CloseSession => f.write_str("CloseSession"),
            ClientEvent::PingReq(_) => f.write_str("PingReq"),
            ClientEvent::PingResp(_) => f.write_str("PingResp"),
            ClientEvent::Subscribe(sub) => f
                .debug_struct("Subscribe")
                .field("id", &sub.packet_identifier.get())
                .field("qos", &sub.subscribe_to)
                .finish(),
            ClientEvent::SubAck(suback) => f
                .debug_struct("SubAck")
                .field("id", &suback.packet_identifier.get())
                .field("qos", &suback.qos)
                .finish(),
            ClientEvent::Unsubscribe(unsub) => f
                .debug_struct("Unsubscribe")
                .field("id", &unsub.packet_identifier.get())
                .field("topic", &unsub.unsubscribe_from)
                .finish(),
            ClientEvent::UnsubAck(unsuback) => f
                .debug_struct("UnsubAck")
                .field("id", &unsuback.packet_identifier.get())
                .finish(),
            ClientEvent::PublishFrom(publish, _) => {
                let (qos, id, dup) = match publish.packet_identifier_dup_qos {
                    proto::PacketIdentifierDupQoS::AtMostOnce => {
                        (proto::QoS::AtMostOnce, None, false)
                    }
                    proto::PacketIdentifierDupQoS::AtLeastOnce(id, dup) => {
                        (proto::QoS::AtLeastOnce, Some(id.get()), dup)
                    }
                    proto::PacketIdentifierDupQoS::ExactlyOnce(id, dup) => {
                        (proto::QoS::ExactlyOnce, Some(id.get()), dup)
                    }
                };
                f.debug_struct("PublishFrom")
                    .field("qos", &qos)
                    .field("id", &id)
                    .field("dup", &dup)
                    .field("retain", &publish.retain)
                    .field("topic_name", &publish.topic_name)
                    .field("payload", &publish.payload)
                    .finish()
            }
            ClientEvent::PublishTo(publish) => {
                let publish = match publish {
                    Publish::QoS0(_, publish) => publish,
                    Publish::QoS12(_, publish) => publish,
                };
                let (qos, id, dup) = match publish.packet_identifier_dup_qos {
                    proto::PacketIdentifierDupQoS::AtMostOnce => {
                        (proto::QoS::AtMostOnce, None, false)
                    }
                    proto::PacketIdentifierDupQoS::AtLeastOnce(id, dup) => {
                        (proto::QoS::AtLeastOnce, Some(id.get()), dup)
                    }
                    proto::PacketIdentifierDupQoS::ExactlyOnce(id, dup) => {
                        (proto::QoS::ExactlyOnce, Some(id.get()), dup)
                    }
                };
                f.debug_struct("PublishTo")
                    .field("qos", &qos)
                    .field("id", &id)
                    .field("dup", &dup)
                    .field("retain", &publish.retain)
                    .field("topic_name", &publish.topic_name)
                    .field("payload", &publish.payload)
                    .finish()
            }
            ClientEvent::PubAck0(packet_identifier) => f
                .debug_struct("PubAck0")
                .field("id", &packet_identifier.get())
                .finish(),
            ClientEvent::PubAck(puback) => f
                .debug_struct("PubAck")
                .field("id", &puback.packet_identifier.get())
                .finish(),
            ClientEvent::PubRec(pubrec) => f
                .debug_struct("PubRec")
                .field("id", &pubrec.packet_identifier.get())
                .finish(),
            ClientEvent::PubRel(pubrel) => f
                .debug_struct("PubRel")
                .field("id", &pubrel.packet_identifier.get())
                .finish(),
            ClientEvent::PubComp(pubcomp) => f
                .debug_struct("PubComp")
                .field("id", &pubcomp.packet_identifier.get())
                .finish(),
        }
    }
}

pub enum SystemEvent {
    /// An event for a broker to stop processing incoming event and exit.
    Shutdown,

    /// An event for a broker to make a snapshot of the current broker state
    /// and send it back to the caller.
    StateSnapshot(StateSnapshotHandle),

    /// An event for a broker to update authorizer with additional data.
    AuthorizationUpdate(Box<dyn Any + Send + Sync>),

    /// An event for a broker to dispatch a publication by broker itself.
    /// The main difference is `ClientEvent::Publish` it doesn't require
    /// ClientId of sender to be passed along with the event.
    Publish(Publication),

    /// An event for a broker to go through offline sessions
    /// and clean up ones that past provided expiration time.
    SessionCleanup(DateTime<Utc>),
}

impl Debug for SystemEvent {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            SystemEvent::Shutdown => f.write_str("Shutdown"),
            SystemEvent::StateSnapshot(_) => f.write_str("StateSnapshot"),
            SystemEvent::AuthorizationUpdate(update) => {
                f.debug_tuple("AuthorizationUpdate").field(&update).finish()
            }
            SystemEvent::Publish(publication) => {
                f.debug_tuple("Publish").field(&publication).finish()
            }
            SystemEvent::SessionCleanup(instant) => {
                f.debug_tuple("SessionCleanup").field(&instant).finish()
            }
        }
    }
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
