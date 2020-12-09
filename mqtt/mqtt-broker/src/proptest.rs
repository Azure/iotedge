#![cfg(any(test, feature = "proptest"))]
use std::{net::IpAddr, net::SocketAddr, time::Duration};

use bytes::Bytes;
use chrono::Utc;
use proptest::{
    bool,
    collection::{hash_map, vec, vec_deque},
    num,
    prelude::*,
};

use mqtt3::proto;

use crate::{
    AuthId, BrokerSnapshot, ClientId, ClientInfo, Publish, Segment, SessionSnapshot, Subscription,
    TopicFilter,
};

prop_compose! {
    pub fn arb_broker_snapshot()(
        retained in hash_map(arb_topic(), arb_publication(), 0..5),
        sessions in vec(arb_session_snapshot(), 0..5),
    ) -> BrokerSnapshot {
        BrokerSnapshot::new(retained, sessions)
    }
}

prop_compose! {
    pub fn arb_session_snapshot()(
        client_info in arb_client_info(),
        subscriptions in hash_map(arb_topic(), arb_subscription(), 0..5),
        waiting_to_be_sent in vec_deque(arb_publication(), 0..5),
    ) -> SessionSnapshot {
        SessionSnapshot::from_parts(
            client_info,
            subscriptions,
            waiting_to_be_sent,
            Utc::now()
        )
    }
}

prop_compose! {
    pub fn arb_connect(client_id: proto::ClientId)(
        username in arb_username(),
        password in arb_password(),
    ) -> proto::Connect{
        proto::Connect{
            username,
            password,
            will: None,
            client_id: client_id.clone(),
            keep_alive: Duration::from_secs(1),
            protocol_name: mqtt3::PROTOCOL_NAME.into(),
            protocol_level: mqtt3::PROTOCOL_LEVEL,
        }
    }
}

prop_compose! {
    pub fn arb_subscribe()(
        packet_identifier in arb_packet_identifier(),
        subscribe_to in proptest::collection::vec(arb_subscribe_to(), 1..5)
    ) -> proto::Subscribe {
        proto::Subscribe {
            packet_identifier,
            subscribe_to
        }
    }
}

prop_compose! {
    pub fn arb_subscribe_to()(
        topic_filter in arb_topic_filter_weighted(),
        qos in arb_qos()
    ) -> proto::SubscribeTo {
        proto::SubscribeTo {
            topic_filter,
            qos
        }
    }
}

prop_compose! {
    pub fn arb_unsubscribe()(
        packet_identifier in arb_packet_identifier(),
        unsubscribe_from in proptest::collection::vec(arb_topic_filter_weighted(), 1..5)
    ) -> proto::Unsubscribe {
        proto::Unsubscribe {
            packet_identifier,
            unsubscribe_from
        }
    }
}

prop_compose! {
    pub fn arb_client_info()(
        client_id in arb_clientid(),
        auth_id in arb_auth_id(),
        ip in arb_ip(),
        port in arb_port(),
    ) -> ClientInfo {
        // Unfortunately we can't just call SocketAddr::arbitrary() because when serde occurs on SocketAddr,
        // we lose the flowid and scope_id. They get set to 0 by default.
        // This workaround manually sets them to 0 but uses arbitrary values for ip and port
        // Issue opened: https://github.com/serde-rs/serde/issues/1896
        let socket = SocketAddr::new(ip, port);
        ClientInfo::new(client_id, socket, auth_id)
    }
}

pub fn arb_topic_filter_weighted() -> impl Strategy<Value = String> {
    let max = 10;
    prop_oneof![
        arb_topic_filter().prop_map(|topic| topic.to_string()),
        (0..max).prop_map(|n| format!("topic/{}", n)),
    ]
}

pub fn arb_username() -> impl Strategy<Value = Option<String>> {
    prop_oneof!["\\PC*".prop_map(Some), Just(None)]
}

pub fn arb_password() -> impl Strategy<Value = Option<String>> {
    prop_oneof!["\\PC*".prop_map(Some), Just(None)]
}

pub fn arb_client_id() -> impl Strategy<Value = proto::ClientId> {
    prop_oneof![
        Just(proto::ClientId::ServerGenerated),
        "[a-zA-Z0-9]{1,23}".prop_map(proto::ClientId::IdWithCleanSession),
        "[a-zA-Z0-9]{1,23}".prop_map(proto::ClientId::IdWithExistingSession)
    ]
}
pub fn arb_clientid() -> impl Strategy<Value = ClientId> {
    // TODO: Add in # and + once the broker can handle them
    "[a-zA-Z0-9_()!@%,'=\\*\\$\\?\\-]{1,23}".prop_map(Into::into)
}

pub fn arb_auth_id() -> impl Strategy<Value = AuthId> {
    prop_oneof![
        "[a-zA-Z0-9]{1,23}".prop_map(AuthId::from),
        Just(AuthId::Anonymous)
    ]
}

pub fn arb_ip() -> impl Strategy<Value = IpAddr> {
    IpAddr::arbitrary()
}

pub fn arb_port() -> impl Strategy<Value = u16> {
    proptest::num::u16::ANY
}

pub fn arb_client_id_weighted() -> impl Strategy<Value = proto::ClientId> {
    let max = 10;
    prop_oneof![
        Just(proto::ClientId::ServerGenerated),
        "[a-zA-Z0-9]{1,23}".prop_map(proto::ClientId::IdWithCleanSession),
        "[a-zA-Z0-9]{1,23}".prop_map(proto::ClientId::IdWithExistingSession),
        (0..max).prop_map(|s| proto::ClientId::IdWithCleanSession(format!("client_{}", s))),
        (0..max).prop_map(|s| proto::ClientId::IdWithExistingSession(format!("client_{}", s)))
    ]
}

pub fn arb_packet_identifier() -> impl Strategy<Value = proto::PacketIdentifier> {
    (1_u16..=u16::max_value())
        .prop_map(|i| proto::PacketIdentifier::new(i).expect("packet identifier failed"))
}

pub fn arb_topic() -> impl Strategy<Value = String> {
    "\\PC+(/\\PC+)*"
}

pub fn arb_payload() -> impl Strategy<Value = Bytes> {
    vec(num::u8::ANY, 0..128).prop_map(Bytes::from)
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
        (arb_packet_identifier(), bool::ANY)
            .prop_map(|(id, dup)| proto::PacketIdentifierDupQoS::AtLeastOnce(id, dup)),
        (arb_packet_identifier(), bool::ANY)
            .prop_map(|(id, dup)| proto::PacketIdentifierDupQoS::ExactlyOnce(id, dup)),
    ]
}

prop_compose! {
    pub fn arb_proto_publish()(
        pidq in arb_pidq(),
        retain in bool::ANY,
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

fn arb_segment() -> impl Strategy<Value = Segment> {
    prop_oneof![
        "[^+#\0/]+".prop_map(Segment::Level),
        Just(Segment::SingleLevelWildcard),
        Just(Segment::MultiLevelWildcard),
    ]
}

prop_compose! {
    pub fn arb_topic_filter()(
        segments in vec(arb_segment(), 1..20),
        multi in bool::ANY,
    ) -> TopicFilter {
        let mut filtered = vec![];
        for segment in segments {
            if segment != Segment::MultiLevelWildcard {
                filtered.push(segment);
            }
        }

        if multi || filtered.is_empty() {
            filtered.push(Segment::MultiLevelWildcard);
        }

        TopicFilter::new(filtered)
    }
}

pub fn arb_qos() -> impl Strategy<Value = proto::QoS> {
    prop_oneof![
        Just(proto::QoS::AtMostOnce),
        Just(proto::QoS::AtLeastOnce),
        Just(proto::QoS::ExactlyOnce),
    ]
}

prop_compose! {
    pub fn arb_subscription()(
        filter in arb_topic_filter(),
        max_qos in arb_qos(),
    ) -> Subscription {
        Subscription::new(filter, max_qos)
    }
}
