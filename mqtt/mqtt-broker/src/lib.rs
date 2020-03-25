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

use std::sync::Arc;

use derive_more::Display;
use mqtt3::*;
use serde::{Deserialize, Serialize};

mod auth;
mod broker;
mod connection;
mod error;
mod persist;
mod server;
mod session;
mod snapshot;
mod subscription;

pub use crate::auth::{AuthId, Certificate};
pub use crate::broker::{Broker, BrokerBuilder, BrokerHandle, BrokerState};
pub use crate::connection::ConnectionHandle;
pub use crate::error::{Error, ErrorKind};
pub use crate::persist::{BincodeFormat, FileFormat, FilePersistor, NullPersistor, Persist};
pub use crate::server::Server;
pub use crate::snapshot::{Snapshotter, StateSnapshotHandle};

#[derive(Clone, Debug, Display, Eq, Hash, PartialEq, Serialize, Deserialize)]
pub struct ClientId(Arc<String>);

impl ClientId {
    pub fn as_str(&self) -> &str {
        &self.0
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
    certificate: Option<Certificate>,
    handle: ConnectionHandle,
}

impl ConnReq {
    pub fn new(
        client_id: ClientId,
        connect: proto::Connect,
        certificate: Option<Certificate>,
        handle: ConnectionHandle,
    ) -> Self {
        Self {
            client_id,
            connect,
            certificate,
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

    pub fn certificate(&self) -> Option<&Certificate> {
        self.certificate.as_ref()
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
pub(crate) mod tests {
    use super::*;

    use bytes::Bytes;
    use proptest::collection::vec;
    use proptest::num;
    use proptest::prelude::*;

    use crate::subscription::tests::arb_qos;

    pub fn arb_clientid() -> impl Strategy<Value = ClientId> {
        "[a-zA-Z0-9]{1,23}".prop_map(|s| ClientId(Arc::new(s)))
    }

    pub fn arb_packet_identifier() -> impl Strategy<Value = proto::PacketIdentifier> {
        (1_u16..=u16::max_value())
            .prop_map(|i| proto::PacketIdentifier::new(i).expect("packet identifier failed"))
    }

    pub fn arb_topic() -> impl Strategy<Value = String> {
        "\\PC+(/\\PC+)*"
    }

    pub fn arb_payload() -> impl Strategy<Value = Bytes> {
        vec(num::u8::ANY, 0..1024).prop_map(Bytes::from)
    }

    prop_compose! {
        pub fn arb_publication()(
            topic_name in arb_topic(),
            qos in arb_qos(),
            retain in proptest::bool::ANY,
            payload in arb_payload(),
        ) -> proto::Publication {
            proto::Publication {
                topic_name,
                qos,
                retain,
                payload,
            }
        }
    }

    pub fn arb_pidq() -> impl Strategy<Value = proto::PacketIdentifierDupQoS> {
        prop_oneof![
            Just(proto::PacketIdentifierDupQoS::AtMostOnce),
            (arb_packet_identifier(), proptest::bool::ANY)
                .prop_map(|(id, dup)| proto::PacketIdentifierDupQoS::AtLeastOnce(id, dup)),
            (arb_packet_identifier(), proptest::bool::ANY)
                .prop_map(|(id, dup)| proto::PacketIdentifierDupQoS::ExactlyOnce(id, dup)),
        ]
    }

    prop_compose! {
        pub fn arb_proto_publish()(
            pidq in arb_pidq(),
            retain in proptest::bool::ANY,
            topic_name in arb_topic(),
            payload in arb_payload(),
        ) -> proto::Publish {
            proto::Publish {
                packet_identifier_dup_qos: pidq,
                retain,
                topic_name,
                payload,
            }
        }
    }

    pub fn arb_publish() -> impl Strategy<Value = Publish> {
        prop_oneof![
            (arb_packet_identifier(), arb_proto_publish())
                .prop_map(|(id, publish)| Publish::QoS0(id, publish)),
            (arb_packet_identifier(), arb_proto_publish())
                .prop_map(|(id, publish)| Publish::QoS12(id, publish)),
        ]
    }

    #[test]
    fn it_works() {
        assert_eq!(2 + 2, 4);
    }
}
