mod common;

use matches::assert_matches;
use proptest::prelude::*;
use tokio::runtime::Runtime;

use common::{DummyAuthenticator, DummyAuthorizer, TestClient, TestClientBuilder};
use mqtt3::{
    proto::{ClientId, QoS},
    ReceivedPublication,
};
use mqtt_broker::{proptest::arb_clientid, BrokerBuilder};

// https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-mqtt-support#retrieving-a-device-twins-properties
#[tokio::test]
async fn translation_twin_retrieve() {
    let broker = BrokerBuilder::default()
        .with_authorizer(DummyAuthorizer::allow())
        .build();

    let server_handle = common::start_server(broker, DummyAuthenticator::anonymous());

    let mut edge_hub_core = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("edge_hub_core".into()))
        .build();
    let mut device_1 = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("device_1".into()))
        .build();

    // Core subscribes
    edge_hub_core
        .subscribe("$edgehub/+/twin/get/#", QoS::AtLeastOnce)
        .await;

    // device requests twin update
    device_1
        .subscribe("$iothub/twin/res/#", QoS::AtLeastOnce)
        .await;
    device_1
        .publish_qos1("$iothub/twin/GET/?rid=10", "", false)
        .await;

    // Core receives request
    receive_with_topic(&mut edge_hub_core, "$edgehub/device_1/twin/get/?rid=10").await;
    edge_hub_core
        .publish_qos1("$edgehub/device_1/twin/res/200/?rid=10", "", false)
        .await;

    // device receives response
    receive_with_topic(&mut device_1, "$iothub/twin/res/200/?rid=10").await;

    edge_hub_core.shutdown().await;
    device_1.shutdown().await;
}

// https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-mqtt-support#update-device-twins-reported-properties
#[tokio::test]
async fn translation_twin_update() {
    let broker = BrokerBuilder::default()
        .with_authorizer(DummyAuthorizer::allow())
        .build();

    let server_handle = common::start_server(broker, DummyAuthenticator::anonymous());

    let mut edge_hub_core = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("edge_hub_core".into()))
        .build();
    let mut device_1 = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("device_1".into()))
        .build();

    // Core subscribes
    edge_hub_core
        .subscribe("$edgehub/+/twin/reported/#", QoS::AtLeastOnce)
        .await;

    // device pushes twin update
    device_1
        .subscribe("$iothub/twin/res/#", QoS::AtLeastOnce)
        .await;
    device_1
        .publish_qos1("$iothub/twin/PATCH/properties/reported/?rid=20", "", false)
        .await;

    // Core receives request
    receive_with_topic(
        &mut edge_hub_core,
        "$edgehub/device_1/twin/reported/?rid=20",
    )
    .await;
    edge_hub_core
        .publish_qos1("$edgehub/device_1/twin/res/200/?rid=20", "", false)
        .await;

    // device receives response
    receive_with_topic(&mut device_1, "$iothub/twin/res/200/?rid=20").await;

    edge_hub_core.shutdown().await;
    device_1.shutdown().await;
}

// https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-mqtt-support#receiving-desired-properties-update-notifications
#[tokio::test]
async fn translation_twin_receive() {
    let broker = BrokerBuilder::default()
        .with_authorizer(DummyAuthorizer::allow())
        .build();

    let server_handle = common::start_server(broker, DummyAuthenticator::anonymous());

    let mut edge_hub_core = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("edge_hub_core".into()))
        .build();
    let mut device_1 = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("device_1".into()))
        .build();

    // device subscribes to twin update
    device_1
        .subscribe("$iothub/twin/PATCH/properties/desired/#", QoS::AtLeastOnce)
        .await;

    // Core sends update
    edge_hub_core
        .publish_qos1("$edgehub/device_1/twin/desired/?version=30", "", false)
        .await;

    // device receives response
    receive_with_topic(
        &mut device_1,
        "$iothub/twin/PATCH/properties/desired/?version=30",
    )
    .await;

    edge_hub_core.shutdown().await;
    device_1.shutdown().await;
}

// https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-mqtt-support#respond-to-a-direct-method
#[tokio::test]
async fn translation_direct_method_response() {
    let broker = BrokerBuilder::default()
        .with_authorizer(DummyAuthorizer::allow())
        .build();

    let server_handle = common::start_server(broker, DummyAuthenticator::anonymous());

    let mut edge_hub_core = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("edge_hub_core".into()))
        .build();
    let mut device_1 = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("device_1".into()))
        .build();

    // Core subscribes
    edge_hub_core
        .subscribe("$edgehub/+/methods/res/#", QoS::AtLeastOnce)
        .await;

    // device subscribes to direct methods
    device_1
        .subscribe("$iothub/methods/POST/#", QoS::AtLeastOnce)
        .await;

    // Core calls method
    edge_hub_core
        .publish_qos1(
            "$edgehub/device_1/methods/post/my_cool_method/?rid=7",
            "",
            false,
        )
        .await;

    // device receives call and responds
    receive_with_topic(&mut device_1, "$iothub/methods/POST/my_cool_method/?rid=7").await;
    device_1
        .publish_qos1("$iothub/methods/res/200/?rid=7", "", false)
        .await;

    // Core receives response
    receive_with_topic(
        &mut edge_hub_core,
        "$edgehub/device_1/methods/res/200/?rid=7",
    )
    .await;

    edge_hub_core.shutdown().await;
    device_1.shutdown().await;
}

#[tokio::test]
async fn translation_twin_notify() {
    test_twin_with_client_id("device_1").await;
}

#[tokio::test]
#[ignore] // Re-enable when wildcards are handled
async fn translation_twin_notify_with_wildcards() {
    test_twin_with_client_id("device+1#325").await;
}

proptest! {
    #[test]
    fn translate_clientid_proptest(client_id in arb_clientid()) {
        let mut rt = Runtime::new().unwrap();
        rt.block_on(test_twin_with_client_id(client_id.as_str()));
    }
}

async fn test_twin_with_client_id(client_id: &str) {
    let broker = BrokerBuilder::default()
        .with_authorizer(DummyAuthorizer::allow())
        .build();

    let server_handle = common::start_server(broker, DummyAuthenticator::anonymous());

    let mut edge_hub_core = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession("edge_hub_core".into()))
        .build();
    let mut device_1 = TestClientBuilder::new(server_handle.address())
        .with_client_id(ClientId::IdWithCleanSession(client_id.into()))
        .build();

    // Core subscribes
    edge_hub_core
        .subscribe("$edgehub/+/twin/get/#", QoS::AtLeastOnce)
        .await;
    edge_hub_core
        .subscribe(
            format!("$edgehub/{}/subscriptions", client_id),
            QoS::AtLeastOnce,
        )
        .await;
    receive_with_topic_and_payload(
        &mut edge_hub_core,
        &format!("$edgehub/{}/subscriptions", client_id),
        "[]",
    )
    .await;

    // device requests twin update
    device_1
        .subscribe("$iothub/twin/res/#", QoS::AtLeastOnce)
        .await;
    receive_with_topic_and_payload(
        &mut edge_hub_core,
        &format!("$edgehub/{}/subscriptions", client_id),
        format!("[\"$edgehub/{}/twin/res/#\"]", client_id),
    )
    .await;

    device_1
        .publish_qos1("$iothub/twin/GET/?rid=10", "", false)
        .await;

    // Core receives request
    receive_with_topic(
        &mut edge_hub_core,
        &format!("$edgehub/{}/twin/get/?rid=10", client_id),
    )
    .await;
    edge_hub_core
        .publish_qos1(
            format!("$edgehub/{}/twin/res/200/?rid=10", client_id),
            "",
            false,
        )
        .await;

    // device receives response
    receive_with_topic(&mut device_1, "$iothub/twin/res/200/?rid=10").await;

    edge_hub_core.shutdown().await;
    device_1.shutdown().await;
}

async fn receive_with_topic(client: &mut TestClient, topic: &str) {
    assert_matches!(
        client.publications().recv().await,
        Some(ReceivedPublication {
            topic_name,..
        }) if topic_name == topic
    );
}

async fn receive_with_topic_and_payload<B>(
    client: &mut TestClient,
    topic: &str,
    expected_payload: B,
) where
    B: Into<bytes::Bytes>,
{
    let mut xx = client.publications().recv().await.unwrap();
    let exp = expected_payload.into();

    if xx.topic_name == topic
        && exp.len() != xx.payload.len()
        && xx.payload.len() == 2
        && xx.payload[0] == 91
        && xx.payload[1] == 93
    {
        xx = client.publications().recv().await.unwrap();
        assert_ne!(xx.payload.len(), 0);
    }

    assert_eq!(xx.topic_name, topic);
    assert_eq!(xx.payload, exp);
}
