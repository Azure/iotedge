use futures_util::StreamExt;

use mqtt3::{
    proto::ClientId, proto::Packet, proto::PacketIdentifier, proto::QoS, proto::Subscribe,
    proto::SubscribeTo,
};
use mqtt_broker::BrokerBuilder;
use mqtt_broker_tests_util::{
    client::TestClientBuilder,
    packet_stream::PacketStream,
    server::{start_server, DummyAuthenticator},
};
use mqtt_edgehub::{
    auth::EdgeHubAuthorizer, auth::IdentityUpdate, command::AuthorizedIdentitiesCommand,
    command::AUTHORIZED_IDENTITIES_TOPIC,
};

#[macro_use]
extern crate assert_matches;

mod common;
use common::{BottomLevelDummyAuthorizer, DummyAuthorizer};

/// Scenario:
/// create broker
/// create command handler
/// publish authorization update from edgehub
/// connect authorized client and subscribe
/// publish authorization update with client removed
/// verify client has disconnected
#[tokio::test]
async fn disconnect_client_on_auth_update() {
    // Start broker with DummyAuthorizer that allows everything from CommandHandler and $edgeHub,
    // but otherwise passes authorization along to EdgeHubAuthorizer
    let broker = BrokerBuilder::default()
        .with_authorizer(DummyAuthorizer::new(EdgeHubAuthorizer::new(
            BottomLevelDummyAuthorizer {},
        )))
        .build();
    let broker_handle = broker.handle();

    let server_handle = start_server(broker, DummyAuthenticator::id("device-1"));

    // start command handler with AuthorizedIdentitiesCommand
    let command = AuthorizedIdentitiesCommand::new(&broker_handle);
    let (command_handler_shutdown_handle, join_handle) =
        common::start_command_handler(server_handle.address(), command)
            .await
            .expect("could not start command handler");

    let mut edgehub_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("$edgehub".into()))
        .build();

    let service_identity1 =
        IdentityUpdate::new("device-1".into(), Some("device-1;$edgehub".into()));
    let identities = vec![service_identity1];

    // EdgeHub sends authorized identities + auth chains to broker
    edgehub_client
        .publish_qos1(
            AUTHORIZED_IDENTITIES_TOPIC,
            serde_json::to_string(&identities).ok().unwrap(),
            false,
        )
        .await;

    let s = Subscribe {
        packet_identifier: PacketIdentifier::new(1).unwrap(),
        subscribe_to: vec![SubscribeTo {
            topic_filter: "$edgehub/device-1/inputs/telemetry/#".into(), // "devices/device-1/inputs/telemetry/#".into(),
            qos: QoS::AtLeastOnce,
        }],
    };

    let mut device_client = PacketStream::connect(
        ClientId::IdWithCleanSession("device-1".into()),
        server_handle.address(),
        None,
        None,
    )
    .await;
    // assert connack
    assert_matches!(device_client.next().await, Some(Packet::ConnAck(_)));

    device_client.send_subscribe(s).await; // client subscribes to topic

    // assert suback
    assert_matches!(device_client.next().await, Some(Packet::SubAck(_)));

    let identities: Vec<IdentityUpdate> = vec![];

    // EdgeHub sends empty list to signal that no identities are authorized
    edgehub_client
        .publish_qos1(
            AUTHORIZED_IDENTITIES_TOPIC,
            serde_json::to_string(&identities).ok().unwrap(),
            false,
        )
        .await;

    // next() will return None only if the client is disconnected, so this
    // asserts that the subscription has been re-evaluated and disconnected by broker.
    assert_eq!(device_client.next().await, None);

    command_handler_shutdown_handle
        .shutdown()
        .await
        .expect("failed to stop command handler client");

    join_handle.await.unwrap();

    edgehub_client.shutdown().await;
}
