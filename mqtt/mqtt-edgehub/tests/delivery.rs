use futures_util::StreamExt;

use mqtt3::{proto::ClientId, proto::QoS, ReceivedPublication};
use mqtt_broker::{auth::AllowAll, BrokerBuilder, MakeMqttPacketProcessor, Server};
use mqtt_broker_tests_util::{
    client::TestClientBuilder,
    server::{self, DummyAuthenticator},
};
use mqtt_edgehub::connection::MakeEdgeHubPacketProcessor;

/// Scenario:
/// create broker
/// create command handler
/// connect client
/// publish message to disconnect client
/// verify client disconnected
#[tokio::test]
async fn it_sends_delivery_confirmation_for_m2m_messages() {
    let broker = BrokerBuilder::default().with_authorizer(AllowAll).build();

    let make_server = |addr| {
        let broker_handle = broker.handle();

        let mut server = Server::from_broker(broker).with_packet_processor(
            MakeEdgeHubPacketProcessor::new(broker_handle, MakeMqttPacketProcessor),
        );

        let authenticator = DummyAuthenticator::anonymous();
        server.with_tcp(&addr, authenticator, None).unwrap();

        server
    };
    let server_handle = server::run(make_server);

    let mut module = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("device-1/module-1".into()))
        .build();

    // subscribe to module inputs
    let inputs = "devices/device-1/modules/module-1/inputs/telemetry/#";
    module.subscribe(inputs, QoS::AtLeastOnce).await;

    let mut edgehub = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("device-1/$edgehub".into()))
        .build();

    // subscribe to confirmation topic
    let confirmation = "$edgehub/delivered";
    edgehub.subscribe(confirmation, QoS::AtLeastOnce).await;

    // publish a message to module-1
    let inputs = "$edgehub/device-1/module-1/inputs/telemetry/?rid=1";
    edgehub.publish_qos1(inputs, "message", false).await;

    assert_eq!(
        module.publications().next().await,
        Some(ReceivedPublication {
            topic_name: "devices/device-1/modules/module-1/inputs/telemetry/?rid=1".into(),
            dup: false,
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: "message".into()
        })
    );

    assert_eq!(
        edgehub.publications().next().await,
        Some(ReceivedPublication {
            topic_name: confirmation.into(),
            dup: false,
            qos: QoS::AtLeastOnce,
            retain: false,
            payload: "\"$edgehub/device-1/module-1/inputs/telemetry/?rid=1\"".into()
        })
    );

    edgehub.shutdown().await;
    module.shutdown().await;
}
