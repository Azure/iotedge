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
use mqtt_broker_tests_util::{start_server, DummyAuthenticator, PacketStream, TestClientBuilder};

// TEST 1: BASIC
// connect client
// create command handler
// verify client connected

// TEST 2: DISCONNECTION
// connect client
// create command handler
// verify client connected
// publish message to disconnect client
// verify client disconnected

// TEST 3: RECONNECTION / DISCONNECTION
// connect client
// create command handler
// verify client connected
// publish message to disconnect client
// verify client disconnected
// reconnect client
// verify client still connected (wait?)
// publish message
// verify client disconnects

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
