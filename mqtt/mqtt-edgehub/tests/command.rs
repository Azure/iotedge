use std::time::Duration;

use futures_util::StreamExt;
use mqtt3::{proto::ClientId, ShutdownError};
use mqtt_broker::{auth::AllowAll, BrokerBuilder, BrokerHandle};
use mqtt_broker_tests_util::{
    client::TestClientBuilder,
    packet_stream::PacketStream,
    server::{start_server, DummyAuthenticator},
};
use mqtt_edgehub::command::{
    CommandHandler, CommandHandlerError, ShutdownHandle as CommandShutdownHandle,
};
use tokio::{task::JoinHandle, time};

const TEST_SERVER_ADDRESS: &str = "localhost:5555";

/// Scenario:
// create broker
// create command handler
// connect client
// publish message to disconnect client
// verify client disconnected
#[tokio::test]
async fn disconnect_client() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();
    let broker_handle = broker.handle();

    let server_handle = start_server(broker, DummyAuthenticator::anonymous());

    let (mut command_handler_shutdown_handle, join_handle) =
        start_command_handler(broker_handle, TEST_SERVER_ADDRESS.to_string())
            .await
            .expect("could not start command handler");

    // TODO: This wait is necessary because edgehub can send disconnect before command handler subscribes to disconnect topic
    //       We can remove once we have a proper approach for starting command handler before edgehub sends disconnects
    //       The risk of this causing issues in a containerized scenario is very small because edgehub startup time > broker startup time
    time::delay_for(Duration::from_secs(1)).await;

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

    let topic = "$edgehub/disconnect";
    edgehub_client
        .publish_qos1(topic, r#""test-client""#, false)
        .await;

    assert_eq!(test_client.next().await, None);

    command_handler_shutdown_handle
        .shutdown()
        .await
        .expect("failed to stop command handler client");
    join_handle
        .await
        .expect("failed to shutdown command handler")
        .expect("command handler failed");

    edgehub_client.shutdown().await;
}

async fn start_command_handler(
    broker_handle: BrokerHandle,
    system_address: String,
) -> Result<
    (
        CommandShutdownHandle,
        JoinHandle<Result<(), CommandHandlerError>>,
    ),
    ShutdownError,
> {
    let device_id = "test-device";
    let command_handler = CommandHandler::new(broker_handle, system_address, device_id);
    let shutdown_handle = command_handler.shutdown_handle()?;

    let join_handle = tokio::spawn(command_handler.run());

    Ok((shutdown_handle, join_handle))
}
