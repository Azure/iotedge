use matches::assert_matches;

use futures_util::StreamExt;
use mqtt3::{
    proto::{QoS, SubscribeTo},
    Event, ReceivedPublication,
};
use mqtt_broker::{AuthId, BrokerBuilder};

use common::TestClientBuilder;

mod common;

#[tokio::test]
async fn basic_connect_clean_session() {
    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client = TestClientBuilder::new(address)
        .client_id("mqtt-smoke-tests")
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
async fn basic_pub_sub() {
    let topic = "topic/A";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, broker_task, address) = common::start_server(broker);

    let mut client = TestClientBuilder::new(address)
        .client_id("mqtt-smoke-tests")
        .build();

    client
        .subscribe(SubscribeTo {
            topic_filter: topic.into(),
            qos: QoS::AtMostOnce,
        })
        .await
        .expect("couldn't subscribe to a topic");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: topic.into(),
            qos: QoS::AtMostOnce,
            retain: false,
            payload: "qos 0".into(),
        })
        .await
        .expect("couldn't publish");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: topic.into(),
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: "qos 1".into(),
        })
        .await
        .expect("couldn't publish");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: topic.into(),
            qos: QoS::ExactlyOnce,
            retain: false,
            payload: "qos 2".into(),
        })
        .await
        .expect("couldn't publish");

    client.next().await; //skip connect event

    assert_matches!(client.next().await, Some(Event::SubscriptionUpdates(_)));
    assert_matches!(
        client.next().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == bytes::Bytes::from("qos 0")
    );
    assert_matches!(
        client.next().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == bytes::Bytes::from("qos 1")
    );
    assert_matches!(
        client.next().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == bytes::Bytes::from("qos 2")
    );

    client.shutdown().await.expect("couldn't shutdown");
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    client.join().await.expect("can't wait for the client");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}

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
        .client_id("mqtt-smoke-tests")
        .build();

    client
        .publish(mqtt3::proto::Publication {
            topic_name: topic_a.into(),
            qos: QoS::AtMostOnce,
            retain: true,
            payload: "r qos 0".into(),
        })
        .await
        .expect("couldn't publish");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: topic_b.into(),
            qos: QoS::AtLeastOnce,
            retain: true,
            payload: "r qos 1".into(),
        })
        .await
        .expect("couldn't publish");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: topic_c.into(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: "r qos 2".into(),
        })
        .await
        .expect("couldn't publish");

    client
        .subscribe(SubscribeTo {
            topic_filter: "topic/+".into(),
            qos: QoS::ExactlyOnce,
        })
        .await
        .expect("couldn't subscribe to a topic");

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
    assert_eq!(events[0], (bytes::Bytes::from("r qos 0"), true));
    assert_eq!(events[1], (bytes::Bytes::from("r qos 1"), true));
    assert_eq!(events[2], (bytes::Bytes::from("r qos 2"), true));

    shutdown_handle.shutdown().await.expect("couldn't shutdown");
    broker_shutdown.send(()).expect("couldn't shutdown broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}
