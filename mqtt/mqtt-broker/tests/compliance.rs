///! This module contains tests that verify MQTT protocol compliance.
///! (qos, ordering, re-delivery, retained messages, will, etc...)
///! Only MQTT-protocol related tests should be added here.
///!
///! For other tests not related to MQTT protocol (like broker config, storage, cleanup)
///! please use `integration.rs`.
use std::time::Duration;

use bytes::Bytes;
use futures_util::StreamExt;
use matches::assert_matches;

use mqtt3::{
    proto::{
        ClientId, ConnAck, Connect, ConnectReturnCode, ConnectionRefusedReason, Packet,
        PacketIdentifier, PacketIdentifierDupQoS, PingReq, PubAck, Publication, Publish, QoS,
        SubAck, SubAckQos, Subscribe, SubscribeTo,
    },
    Event, ReceivedPublication, PROTOCOL_LEVEL, PROTOCOL_NAME,
};
use mqtt_broker::{auth::AllowAll, BrokerBuilder};
use mqtt_broker_tests_util::{
    client::TestClientBuilder,
    packet_stream::PacketStream,
    server::{start_server, DummyAuthenticator},
};

#[tokio::test]
async fn basic_connect_clean_session() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
        .build();

    assert_eq!(
        client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );

    client.shutdown().await;
}

/// Scenario:
///	- Client connects with clean session flag = false.
///	- Expects to see `reset_session` flag = true (brand new session on the server).
///	- Client disconnects.
///	- Client connects with clean session flag = false.
///	- Expects to see `reset_session` flag = false (existing session on the server).
#[tokio::test]
async fn basic_connect_existing_session() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests".into()))
        .build();

    assert_eq!(
        client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: true
        })
    );

    client.shutdown().await;

    let mut client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests".into()))
        .build();

    assert_eq!(
        client.connections().recv().await,
        Some(Event::NewConnection {
            reset_session: false
        })
    );

    client.shutdown().await;
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

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
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

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let mut server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
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
    let state = server_handle.shutdown().await;

    // inspect broker state after shutdown to
    // deterministically verify presence of retained messages.
    // filter out edgehub messages
    let (retained, _) = state.into_parts();
    assert_eq!(
        retained
            .iter()
            .filter(|(topic, _)| !topic.contains("edgehub"))
            .count(),
        3
    );
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

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let mut server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
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
    let state = server_handle.shutdown().await;

    // inspect broker state after shutdown to
    // deterministically verify absence of retained messages.
    // filter out edgehub messages
    let (retained, _) = state.into_parts();
    assert_eq!(
        retained
            .iter()
            .filter(|(topic, _)| !topic.contains("edgehub"))
            .count(),
        0
    );
}

/// Scenario:
/// - Client A connects with clean session.
/// - Client A publishes to a Topic/A with RETAIN = true / QoS 0 / Some payload
/// - Client A publishes to a Topic/B with RETAIN = true / QoS 1 / Some payload
/// - Client A publishes to a Topic/C with RETAIN = true / QoS 2 / Some payload
/// - Broker properly restarts.
/// - Client A subscribes to a Topic/+
/// - Expects to receive three messages.
#[tokio::test]
async fn retained_messages_persisted_on_broker_restart() {
    let topic_a = "topic/A";
    let topic_b = "topic/B";
    let topic_c = "topic/C";

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let mut server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
        .build();

    client.publish_qos0(topic_a, "r qos 0", true).await;
    client.publish_qos1(topic_b, "r qos 1", true).await;
    client.publish_qos2(topic_c, "r qos 2", true).await;

    // need to wait till all messages are processed.
    tokio::time::delay_for(Duration::from_secs(1)).await;

    client.shutdown().await;
    let state = server_handle.shutdown().await;

    // restart broker with saved state
    let broker = BrokerBuilder::default()
        .with_state(state)
        .with_authorizer(AllowAll)
        .build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests".into()))
        .build();

    client.subscribe("topic/+", QoS::ExactlyOnce).await;

    // read and map 3 expected messages from the stream
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
}

/// Scenario:
/// - Client A connects with clean session, will message for TopicA.
/// - Client B connects with clean session and subscribes to TopicA
/// - Client A terminates abruptly.
/// - Expects client B to receive will message.
#[tokio::test]
async fn will_message() {
    let topic = "topic/A";

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client_b = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-b".into()))
        .build();

    client_b.subscribe(topic, QoS::AtLeastOnce).await;

    client_b.subscriptions().recv().await; // wait for SubAck.

    let mut client_a = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-a".into()))
        .with_will(Publication {
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

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client_a = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests-a".into()))
        .build();

    client_a.subscribe("topic/+", QoS::ExactlyOnce).await;

    client_a.shutdown().await;

    let mut client_b = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-b".into()))
        .build();

    client_b.publish_qos0(topic_a, "o qos 0", false).await;
    client_b.publish_qos1(topic_b, "o qos 1", false).await;
    client_b.publish_qos2(topic_c, "o qos 2", false).await;

    let mut client_a = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests-a".into()))
        .build();

    // expects existing session.
    assert_eq!(
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
}

#[tokio::test]
async fn offline_messages_persisted_on_broker_restart() {
    let topic_a = "topic/A";
    let topic_b = "topic/B";
    let topic_c = "topic/C";

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let mut server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client_a = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests-a".into()))
        .build();

    client_a.subscribe("topic/+", QoS::ExactlyOnce).await;

    client_a.shutdown().await;

    let mut client_b = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-b".into()))
        .build();

    client_b.publish_qos0(topic_a, "o qos 0", false).await;
    client_b.publish_qos1(topic_b, "o qos 1", false).await;
    client_b.publish_qos2(topic_c, "o qos 2", false).await;

    // need to wait till all messages are processed.
    tokio::time::delay_for(Duration::from_secs(1)).await;

    client_b.shutdown().await;
    let state = server_handle.shutdown().await;

    // restart broker with saved state
    let broker = BrokerBuilder::default()
        .with_state(state)
        .with_authorizer(AllowAll)
        .build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client_a = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("mqtt-smoke-tests-a".into()))
        .build();

    // expects existing session.
    assert_eq!(
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
}

/// Scenario:
/// - Client A connects with clean session
/// - Client B connects with existing session
/// - Client B sunbscribes to a topic
/// - Client A sends QoS0 and several QoS1 packets
/// - Client B receives packets and don't send PUBACKs
/// - Client B reconnects
/// - Client B expects to receive only QoS1 packets with dup=true in correct order
#[tokio::test]
async fn inflight_qos1_messages_redelivered_on_reconnect() {
    let topic_a = "topic/A";

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client_a = PacketStream::connect(
        ClientId::IdWithCleanSession("test-client-a".into()),
        server_handle.address(),
        None,
        None,
    )
    .await;

    client_a.next().await; // skip connack

    let mut client_b = PacketStream::connect(
        ClientId::IdWithExistingSession("test-client-b".into()),
        server_handle.address(),
        None,
        None,
    )
    .await;

    client_b.next().await; // skip connack

    client_b
        .send_packet(Packet::Subscribe(Subscribe {
            packet_identifier: PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![SubscribeTo {
                topic_filter: topic_a.into(),
                qos: QoS::AtLeastOnce,
            }],
        }))
        .await;

    assert_eq!(
        client_b.next().await,
        Some(Packet::SubAck(SubAck {
            packet_identifier: PacketIdentifier::new(1).unwrap(),
            qos: vec![SubAckQos::Success(QoS::AtLeastOnce)]
        }))
    );

    client_a
        .send_publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtMostOnce,
            retain: false,
            topic_name: topic_a.into(),
            payload: Bytes::from("qos 0"),
        })
        .await;

    const QOS1_MESSAGES: u16 = 3;

    for i in 1..=QOS1_MESSAGES {
        client_a
            .send_publish(Publish {
                packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(
                    PacketIdentifier::new(i).unwrap(),
                    false,
                ),
                retain: false,
                topic_name: topic_a.into(),
                payload: Bytes::from(format!("qos 1-{}", i)),
            })
            .await;
    }

    // receive all messages but don't send puback for QoS1/2.
    assert_matches!(
        client_b.next().await,
        Some(Packet::Publish(Publish {
            payload,
            ..
        })) if payload == Bytes::from("qos 0")
    );

    for i in 1..=QOS1_MESSAGES {
        let publish = match client_b.next().await {
            Some(Packet::Publish(publish)) => publish,
            x => panic!("Expected publish but {:?} found", x),
        };

        assert_matches!(
            publish.packet_identifier_dup_qos,
            PacketIdentifierDupQoS::AtLeastOnce(_, false)
        );
        assert_eq!(publish.payload, Bytes::from(format!("qos 1-{0}", i)));
    }

    // disconnect client_b;
    drop(client_b);

    // reconnect client_b
    let mut client_b = PacketStream::connect(
        ClientId::IdWithExistingSession("test-client-b".into()),
        server_handle.address(),
        None,
        None,
    )
    .await;

    assert_eq!(
        client_b.next().await,
        Some(Packet::ConnAck(ConnAck {
            session_present: true,
            return_code: ConnectReturnCode::Accepted
        }))
    );

    // expect messages to be redelivered (QoS 1/2).
    for i in 1..=QOS1_MESSAGES {
        let publish = match client_b.next().await {
            Some(Packet::Publish(publish)) => publish,
            x => panic!("Expected publish but {:?} found", x),
        };

        assert_matches!(
            publish.packet_identifier_dup_qos,
            PacketIdentifierDupQoS::AtLeastOnce(_, true)
        );
        assert_eq!(publish.payload, Bytes::from(format!("qos 1-{0}", i)));
    }
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

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client_a = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-a".into()))
        .build();

    client_a.subscribe(topic, QoS::AtMostOnce).await;
    client_a
        .subscribe(topic_filter_pound, QoS::AtLeastOnce)
        .await;
    client_a
        .subscribe(topic_filter_plus, QoS::ExactlyOnce)
        .await;

    let mut client_b = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("mqtt-smoke-tests-b".into()))
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
}

#[tokio::test]
async fn wrong_first_packet_connection_dropped() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = PacketStream::open(server_handle.address()).await;
    client.send_packet(Packet::PingReq(PingReq)).await;

    assert_eq!(client.next().await, None); // None means stream closed.
}

#[tokio::test]
async fn duplicate_connect_packet_connection_dropped() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = PacketStream::open(server_handle.address()).await;
    client
        .send_connect(Connect {
            client_id: ClientId::IdWithCleanSession("test-client".into()),
            username: None,
            password: None,
            will: None,
            keep_alive: Duration::from_secs(30),
            protocol_name: PROTOCOL_NAME.into(),
            protocol_level: PROTOCOL_LEVEL,
        })
        .await;

    assert_eq!(
        client.next().await,
        Some(Packet::ConnAck(ConnAck {
            return_code: ConnectReturnCode::Accepted,
            session_present: false
        }))
    );

    client
        .send_connect(Connect {
            client_id: ClientId::IdWithCleanSession("test-client".into()),
            username: None,
            password: None,
            will: None,
            keep_alive: Duration::from_secs(30),
            protocol_name: PROTOCOL_NAME.into(),
            protocol_level: PROTOCOL_LEVEL,
        })
        .await;

    assert_eq!(client.next().await, None); // None means stream closed.
}

#[tokio::test]
async fn wrong_protocol_name_connection_dropped() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = PacketStream::open(server_handle.address()).await;
    client
        .send_connect(Connect {
            client_id: ClientId::IdWithCleanSession("test-client".into()),
            protocol_name: "HELLO".into(),
            username: None,
            password: None,
            will: None,
            keep_alive: Duration::from_secs(30),
            protocol_level: PROTOCOL_LEVEL,
        })
        .await;

    assert_eq!(client.next().await, None); // None means stream closed.
}

#[tokio::test]
async fn wrong_protocol_version_rejected() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = PacketStream::open(server_handle.address()).await;
    client
        .send_connect(Connect {
            client_id: ClientId::IdWithCleanSession("test-client".into()),
            protocol_level: 80u8,
            username: None,
            password: None,
            will: None,
            keep_alive: Duration::from_secs(30),
            protocol_name: PROTOCOL_NAME.into(),
        })
        .await;

    assert_matches!(
        client.next().await,
        Some(Packet::ConnAck(ConnAck {
            return_code: ConnectReturnCode::Refused(
                ConnectionRefusedReason::UnacceptableProtocolVersion
            ),
            ..
        }))
    );
}

#[tokio::test]
async fn qos1_puback_should_be_in_order() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut client = PacketStream::connect(
        ClientId::IdWithCleanSession("test-client".into()),
        server_handle.address(),
        None,
        None,
    )
    .await;

    client.next().await; // skip connack

    client
        .send_publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(
                PacketIdentifier::new(1).unwrap(),
                false,
            ),
            retain: false,
            topic_name: "topic/A".into(),
            payload: Bytes::from("qos 1"),
        })
        .await;

    client
        .send_publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(
                PacketIdentifier::new(2).unwrap(),
                false,
            ),
            retain: false,
            topic_name: "topic/A".into(),
            payload: Bytes::from("qos 1"),
        })
        .await;

    client
        .send_publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(
                PacketIdentifier::new(3).unwrap(),
                false,
            ),
            retain: false,
            topic_name: "topic/A".into(),
            payload: Bytes::from("qos 1"),
        })
        .await;

    assert_eq!(
        client.next().await,
        Some(Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(1).unwrap()
        }))
    );

    assert_eq!(
        client.next().await,
        Some(Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(2).unwrap()
        }))
    );

    assert_eq!(
        client.next().await,
        Some(Packet::PubAck(PubAck {
            packet_identifier: PacketIdentifier::new(3).unwrap()
        }))
    );
}
