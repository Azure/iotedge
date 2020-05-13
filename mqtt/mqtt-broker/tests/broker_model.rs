use matches::assert_matches;
use mqtt3::{proto, PROTOCOL_LEVEL, PROTOCOL_NAME};
use mqtt_broker::{
    AuthId, Broker, BrokerBuilder, ClientEvent, ClientId, ConnReq, ConnectionHandle, Message,
};
use proptest::{prop_assert_eq, proptest};
use std::{
    collections::{HashMap, HashSet},
    time::Duration,
};
use tokio::sync::mpsc::{self, Receiver, Sender};
use uuid::Uuid;

proptest! {
    #[test]
    fn broker_manages_sessions(
        events in proptest::collection::vec(tests_util::arb_broker_event(), 1..50)
    ) {
    tokio::runtime::Builder::new()
        .basic_scheduler()
        .enable_all()
        .build()
        .unwrap()
        .block_on(test_broker_manages_sessions(events));
    }
}

async fn test_broker_manages_sessions(events: impl IntoIterator<Item = BrokerEvent>) {
    let mut broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    // let (tx, mut rx) = tokio::sync::mpsc::unbounded_channel();
    // let connection_handle = ConnectionHandle::from_sender(tx);

    let mut clients = Vec::new();

    let mut model = BrokerModel::default();

    for event in events {
        let (client_id, broker_event, model_event) = match event {
            BrokerEvent::ConnReq(client_id, connect) => {
                let (tx, rx) = tokio::sync::mpsc::unbounded_channel();
                clients.push(rx);
                let connection_handle = ConnectionHandle::from_sender(tx);
                let connreq =
                    ConnReq::new(client_id.clone(), connect.clone(), None, connection_handle);
                (
                    client_id,
                    ClientEvent::ConnReq(connreq),
                    ModelEventIn::ConnReq(connect),
                )
            }
            BrokerEvent::Disconnect(client_id, disconnect) => (
                client_id,
                ClientEvent::Disconnect(disconnect.clone()),
                ModelEventIn::Disconnect(disconnect),
            ),
            BrokerEvent::Subscribe(client_id, subscribe) => (
                client_id,
                ClientEvent::Subscribe(subscribe.clone()),
                ModelEventIn::Subscribe(subscribe),
            ),
            BrokerEvent::Unsubscribe(client_id, unsubscribe) => (
                client_id,
                ClientEvent::Unsubscribe(unsubscribe.clone()),
                ModelEventIn::Unsubscribe(unsubscribe),
            ),
        };

        broker
            .process_message(client_id.clone(), broker_event)
            .expect("process message");

        let model_event = model.process_message(client_id, model_event);
    }

    // let model_response = model.process_message(client_id.clone(), ModelEventIn::ConnReq(connect));

    let (retained, sessions) = broker.clone_state().into_parts();

    // match (rx.recv().await, model_response) {
    //     (
    //         Some(Message::Client(broker_client_id, ClientEvent::ConnAck(broker_connack))),
    //         Some(ModelEventOut::ConnAck(model_connack)),
    //     ) => {
    //         assert_eq!(broker_client_id, client_id);
    //         assert_eq!(broker_connack, model_connack);
    //     }
    //     _ => todo!(),
    // }

    assert_eq!(sessions.len(), model.sessions.len());

    for (client_id, subscriptions, _) in sessions.into_iter().map(|session| session.into_parts()) {
        let model_session = model.sessions.remove(&client_id).expect("model_session");
        let mut model_topics = model_session.into_topics();

        assert_eq!(subscriptions.len(), model_topics.len());

        for topic in subscriptions.keys() {
            assert!(model_topics.remove(topic));
        }

        assert!(model_topics.is_empty());
    }

    assert!(model.sessions.is_empty());
}

fn client_id(client_id: &proto::ClientId) -> ClientId {
    match client_id {
        proto::ClientId::ServerGenerated => Uuid::new_v4().to_string().into(),
        proto::ClientId::IdWithCleanSession(id) => id.into(),
        proto::ClientId::IdWithExistingSession(id) => id.into(),
    }
}

#[derive(Debug)]
pub enum BrokerEvent {
    ConnReq(ClientId, proto::Connect),
    Disconnect(ClientId, proto::Disconnect),
    Subscribe(ClientId, proto::Subscribe),
    Unsubscribe(ClientId, proto::Unsubscribe),
}

// impl BrokerEvent {
//     fn client_id(&self) -> ClientId {
//         match self {
//             BrokerEvent::ConnReq(client_id, _) => client_id.clone(),
//         }
//     }

//     fn into_broker_event(self) -> ClientEvent {
//         match self {
//             BrokerEvent::ConnReq(_, connreq) => ClientEvent::ConnReq(connreq),
//         }
//     }

//     fn as_model_event(&self) -> ModelEventIn {
//         match self {
//             BrokerEvent::ConnReq(_, connreq) => connreq.connect().clone(),
//         }
//     }
// }

#[derive(Debug, Default)]
struct BrokerModel {
    pub sessions: HashMap<ClientId, ModelSession>,
}

impl BrokerModel {
    fn process_message(&mut self, client_id: ClientId, event: ModelEventIn) {
        //) -> Option<ModelEventOut> {
        match event {
            ModelEventIn::ConnReq(connreq) => self.process_connect(client_id, connreq),
            ModelEventIn::Disconnect(_) => self.process_disconnect(client_id),
            ModelEventIn::Subscribe(subscribe) => self.process_subscribe(client_id, subscribe),
            ModelEventIn::Unsubscribe(unsubscribe) => {
                self.process_unsubscribe(client_id, unsubscribe)
            }
        }
    }

    fn process_connect(&mut self, client_id: ClientId, connect: proto::Connect) {
        // -> ModelEventOut {
        let existing = self.sessions.remove(&client_id);
        // let session_present = existing.is_some();

        let session = match connect.client_id {
            proto::ClientId::ServerGenerated | proto::ClientId::IdWithCleanSession(_) => {
                ModelSession::Transient(HashSet::default())
            }
            proto::ClientId::IdWithExistingSession(_) => {
                let subscriptions = match existing {
                    Some(ModelSession::Transient(subscriptions)) => subscriptions,
                    Some(ModelSession::Persisted(subscriptions)) => subscriptions,
                    Some(ModelSession::Offline(subscriptions)) => subscriptions,
                    None => HashSet::default(),
                };
                ModelSession::Persisted(subscriptions)
            }
        };

        self.sessions.insert(client_id, session);

        // ModelEventOut::ConnAck(proto::ConnAck {
        //     session_present,
        //     return_code: proto::ConnectReturnCode::Accepted,
        // })
    }

    fn process_disconnect(&mut self, client_id: ClientId) {
        if let Some(session) = self.sessions.remove(&client_id) {
            let offline_session = match session {
                ModelSession::Transient(_) => None,
                ModelSession::Persisted(topics) => Some(ModelSession::Offline(topics)),
                ModelSession::Offline(topics) => Some(ModelSession::Offline(topics)),
            };

            if let Some(session) = offline_session {
                self.sessions.insert(client_id, session);
            }
        }
    }

    fn process_subscribe(&mut self, client_id: ClientId, subscribe: proto::Subscribe) {
        if let Some(session) = self.sessions.get_mut(&client_id) {
            let online_topics = match session {
                ModelSession::Transient(topics) => Some(topics),
                ModelSession::Persisted(topics) => Some(topics),
                ModelSession::Offline(_) => None,
            };

            if let Some(topics) = online_topics {
                for proto::SubscribeTo { topic_filter, .. } in subscribe.subscribe_to {
                    topics.insert(topic_filter);
                }
            }
        }
    }

    fn process_unsubscribe(&mut self, client_id: ClientId, unsubscribe: proto::Unsubscribe) {
        if let Some(session) = self.sessions.get_mut(&client_id) {
            let online_topics = match session {
                ModelSession::Transient(topics) => Some(topics),
                ModelSession::Persisted(topics) => Some(topics),
                ModelSession::Offline(_) => None,
            };

            if let Some(topics) = online_topics {
                for topic_filter in unsubscribe.unsubscribe_from {
                    topics.remove(&topic_filter);
                }
            }
        }
    }
}

#[derive(Debug)]
pub enum ModelSession {
    Transient(HashSet<String>),
    Persisted(HashSet<String>),
    Offline(HashSet<String>),
}

impl ModelSession {
    fn into_topics(self) -> HashSet<String> {
        match self {
            ModelSession::Transient(topics) => topics,
            ModelSession::Persisted(topics) => topics,
            ModelSession::Offline(topics) => topics,
        }
    }
}

enum ModelEventIn {
    ConnReq(proto::Connect),
    Disconnect(proto::Disconnect),
    Subscribe(proto::Subscribe),
    Unsubscribe(proto::Unsubscribe),
}

enum ModelEventOut {
    ConnAck(proto::ConnAck),
}

mod tests_util {
    use std::{sync::Arc, time::Duration};

    use mqtt3::proto;
    use proptest::{bool, collection::vec, num, prelude::*};

    use bytes::Bytes;
    use mqtt_broker::{
        ClientEvent, ClientId, ConnReq, Publish, Segment, Subscription, TopicFilter,
    };

    use crate::{client_id, BrokerEvent};

    // prop_compose! {
    //         pub fn arb_broker_event_new()(
    //             client_id in arb_client_id_weighted()
    //         ) -> BrokerEvent{
    //             prop_oneof![
    // arb_connect(client_id).prop_map(|p|
    //                 BrokerEvent::ConnReq(client_id(&p.client_id), p))
    //             ]
    //         }
    //     }

    pub fn arb_broker_event() -> impl Strategy<Value = BrokerEvent> {
        prop_oneof![
            arb_client_id_weighted()
                .prop_flat_map(|id| arb_connect(id)
                    .prop_map(|p| BrokerEvent::ConnReq(client_id(&p.client_id), p))),
            arb_client_id_weighted()
                .prop_map(|id| BrokerEvent::Disconnect(client_id(&id), proto::Disconnect)),
            arb_client_id_weighted()
                .prop_flat_map(|id| arb_subscribe()
                    .prop_map(move |p| BrokerEvent::Subscribe(client_id(&id), p))),
            arb_client_id_weighted().prop_flat_map(|id| arb_unsubscribe()
                .prop_map(move |p| BrokerEvent::Unsubscribe(client_id(&id), p))),
        ]
    }

    // prop_compose! {
    // pub fn arb_broker_connect()(
    //     connect in arb_connect()
    //     ) -> BrokerEvent{

    //         let client_id = client_id(&connect.client_id);
    //         let event = ClientEvent::ConnReq(ConnReq::new(
    //             client_id.clone(),
    //             connect.clone(),
    //             None,
    //             connection_handle,
    //         ));

    //         BrokerEvent::ConnReq(client_id, rx, connreq)
    //     }
    // }

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
            subscribe_to in proptest::collection::vec(arb_subscribe_to(), 1..10)
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
            unsubscribe_from in proptest::collection::vec(arb_topic_filter_weighted(), 1..10)
        ) -> proto::Unsubscribe {
            proto::Unsubscribe {
                packet_identifier,
                unsubscribe_from
            }
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

    pub fn arb_client_id_weighted() -> impl Strategy<Value = proto::ClientId> {
        let max = 10;
        prop_oneof![
            Just(proto::ClientId::ServerGenerated),
            "[a-zA-Z0-9]{1,23}".prop_map(|s| proto::ClientId::IdWithCleanSession(s)),
            "[a-zA-Z0-9]{1,23}".prop_map(|s| proto::ClientId::IdWithExistingSession(s)),
            (0..max).prop_map(|s| proto::ClientId::IdWithCleanSession(format!("client_{}", s))),
            (0..max).prop_map(|s| proto::ClientId::IdWithExistingSession(format!("client_{}", s)))
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
