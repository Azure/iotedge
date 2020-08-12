use futures_util::StreamExt;

use mqtt3::proto::ClientId;
use mqtt_broker::{
    auth::{AllowAll, DenyAll},
    BrokerBuilder, BrokerHandle,
};
use mqtt_broker_tests_util::{start_server, DummyAuthenticator, PacketStream, TestClientBuilder};

use mqtt_edgehub::command::{CommandHandler, ShutdownHandle as CommandShutdownHandle};
use tokio::task::JoinHandle;

use anyhow::Result;
use assert_matches::assert_matches;

/// Scenario:
// create broker
// create command handler
// connect client
// publish message to disconnect client
// verify client disconnected
#[tokio::test]
async fn disconnect_client() {
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

    let mut edgehub_client = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("$edgehub".into()))
        .build();

    let topic = "$edgehub/test-client/disconnect";
    edgehub_client.publish_qos1(topic, "qos 1", false).await;

    assert_eq!(test_client.next().await, None);

    // TODO REVIEW: shutdown broker? Ask Vadim
    command_handler_shutdown_handle
        .shutdown()
        .await
        .expect("failed to stop command handler client");
    join_handle
        .await
        .expect("failed to shutdown command handler");

    edgehub_client.shutdown().await;
}

#[tokio::test]
async fn fail_when_cannot_subscribe() {
    let broker = BrokerBuilder::default().with_authorizer(DenyAll).build();

    let start_output = start_command_handler(broker.handle()).await;

    assert_matches!(start_output, Err(_));
}

async fn start_command_handler(
    broker_handle: BrokerHandle,
) -> Result<(CommandShutdownHandle, JoinHandle<()>)> {
    let command_handler = CommandHandler::new(
        broker_handle,
        "localhost:5555".to_string(),
        "test-device".to_string(),
    )?;
    let shutdown_handle = command_handler.shutdown_handle()?;

    let join_handle = tokio::spawn(command_handler.run());

    Ok((shutdown_handle, join_handle))
}
