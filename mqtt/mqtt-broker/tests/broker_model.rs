use std::collections::{HashMap, HashSet};

use proptest::{prop_oneof, proptest, strategy::Strategy};
use tokio::sync::mpsc::{self, UnboundedReceiver};
use uuid::Uuid;

use mqtt3::proto;
use mqtt_broker::{
    auth::AllowAll,
    proptest::{arb_client_id_weighted, arb_connect, arb_subscribe, arb_unsubscribe},
    Auth, AuthId, BrokerBuilder, ClientEvent, ClientId, ConnReq, ConnectionHandle, Message,
};

proptest! {
    /// Model based test to check whether broker can manage arbitrary packet sequence while
    /// maintaining sessions and subscriptions for clients properly.
    ///
    /// Model-based tests contain a simplified model of MQTT broker that handles some MQTT packets.
    /// Randomly generated MQTT packets processed by both: broker and broker model. Later tests verify
    /// that the broker and its model processed packet in a similar way.
    ///
    /// Currently, there is only on a test case that handles a randomly generated sequence of
    /// CONNECT, DISCONNECT, SUBSCRIBE, UNSUBSCRIBE packets with pseudo-random data and also
    /// DropConnection, CloseSession events. In the end, the test verifies that both the broker and
    /// its model contain the same number of sessions and same number of subscriptions.
    #[test]
    fn broker_manages_sessions(
        events in proptest::collection::vec(arb_broker_event(), 1..50)
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
    let mut broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let mut model = BrokerModel::default();

    let mut clients = Vec::new();
    for event in events {
        let (client_id, broker_event, model_event) = into_events(event, &mut clients);

        broker
            .process_client_event(client_id.clone(), broker_event)
            .expect("process message");

        model.process_message(client_id, model_event);
    }

    let (_retained, sessions) = broker.clone_state().into_parts();

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

fn into_events(
    event: BrokerEvent,
    clients: &mut Vec<UnboundedReceiver<Message>>,
) -> (ClientId, ClientEvent, ModelEventIn) {
    match event {
        BrokerEvent::ConnReq(client_id, connect) => {
            let (tx, rx) = mpsc::unbounded_channel();
            clients.push(rx);

            let connection_handle = ConnectionHandle::from_sender(tx);
            let connreq = ConnReq::new(
                client_id.clone(),
                "127.0.0.1:12345".parse().expect("peer_addr"),
                connect.clone(),
                Auth::Identity(AuthId::Anonymous),
                connection_handle,
            );
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
        BrokerEvent::CloseSession(client_id) => (
            client_id,
            ClientEvent::CloseSession,
            ModelEventIn::CloseSession,
        ),
        BrokerEvent::DropConnection(client_id) => (
            client_id,
            ClientEvent::DropConnection,
            ModelEventIn::DropConnection,
        ),
    }
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
    CloseSession(ClientId),
    DropConnection(ClientId),
}

#[derive(Debug, Default)]
struct BrokerModel {
    pub sessions: HashMap<ClientId, ModelSession>,
}

impl BrokerModel {
    fn process_message(&mut self, client_id: ClientId, event: ModelEventIn) {
        match event {
            ModelEventIn::ConnReq(connreq) => self.process_connect(client_id, connreq),
            ModelEventIn::Disconnect(_) => self.process_disconnect(client_id),
            ModelEventIn::Subscribe(subscribe) => self.process_subscribe(client_id, subscribe),
            ModelEventIn::Unsubscribe(unsubscribe) => {
                self.process_unsubscribe(client_id, unsubscribe)
            }
            ModelEventIn::CloseSession => self.process_close_session(client_id),
            ModelEventIn::DropConnection => self.process_drop_connection(client_id),
        }
    }

    fn process_connect(&mut self, client_id: ClientId, connect: proto::Connect) {
        let existing = self.sessions.remove(&client_id);

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
    }

    fn process_disconnect(&mut self, client_id: ClientId) {
        self.close_session(client_id);
    }

    fn process_close_session(&mut self, client_id: ClientId) {
        self.close_session(client_id);
    }

    fn process_drop_connection(&mut self, client_id: ClientId) {
        self.close_session(client_id);
    }

    fn close_session(&mut self, client_id: ClientId) {
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
    CloseSession,
    DropConnection,
}

pub fn arb_broker_event() -> impl Strategy<Value = BrokerEvent> {
    prop_oneof![
        arb_client_id_weighted().prop_flat_map(
            |id| arb_connect(id).prop_map(|p| BrokerEvent::ConnReq(client_id(&p.client_id), p))
        ),
        arb_client_id_weighted()
            .prop_map(|id| BrokerEvent::Disconnect(client_id(&id), proto::Disconnect)),
        arb_client_id_weighted().prop_flat_map(
            |id| arb_subscribe().prop_map(move |p| BrokerEvent::Subscribe(client_id(&id), p))
        ),
        arb_client_id_weighted()
            .prop_flat_map(|id| arb_unsubscribe()
                .prop_map(move |p| BrokerEvent::Unsubscribe(client_id(&id), p))),
        arb_client_id_weighted().prop_map(|id| BrokerEvent::CloseSession(client_id(&id))),
        arb_client_id_weighted().prop_map(|id| BrokerEvent::DropConnection(client_id(&id))),
    ]
}
