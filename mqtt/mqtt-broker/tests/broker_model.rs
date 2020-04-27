use matches::assert_matches;
use mqtt3::{proto, PROTOCOL_LEVEL, PROTOCOL_NAME};
use mqtt_broker::{
    AuthId, Broker, BrokerBuilder, ClientEvent, ClientId, ConnReq, ConnectionHandle, Message,
};
use proptest::proptest;
use std::{collections::HashMap, time::Duration};
use uuid::Uuid;

proptest! {
    #[test]
    fn broker_manages_sessions(
        connect in tests_util::arb_connect()
    ) {
    tokio::runtime::Builder::new()
        .basic_scheduler()
        .enable_all()
        .build()
        .unwrap()
        .block_on(test_broker_manages_sessions(connect));
    }
}

async fn test_broker_manages_sessions(connect: proto::Connect) {
    let mut broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (tx, mut rx) = tokio::sync::mpsc::channel(1);
    let connection_handle = ConnectionHandle::from_sender(tx);

    let client_id = client_id(&connect.client_id);
    // let connect = proto::Connect {
    //     username: None,
    //     password: None,
    //     will: None,
    //     client_id: proto::ClientId::IdWithCleanSession(id.into()),
    //     keep_alive: Duration::from_secs(1),
    //     protocol_name: PROTOCOL_NAME.into(),
    //     protocol_level: PROTOCOL_LEVEL,
    // };
    let event = ClientEvent::ConnReq(ConnReq::new(
        client_id.clone(),
        connect.clone(),
        None,
        connection_handle,
    ));

    let mut model = BrokerModel::default();

    broker
        .process_message(client_id.clone(), event)
        .await
        .expect("process message");

    let model_response = model.process_message(client_id.clone(), ModelEventIn::ConnReq(connect));

    let (retained, sessions) = broker.clone_state().into_parts();

    match (rx.recv().await, model_response) {
        (
            Some(Message::Client(broker_client_id, ClientEvent::ConnAck(broker_connack))),
            Some(ModelEventOut::ConnAck(model_connack)),
        ) => {
            assert_eq!(broker_client_id, client_id);
            assert_eq!(broker_connack, model_connack);
        }
        _ => todo!(),
    }

    assert_eq!(sessions.len(), model.sessions.len());
    for (broker, model) in sessions.into_iter().zip(model.sessions) {
        assert_eq!(broker.client_id(), &model.0);
    }
}

fn client_id(client_id: &proto::ClientId) -> ClientId {
    match client_id {
        proto::ClientId::ServerGenerated => Uuid::new_v4().to_string().into(),
        proto::ClientId::IdWithCleanSession(id) => id.into(),
        proto::ClientId::IdWithExistingSession(id) => id.into(),
    }
}

#[derive(Debug, Default)]
struct BrokerModel {
    pub sessions: HashMap<ClientId, ModelSession>,
}

impl BrokerModel {
    fn process_message(
        &mut self,
        client_id: ClientId,
        event: ModelEventIn,
    ) -> Option<ModelEventOut> {
        match event {
            ModelEventIn::ConnReq(connreq) => Some(self.process_connect(client_id, connreq)),
        }
    }

    fn process_connect(&mut self, client_id: ClientId, connect: proto::Connect) -> ModelEventOut {
        let existing = self.sessions.remove(&client_id);
        let session_present = existing.is_some();

        let session = match connect.client_id {
            proto::ClientId::ServerGenerated | proto::ClientId::IdWithCleanSession(_) => {
                ModelSession::Transient(Vec::default())
            }
            _ => existing.unwrap_or_else(|| ModelSession::Persisted(Vec::default())),
        };

        self.sessions.insert(client_id, session);

        ModelEventOut::ConnAck(proto::ConnAck {
            session_present,
            return_code: proto::ConnectReturnCode::Accepted,
        })
    }
}

#[derive(Debug)]
pub enum ModelSession {
    Transient(Vec<String>),
    Persisted(Vec<String>),
}

enum ModelEventIn {
    ConnReq(proto::Connect),
}

enum ModelEventOut {
    ConnAck(proto::ConnAck),
}

mod tests_util {
    use std::{sync::Arc, time::Duration};

    use mqtt3::proto;
    use proptest::{bool, collection::vec, num, prelude::*};

    use bytes::Bytes;
    use mqtt_broker::{ClientId, Publish, Segment, Subscription, TopicFilter};

    //     pub fn arb_client_event() -> impl Strategy<Value = ClientEvent> {

    //         prop_oneof![
    //             arb_connect()
    //         ]
    //     }
    // }

    prop_compose! {
        pub fn arb_connect()(
            username in arb_username(),
            password in arb_password(),
            client_id in arb_client_id()
        ) -> proto::Connect{
            proto::Connect{

                username,
                password,
                will: None,
                client_id,
                keep_alive: Duration::from_secs(1),
                protocol_name: mqtt3::PROTOCOL_NAME.into(),
                protocol_level: mqtt3::PROTOCOL_LEVEL,
            }
        }
    }

    pub fn arb_username() -> impl Strategy<Value = Option<String>> {
        prop_oneof!["\\PC*".prop_map(|s| Some(s.into())), Just(None)]
    }

    pub fn arb_password() -> impl Strategy<Value = Option<String>> {
        prop_oneof!["\\PC*".prop_map(|s| Some(s.to_string())), Just(None)]
    }

    pub fn arb_client_id() -> impl Strategy<Value = proto::ClientId> {
        prop_oneof![
            Just(proto::ClientId::ServerGenerated),
            "[a-zA-Z0-9]{1,23}".prop_map(|s| proto::ClientId::IdWithCleanSession(s)),
            "[a-zA-Z0-9]{1,23}".prop_map(|s| proto::ClientId::IdWithExistingSession(s))
        ]
    }

    // pub fn arb_client_id() -> impl Strategy<Value = ClientId> {
    //     "[a-zA-Z0-9]{1,23}".prop_map(|s| ClientId(Arc::new(s)))
    // }

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
}
