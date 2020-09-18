use std::error::Error as StdError;

use futures_util::StreamExt;
use tokio::task::JoinHandle;

use mqtt3::{proto::ClientId, ShutdownError};
use mqtt_broker::{auth::AllowAll, BrokerBuilder};
use mqtt_broker_tests_util::{
    client::TestClientBuilder,
    packet_stream::PacketStream,
    server::{start_server, DummyAuthenticator},
};
use mqtt_edgehub::command::{Command, CommandHandler, Disconnect, ShutdownHandle};

const DISCONNECT_TOPIC: &str = "$edgehub/disconnect";
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

    let command = Disconnect::new(&broker_handle);
    let (command_handler_shutdown_handle, join_handle) =
        start_command_handler(TEST_SERVER_ADDRESS.to_string(), command)
            .await
            .expect("could not start command handler");

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

    let topic = DISCONNECT_TOPIC;
    edgehub_client
        .publish_qos1(topic, r#""test-client""#, false)
        .await;

    assert_eq!(test_client.next().await, None);

    command_handler_shutdown_handle
        .shutdown()
        .await
        .expect("failed to stop command handler client");

    join_handle.await.unwrap();

    edgehub_client.shutdown().await;
}

async fn start_command_handler<C, E>(
    system_address: String,
    command: C,
) -> Result<(ShutdownHandle, JoinHandle<()>), ShutdownError>
where
    C: Command<Error = E> + Send + 'static,
    E: StdError + 'static,
{
    let mut command_handler = CommandHandler::new(system_address, "test-device");
    command_handler.add_command(command);

    command_handler.init().await.unwrap();

    let shutdown_handle: ShutdownHandle = command_handler.shutdown_handle().unwrap();

    let join_handle = tokio::spawn(command_handler.run());

    Ok((shutdown_handle, join_handle))
}
