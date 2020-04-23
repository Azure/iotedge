use futures_util::FutureExt;
use mqtt3::proto::QoS::{AtLeastOnce, AtMostOnce, ExactlyOnce};
use mqtt3::proto::SubscribeTo;
use mqtt3::{Event, ReceivedPublication};
use mqtt_broker::{AuthId, BrokerBuilder, Server};

mod common;

#[tokio::test]
async fn basic_connect_clean_session() {
    const SERVER: &str = "localhost:1883";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (shutdown, rx) = tokio::sync::oneshot::channel::<()>();
    let transports = vec![mqtt_broker::TransportBuilder::Tcp(SERVER)];
    let broker_task = tokio::spawn(Server::from_broker(broker).serve(transports, rx.map(drop)));

    let mut client = common::TestClientBuilder::default()
        .server(SERVER)
        .client_id("mqtt-smoke-tests")
        .build();

    matches::assert_matches!(
        client.events_receiver.recv().await,
        Some(Event::NewConnection { reset_session }) if reset_session
    );

    shutdown.send(()).expect("can't stop the broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}

#[tokio::test]
async fn basic_pub_sub() {
    const SERVER: &str = "localhost:1884";
    const TOPIC: &str = "topic/A";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, rx) = tokio::sync::oneshot::channel::<()>();
    let transports = vec![mqtt_broker::TransportBuilder::Tcp(SERVER)];
    let broker_task = tokio::spawn(Server::from_broker(broker).serve(transports, rx.map(drop)));

    let mut client = common::TestClientBuilder::default()
        .server(SERVER)
        .client_id("mqtt-smoke-tests")
        .build();

    client
        .subscribe(SubscribeTo {
            topic_filter: TOPIC.into(),
            qos: AtMostOnce,
        })
        .await
        .expect("couldn't subscribe to a topic");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: TOPIC.into(),
            qos: AtMostOnce,
            retain: false,
            payload: "qos 0".into(),
        })
        .await
        .expect("couldn't publish");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: TOPIC.into(),
            qos: AtLeastOnce,
            retain: false,
            payload: "qos 1".into(),
        })
        .await
        .expect("couldn't publish");

    client
        .publish(mqtt3::proto::Publication {
            topic_name: TOPIC.into(),
            qos: ExactlyOnce,
            retain: false,
            payload: "qos 2".into(),
        })
        .await
        .expect("couldn't publish");

    client.events_receiver.recv().await; //skip connect event

    matches::assert_matches!(
        client.events_receiver.recv().await,
        Some(Event::SubscriptionUpdates(_))
    );
    matches::assert_matches!(
        client.events_receiver.recv().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == bytes::Bytes::from("qos 0")
    );
    matches::assert_matches!(
        client.events_receiver.recv().await,
        Some(Event::Publication(ReceivedPublication{payload, ..})) if payload == bytes::Bytes::from("qos 1")
    );
    matches::assert_matches!(
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