use assert_matches::assert_matches;
use bytes::Bytes;
use futures_util::StreamExt;

use mqtt3::proto::{
    ClientId, ConnectReturnCode, ConnectionRefusedReason, Packet, PacketIdentifier,
    PacketIdentifierDupQoS, Publish, QoS, SubAckQos, Subscribe, SubscribeTo,
};
use mqtt_broker::BrokerBuilder;
use mqtt_broker_tests_util::{
    client::TestClientBuilder,
    packet_stream::PacketStream,
    server::{start_server, DummyAuthenticator},
};
use mqtt_edgehub::{
    auth::{PolicyAuthorizer, PolicyUpdate},
    command::{PolicyUpdateCommand, POLICY_UPDATE_TOPIC},
};

mod common;
use common::DummyAuthorizer;

/// Scenario:
/// create broker
/// create command handler
/// connect a client
/// verify client can't connect, since policy haven't been sent.
#[tokio::test]
async fn connect_not_allowed_policy_not_set() {
    // Start broker with DummyAuthorizer that allows everything from CommandHandler and $edgeHub,
    // but otherwise passes authorization along to PolicyAuthorizer
    let broker = BrokerBuilder::default()
        .with_authorizer(DummyAuthorizer::new(
            PolicyAuthorizer::without_ready_handle("this_edgehub_id".to_string()),
        ))
        .build();
    let server_handle = start_server(
        broker,
        DummyAuthenticator::with_id("myhub.azure-devices.net/device-1"),
    );

    let mut device_client = PacketStream::connect(
        ClientId::IdWithCleanSession("device-1".into()),
        server_handle.address(),
        None,
        None,
        None,
    )
    .await;

    // Verify client cannot connect because policy is not set.
    assert_matches!(
        device_client.next().await,
        Some(Packet::ConnAck(ack)) if ack.return_code == ConnectReturnCode::Refused(ConnectionRefusedReason::ServerUnavailable)
    );
}

/// Scenario:
/// create broker
/// create command handler
/// publish policy update from edgehub
/// connect authorized client
/// verify client can connect, subscribe and publish
#[tokio::test]
async fn auth_policy_happy_case() {
    // Start broker with DummyAuthorizer that allows everything from CommandHandler and $edgeHub,
    // but otherwise passes authorization along to PolicyAuthorizer
    let mut authorizer = DummyAuthorizer::new(PolicyAuthorizer::without_ready_handle(
        "this_edgehub_id".to_string(),
    ));
    let mut policy_ready = authorizer.update_signal();
    let broker = BrokerBuilder::default().with_authorizer(authorizer).build();
    let broker_handle = broker.handle();

    let server_handle = start_server(
        broker,
        DummyAuthenticator::with_id("myhub.azure-devices.net/device-1"),
    );

    // start command handler with PolicyUpdateCommand
    let command = PolicyUpdateCommand::new(&broker_handle);
    let (command_handler_shutdown_handle, join_handle) =
        common::start_command_handler(server_handle.address(), command)
            .await
            .expect("could not start command handler");

    let mut edgehub_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("$edgehub".into()))
        .build();

    let policy = PolicyUpdate::new(
        r###"{
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "myhub.azure-devices.net/device-1"
                    ],
                    "operations": [
                        "mqtt:connect",
                        "mqtt:publish",
                        "mqtt:subscribe"
                    ],
                    "resources": [
                        "#"
                    ]
                }
            ]
        }"###,
    );

    // EdgeHub sends authorization policy to the broker
    edgehub_client
        .publish_qos1(
            POLICY_UPDATE_TOPIC,
            serde_json::to_string(&policy).expect("unable to serialize policy"),
            true,
        )
        .await;

    // let policy update sink in...
    policy_ready.recv().await;

    let mut device_client = PacketStream::connect(
        ClientId::IdWithCleanSession("device-1".into()),
        server_handle.address(),
        None,
        None,
        None,
    )
    .await;

    // assert connack
    assert_matches!(
        device_client.next().await,
        Some(Packet::ConnAck(ack)) if ack.return_code == ConnectReturnCode::Accepted
    );

    // client subscribes to a topic
    device_client
        .send_subscribe(Subscribe {
            packet_identifier: PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![SubscribeTo {
                topic_filter: "custom/topic".into(),
                qos: QoS::AtLeastOnce,
            }],
        })
        .await;

    // assert suback
    assert_matches!(
        device_client.next().await,
        Some(Packet::SubAck(ack)) if ack.qos[0] == SubAckQos::Success(QoS::AtLeastOnce)
    );

    // client publishes to a topic
    device_client
        .send_publish(Publish {
            packet_identifier_dup_qos: PacketIdentifierDupQoS::AtLeastOnce(
                PacketIdentifier::new(1).unwrap(),
                false,
            ),
            retain: false,
            topic_name: "custom/topic".into(),
            payload: Bytes::from("qos 1"),
        })
        .await;

    // assert puback
    assert_matches!(device_client.next().await, Some(Packet::PubAck(_)));

    command_handler_shutdown_handle
        .shutdown()
        .await
        .expect("failed to stop command handler client");

    join_handle.await.unwrap();

    edgehub_client.shutdown().await;
}

/// Scenario:
/// create broker
/// create command handler
/// publish policy update from edgehub
/// connect authorized client
/// publish policy update with client access removed
/// verify client has disconnected
#[tokio::test]
async fn policy_update_reevaluates_sessions() {
    mqtt_broker_tests_util::init_logging();
    // Start broker with DummyAuthorizer that allows everything from CommandHandler and $edgeHub,
    // but otherwise passes authorization along to PolicyAuthorizer
    let mut authorizer = DummyAuthorizer::new(PolicyAuthorizer::without_ready_handle(
        "this_edgehub_id".to_string(),
    ));
    let mut policy_update_signal = authorizer.update_signal();
    let broker = BrokerBuilder::default().with_authorizer(authorizer).build();
    let broker_handle = broker.handle();

    let server_handle = start_server(
        broker,
        DummyAuthenticator::with_id("myhub.azure-devices.net/device-1"),
    );

    // start command handler with PolicyUpdateCommand
    let command = PolicyUpdateCommand::new(&broker_handle);
    let (command_handler_shutdown_handle, join_handle) =
        common::start_command_handler(server_handle.address(), command)
            .await
            .expect("could not start command handler");

    let mut edgehub_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("$edgehub".into()))
        .build();

    // EdgeHub sends authorization policy to the broker
    let policy = PolicyUpdate::new(
        r###"{
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "myhub.azure-devices.net/device-1"
                    ],
                    "operations": [
                        "mqtt:connect"
                    ]
                }
            ]
        }"###,
    );

    edgehub_client
        .publish_qos1(
            POLICY_UPDATE_TOPIC,
            serde_json::to_string(&policy).expect("unable to serialize policy"),
            true,
        )
        .await;

    // let policy update sink in...
    policy_update_signal.recv().await;

    let mut device_client = PacketStream::connect(
        ClientId::IdWithCleanSession("device-1".into()),
        server_handle.address(),
        None,
        None,
        None,
    )
    .await;

    // assert connack
    assert_matches!(
        device_client.next().await,
        Some(Packet::ConnAck(ack)) if ack.return_code == ConnectReturnCode::Accepted
    );

    // EdgeHub sends updated authorization policy to the broker
    // where client no longer allowed to connect
    let policy = PolicyUpdate::new(
        r###"{
            "statements": [
                {
                    "effect": "deny",
                    "identities": [
                        "myhub.azure-devices.net/device-1"
                    ],
                    "operations": [
                        "mqtt:connect"
                    ]
                }
            ]
        }"###,
    );

    edgehub_client
        .publish_qos1(
            POLICY_UPDATE_TOPIC,
            serde_json::to_string(&policy).expect("unable to serialize policy"),
            true,
        )
        .await;

    // let policy update sink in...
    policy_update_signal.recv().await;

    // assert client disconnected
    assert_matches!(device_client.next().await, None);

    command_handler_shutdown_handle
        .shutdown()
        .await
        .expect("failed to stop command handler client");

    join_handle.await.unwrap();

    edgehub_client.shutdown().await;
}
