use matches::assert_matches;

use common::TestClientBuilder;
use mqtt3::proto::QoS::{AtLeastOnce, AtMostOnce, ExactlyOnce};
use mqtt3::proto::SubscribeTo;
use mqtt3::{Event, ReceivedPublication};
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
        .client_id("mqtt-smoke-tests")
        .build();

    assert_matches!(
        client.events_receiver.recv().await,
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
            qos: AtMostOnce,
        })
        .await
        .expect("couldn't subscribe to a topic");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: topic.into(),
            qos: AtMostOnce,
            retain: false,
            payload: "qos 0".into(),
        })
        .await
        .expect("couldn't publish");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: topic.into(),
            qos: AtLeastOnce,
            retain: false,
            payload: "qos 1".into(),
        })
        .await
        .expect("couldn't publish");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: topic.into(),
            qos: ExactlyOnce,
            retain: false,
            payload: "qos 2".into(),
        })
        .await
        .expect("couldn't publish");

    client.events_receiver.recv().await; //skip connect event

    assert_matches!(
        client.events_receiver.recv().await,
        Some(Event::SubscriptionUpdates(_))
    );
    assert_matches!(
        client.events_receiver.recv().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == bytes::Bytes::from("qos 0")
    );
    assert_matches!(
        client.events_receiver.recv().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == bytes::Bytes::from("qos 1")
    );
    assert_matches!(
        client.events_receiver.recv().await,
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
