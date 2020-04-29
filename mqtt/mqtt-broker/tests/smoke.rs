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
        client.next().await,
        Some(Event::NewConnection { reset_session }) if reset_session
    );

    broker_shutdown.send(()).expect("can't stop the broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}

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
        client.next().await,
        Some(Event::NewConnection { reset_session }) if reset_session
    );

    client.shutdown().await;

    assert_eq!(client.next().await, None); //drain event queue.

    let mut client = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests".into()))
        .build();

    assert_matches!(
        client.next().await,
        Some(Event::NewConnection { reset_session }) if !reset_session
    );

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

    client.next().await; //skip connect event

    assert_matches!(client.next().await, Some(Event::SubscriptionUpdates(_)));
    assert_matches!(
        client.next().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == Bytes::from("qos 0")
    );
    assert_matches!(
        client.next().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == Bytes::from("qos 1")
    );
    assert_matches!(
        client.next().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == Bytes::from("qos 2")
    );

    client.shutdown().await;
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    client.join().await;
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

    client.next().await; //skip connect event

    assert_matches!(
        client.next().await,
        Some(Event::SubscriptionUpdates(_))
    );

    let mut shutdown_handle = client.shutdown_handle();

    // read and map 3 expected events from the stream
    let mut events = client
        .take(3)
        .filter_map(|e| async {
            match e {
                Event::Publication(publication) => Some((publication.payload, publication.retain)),
                _ => None,
            }
        })
        .collect::<Vec<_>>()
        .await;

    // sort by payload for ease of comparison.
    events.sort_by_key(|e| e.0.clone());

    assert_eq!(3, events.len());
    assert_eq!(events[0], (Bytes::from("r qos 0"), true));
    assert_eq!(events[1], (Bytes::from("r qos 1"), true));
    assert_eq!(events[2], (Bytes::from("r qos 2"), true));

    shutdown_handle.shutdown().await.expect("couldn't shutdown");
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    let state = broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");

    // inspect broker state after shutdown to
    // deterministically verify presense of retained messages.
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

    client.next().await; //skip connect event

    client.publish_qos0(topic_a, "r qos 0", true).await;
    client.publish_qos0(topic_a, "", true).await;

    client.publish_qos1(topic_b, "r qos 1", true).await;
    client.publish_qos1(topic_b, "", true).await;

    client.publish_qos2(topic_c, "r qos 2", true).await;
    client.publish_qos2(topic_c, "", true).await;

    client.subscribe("topic/+", QoS::ExactlyOnce).await;

    assert_eq!(client.try_recv().await, None); // no new message expected.

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

    let mut client_a = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-a".into()))
        .will(Publication {
            topic_name: topic.into(),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: "will_msg_a".into(),
        })
        .keep_alive(Duration::from_secs(1))
        .build();

    client_a.next().await; //skip connect event

    let mut client_b = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-b".into()))
        .build();

    client_a.subscribe(topic, QoS::ExactlyOnce).await;

    client_b.next().await; //skip connect event
    client_b.next().await; //skip subscribe event

    client_a.terminate().await;

    // expect will message
    assert_matches!(
        client_b.next().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == Bytes::from("will_msg_a")
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
        .keep_alive(Duration::from_secs(1))
        .build();

    client_a.subscribe("topic/+", QoS::ExactlyOnce).await;

    client_a.next().await; //skip connect event

    client_a.shutdown().await;

    assert_eq!(client_a.next().await, None); //drain event queue.

    let mut client_b = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-b".into()))
        .build();

    client_b.publish_qos0(topic_a, "o qos 0", false).await;
    client_b.publish_qos1(topic_b, "o qos 1", false).await;
    client_b.publish_qos2(topic_c, "o qos 2", false).await;

    let mut client_a = TestClientBuilder::new(address.clone())
        .client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests-a".into()))
        .keep_alive(Duration::from_secs(10))
        .build();

    // expects existing session.
    assert_matches!(
        client_a.next().await,
        Some(Event::NewConnection { reset_session }) if !reset_session
    );

    // read and map 3 expected events from the stream
    let mut events = client_a
        .take(3)
        .filter_map(|e| async {
            match e {
                Event::Publication(publication) => Some(publication.payload),
                _ => None,
            }
        })
        .collect::<Vec<_>>()
        .await;

    // sort by payload for ease of comparison.
    events.sort();

    assert_eq!(3, events.len());
    assert_eq!(events[0], Bytes::from("o qos 0"));
    assert_eq!(events[1], Bytes::from("o qos 1"));
    assert_eq!(events[2], Bytes::from("o qos 2"));

    client_b.shutdown().await;
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}
