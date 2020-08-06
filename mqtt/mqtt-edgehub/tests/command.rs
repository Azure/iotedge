use std::time::Duration;

use bytes::Bytes;
use futures_util::StreamExt;
use matches::assert_matches;

use mqtt3::{
    proto::{
        ClientId, ConnAck, Connect, ConnectReturnCode, ConnectionRefusedReason, Disconnect, Packet,
        PacketIdentifier, PacketIdentifierDupQoS, PingReq, PubAck, Publication, Publish, QoS,
        SubAck, SubAckQos, Subscribe, SubscribeTo,
    },
    Event, ReceivedPublication, ShutdownError, PROTOCOL_LEVEL, PROTOCOL_NAME,
};
use mqtt_broker::{auth::AllowAll, BrokerBuilder, BrokerHandle};
use mqtt_broker_tests_util::{start_server, DummyAuthenticator, PacketStream, TestClientBuilder};

use mqtt_edgehub::command::{CommandHandler, ShutdownHandle as CommandShutdownHandle};
use tokio::task::JoinHandle;

// TODO: create integration tests with PacketStream rather than mqtt3

// TEST 1: DISCONNECTION
// connect client
// create command handler
// verify client connected
// publish message to disconnect client
// verify client disconnected

// TEST 2: DISCONNECTION / RECONNECTION / DISCONNECTION
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
async fn command_handler_client_disconnection() {
    let topic = "$edgehub/test-client/disconnect";

    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let (mut command_handler_shutdown_handle, join_handle) = start_command_handler(broker.handle())
        .await
        .expect("could not start command handler");

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let mut test_client = PacketStream::connect(
        ClientId::IdWithCleanSession("test-client".into()),
        server_handle.address(),
        None,
        None,
    )
    .await;
    test_client.next().await; // skip connack

    // TODO REVIEW: use edgehub client id?
    let mut edgehub_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("$edgehub".into()))
        .build();

    edgehub_client.publish_qos1(topic, "qos 1", false).await;

    assert_eq!(
        test_client.next().await,
        Some(Packet::Disconnect(Disconnect {}))
    );

    command_handler_shutdown_handle
        .shutdown()
        .await
        .expect("failed to stop command handler client");
    join_handle
        .await
        .expect("failed to shutdown command handler");

    edgehub_client.shutdown().await;
}

async fn start_command_handler(
    broker_handle: BrokerHandle,
) -> Result<(CommandShutdownHandle, JoinHandle<()>), ShutdownError> {
    let command_handler = CommandHandler::new(broker_handle);
    let shutdown_handle = command_handler.shutdown_handle()?;

    let join_handle = tokio::spawn(command_handler.run());

    Ok((shutdown_handle, join_handle))
}
