use futures_util::{FutureExt, StreamExt};
use mqtt3::proto::QoS::{AtLeastOnce, AtMostOnce, ExactlyOnce};
use mqtt3::proto::SubscribeTo;
use mqtt3::{Event, ReceivedPublication};
use mqtt_broker::{AuthId, BrokerBuilder, Server};
use std::future::Future;

mod common;

#[tokio::test]
async fn basic_connect_clean_session() {
    const SERVER: &str = "localhost:1883";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let shutdown = futures_util::future::pending();
    let transports = vec![mqtt_broker::TransportBuilder::Tcp(SERVER)];
    let _ = tokio::spawn(Server::from_broker(broker).serve(transports, shutdown));

    let mut client = common::TestClientBuilder::default()
        .server(SERVER)
        .client_id("mqtt-smoke-tests")
        .build();

    matches::assert_matches!(
        client.events_receiver.recv().await,
        Some(Event::NewConnection { reset_session }) if reset_session
    );
}

#[tokio::test]
async fn basic_pub_sub() {
    const SERVER: &str = "localhost:1884";
    const TOPIC: &str = "topic/A";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let shutdown = futures_util::future::pending();
    let transports = vec![mqtt_broker::TransportBuilder::Tcp(SERVER)];
    let _ = tokio::spawn(Server::from_broker(broker).serve(transports, shutdown));

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

#[tokio::test]
async fn basic_pub_sub2() {
    const SERVER: &str = "localhost:1885";
    const TOPIC: &str = "topic/A";

    let broker = BrokerBuilder::default()
        .authenticator(|_| Ok(Some(AuthId::Anonymous)))
        .authorizer(|_| Ok(true))
        .build();

    let (broker_shutdown, rx) = tokio::sync::oneshot::channel::<()>();
    let transports = vec![mqtt_broker::TransportBuilder::Tcp(SERVER)];
    let broker_task = tokio::spawn(Server::from_broker(broker).serve(transports, rx.map(drop)));

    let mut client = mqtt3::Client::new(
        Some("client-mqtt-smoke".to_string()),
        None,
        None,
        move || {
            Box::pin(async move {
                let io = tokio::net::TcpStream::connect(&SERVER).await;
                io.map(|io| (io, None))
            })
        },
        std::time::Duration::from_secs(1),
        std::time::Duration::from_secs(60),
    );

    client.next().await;

    let t1 = client.subscribe(SubscribeTo {
        topic_filter: TOPIC.into(),
        qos: AtMostOnce,
    });

    matches::assert_matches!(client.next().await, Some(Ok(Event::SubscriptionUpdates(_))));
    t1.expect("can't subscribe");

    let _ = client.publish(mqtt3::proto::Publication {
        topic_name: TOPIC.into(),
        qos: AtMostOnce,
        retain: false,
        payload: "qos 0".into(),
    });

    matches::assert_matches!(
        client.next().await,
        Some(Ok(Event::Publication(ReceivedPublication{payload, ..}))) if payload == bytes::Bytes::from("qos 0")
    );

    let _ = client.publish(mqtt3::proto::Publication {
        topic_name: TOPIC.into(),
        qos: AtLeastOnce,
        retain: false,
        payload: "qos 1".into(),
    });

    matches::assert_matches!(
        client.next().await,
        Some(Ok(Event::Publication(ReceivedPublication{payload, ..}))) if payload == bytes::Bytes::from("qos 1")
    );

    let _ = client.publish(mqtt3::proto::Publication {
        topic_name: TOPIC.into(),
        qos: ExactlyOnce,
        retain: false,
        payload: "qos 2".into(),
    });

    matches::assert_matches!(
        client.next().await,
        Some(Ok(Event::Publication(ReceivedPublication{payload, ..}))) if payload == bytes::Bytes::from("qos 2")
    );

    broker_shutdown.send(()).expect("couldn't shutdown broker");
    broker_task
        .await
        .unwrap()
        .expect("can't wait for the broker");
}
