use std::time::Duration;

use bytes::Bytes;
use futures_util::StreamExt;
use matches::assert_matches;
use mqtt3::{
    proto::{ClientId, Publication, QoS},
    Event, ReceivedPublication,
};

use common::TestClientBuilder;
use mqtt_broker::{AuthId, BrokerBuilder};

mod common;

#[tokio::test]
async fn basic_connect_clean_session() {
    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client = TestClientBuilder::new(address)
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
        .build();

    assert_matches!(
        client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );

    client.shutdown().await;
    broker_shutdown.send(()).expect("can't stop the broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}

/// Scenario:
///	- Client connects with clean session flag = false.
///	- Expects to see `reset_session` flag = true (brand new session on the server).
///	- Client disconnects.
///	- Client connects with clean session flag = false.
///	- Expects to see `reset_session` flag = false (existing session on the server).
#[tokio::test]
async fn basic_connect_existing_session() {
    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests".into()))
        .build();

    assert_matches!(
        client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );

    client.shutdown().await;

    let mut client = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests".into()))
        .build();

    assert_matches!(
        client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: false
        })
    );

    client.shutdown().await;
    broker_shutdown.send(()).expect("can't stop the broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}

/// Scenario:
///	- Client connects with clean session.
///	- Client subscribes to a TopicA
///	- Client publishes to a TopicA with QoS 0
///	- Client publishes to a TopicA with QoS 1
///	- Client publishes to a TopicA with QoS 2
///	- Expects to receive back three messages.
#[tokio::test]
async fn basic_pub_sub() {
    let topic = "topic/A";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client = TestClientBuilder::new(address)
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
        .build();

    client.subscribe(topic, QoS::ExactlyOnce).await;

    client.publish_qos0(topic, "qos 0", false).await;
    client.publish_qos1(topic, "qos 1", false).await;
    client.publish_qos2(topic, "qos 2", false).await;

    assert_matches!(
        client.subscriptions().recv().await,
        Some(Event::SubscriptionUpdates(_))
    );
    assert_matches!(
        client.publications().recv().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("qos 0")
    );
    assert_matches!(
        client.publications().recv().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("qos 1")
    );
    assert_matches!(
        client.publications().recv().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("qos 2")
    );

    client.shutdown().await;
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}

/// Scenario:
/// - Client A connects with clean session.
/// - Client A publishes to a Topic/A with RETAIN = true and QoS 0
/// - Client A publishes to a Topic/B with RETAIN = true and QoS 1
/// - Client A publishes to a Topic/C with RETAIN = true and QoS 2
/// - Client A subscribes to a Topic/+
/// - Expects to receive three messages w/ RETAIN = true
/// - Expects three retain messages in the broker state.
#[tokio::test]
async fn retained_messages() {
    let topic_a = "topic/A";
    let topic_b = "topic/B";
    let topic_c = "topic/C";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client = TestClientBuilder::new(address)
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
        .build();

    client.publish_qos0(topic_a, "r qos 0", true).await;
    client.publish_qos1(topic_b, "r qos 1", true).await;
    client.publish_qos2(topic_c, "r qos 2", true).await;

    client.subscribe("topic/+", QoS::ExactlyOnce).await;

    assert_matches!(
        client.subscriptions().recv().await,
        Some(Event::SubscriptionUpdates(_))
    );

    // read and map 3 expected events from the stream
    let mut events: Vec<_> = client
        .publications()
        .take(3)
        .map(|p| (p.payload, p.retain))
        .collect()
        .await;

    // sort by payload for ease of comparison.
    events.sort_by_key(|e| e.0.clone());

    assert_eq!(3, events.len());
    assert_eq!(events[0], (Bytes::from("r qos 0"), true));
    assert_eq!(events[1], (Bytes::from("r qos 1"), true));
    assert_eq!(events[2], (Bytes::from("r qos 2"), true));

    client.shutdown().await;
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    let state = broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");

    // inspect broker state after shutdown to
    // deterministically verify presence of retained messages.
    let (retained, _) = state.into_parts();
    assert_eq!(retained.len(), 3);
}

/// Scenario:
/// - Client A connects with clean session.
/// - Client A publishes to a Topic/A with RETAIN = true / QoS 0 / Some payload
/// - Client A publishes to a Topic/A with RETAIN = true / QoS 0 / Zero-length payload
/// - Client A publishes to a Topic/B with RETAIN = true / QoS 1 / Some payload
/// - Client A publishes to a Topic/B with RETAIN = true / QoS 1 / Zero-length payload
/// - Client A publishes to a Topic/C with RETAIN = true / QoS 2 / Some payload
/// - Client A publishes to a Topic/C with RETAIN = true / QoS 2 / Zero-length payload
/// - Client A subscribes to a Topic/+
/// - Expects to receive no messages.
/// - Expects no retain messages in the broker state.
#[tokio::test]
async fn retained_messages_zero_payload() {
    let topic_a = "topic/A";
    let topic_b = "topic/B";
    let topic_c = "topic/C";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client = TestClientBuilder::new(address)
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
        .build();

    client.publish_qos0(topic_a, "r qos 0", true).await;
    client.publish_qos0(topic_a, "", true).await;

    client.publish_qos1(topic_b, "r qos 1", true).await;
    client.publish_qos1(topic_b, "", true).await;

    client.publish_qos2(topic_c, "r qos 2", true).await;
    client.publish_qos2(topic_c, "", true).await;

    client.subscribe("topic/+", QoS::ExactlyOnce).await;

    assert_eq!(client.publications().try_recv().ok(), None); // no new message expected.

    client.shutdown().await;
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    let state = broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");

    // inspect broker state after shutdown to
    // deterministically verify absence of retained messages.
    let (retained, _) = state.into_parts();
    assert_eq!(retained.len(), 0);
}

/// Scenario:
/// - Client A connects with clean session, will message for TopicA.
/// - Client B connects with clean session and subscribes to TopicA
/// - Client A terminates abruptly.
/// - Expects client B to receive will message.
#[tokio::test]
async fn will_message() {
    let topic = "topic/A";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client_b = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-b".into()))
        .build();

    client_b.subscribe(topic, QoS::AtLeastOnce).await;

    client_b.subscriptions().recv().await; // wait for SubAck.

    let mut client_a = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-a".into()))
        .will(Publication {
            topic_name: topic.into(),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: "will_msg_a".into(),
        })
        .build();

    client_a.connections().recv().await; // wait for ConnAck

    client_a.terminate().await;

    // expect will message
    assert_matches!(
        client_b.publications().recv().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("will_msg_a")
    );

    client_b.shutdown().await;
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}

/// Scenario:
/// - Client A connects with clean session = false and subscribes to Topic/+.
/// - Client A disconnects
/// - Client B connects with clean session
/// - Client B publishes to a Topic/A with QoS 0
/// - Client B publishes to a Topic/B with QoS 1
/// - Client B publishes to a Topic/C with QoS 2
/// - Client B disconnects
/// - Client A connects with clean session = false
/// - Expects session present = 0x01
/// - Expects to receive three messages (QoS 1, 2, and including QoS 0).
#[tokio::test]
async fn offline_messages() {
    let topic_a = "topic/A";
    let topic_b = "topic/B";
    let topic_c = "topic/C";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client_a = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests-a".into()))
        .build();

    client_a.subscribe("topic/+", QoS::ExactlyOnce).await;

    client_a.shutdown().await;

    let mut client_b = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-b".into()))
        .build();

    client_b.publish_qos0(topic_a, "o qos 0", false).await;
    client_b.publish_qos1(topic_b, "o qos 1", false).await;
    client_b.publish_qos2(topic_c, "o qos 2", false).await;

    let mut client_a = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests-a".into()))
        .build();

    // expects existing session.
    assert_matches!(
        client_a.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: false
        })
    );

    // read and map 3 expected publications from the stream
    let mut events = client_a
        .publications()
        .take(3)
        .map(|p| (p.payload))
        .collect::<Vec<_>>()
        .await;

    // sort by payload for ease of comparison.
    events.sort();

    assert_eq!(3, events.len());
    assert_eq!(events[0], Bytes::from("o qos 0"));
    assert_eq!(events[1], Bytes::from("o qos 1"));
    assert_eq!(events[2], Bytes::from("o qos 2"));

    client_a.shutdown().await;
    client_b.shutdown().await;
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}

/// Scenario:
/// - Client A connects with clean session.
/// - Client A subscribes to Topic/A
/// - Client A subscribes to Topic/+
/// - Client A subscribes to Topic/#
/// - Client B connects with clean session
/// - Client B publishes to a Topic/A three messages with QoS 0, QoS 1, QoS 2
/// - Client A Expects to receive ONLY three messages.
#[tokio::test]
async fn overlapping_subscriptions() {
    let topic = "topic/A";
    let topic_filter_pound = "topic/#";
    let topic_filter_plus = "topic/+";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client_a = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-a".into()))
        .build();

    client_a.subscribe(topic, QoS::AtMostOnce).await;
    client_a
        .subscribe(topic_filter_pound, QoS::AtLeastOnce)
        .await;
    client_a
        .subscribe(topic_filter_plus, QoS::ExactlyOnce)
        .await;

    let mut client_b = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-b".into()))
        .build();

    client_b.publish_qos0(topic, "overlap qos 0", false).await;
    client_b.publish_qos1(topic, "overlap qos 1", false).await;
    client_b.publish_qos2(topic, "overlap qos 2", false).await;

    let mut events: Vec<_> = client_a
        .publications()
        .take(3)
        .map(|p| (p.payload))
        .collect()
        .await;

    // need to wait till all messages are processed.
    tokio::time::delay_for(Duration::from_secs(1)).await;

    assert_eq!(client_a.publications().try_recv().ok(), None); // no new message expected.

    events.sort();

    assert_eq!(3, events.len());
    assert_eq!(events[0], Bytes::from("overlap qos 0"));
    assert_eq!(events[1], Bytes::from("overlap qos 1"));
    assert_eq!(events[2], Bytes::from("overlap qos 2"));

    client_a.shutdown().await;
    client_b.shutdown().await;
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}
