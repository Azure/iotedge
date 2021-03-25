mod common;

use std::time::Duration;

use bson::{doc, spec::BinarySubtype};
use bytes::Bytes;
use futures_util::StreamExt;
use matches::assert_matches;
use tokio::time;

use mqtt3::{
    proto::{ClientId, QoS},
    ReceivedPublication,
};
use mqtt_broker::auth::AllowAll;
use mqtt_broker_tests_util::client::TestClientBuilder;

#[tokio::test]
async fn get_twin_update_via_rpc() {
    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        common::setup_brokers(AllowAll, AllowAll);

    let dir = tempfile::tempdir().expect("Failed to create temp dir");
    let storage_dir_override = dir.path().to_path_buf();

    let (controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-1",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        Vec::new(),
        &storage_dir_override,
    )
    .await;

    // wait for bridge controller subscribed to all required topics
    time::delay_for(Duration::from_millis(100)).await;

    // connect to the remote broker to emulate upstream interaction
    let mut upstream = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession(
            "edge-device-1/upstream/$bridge".into(),
        ))
        .build();
    upstream
        .subscribe("$iothub/+/twin/get/#", QoS::AtLeastOnce)
        .await;
    assert!(upstream.subscriptions().next().await.is_some());

    // connect to the local broker with eh-core client
    let mut edgehub = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession(
            "edge-device-1/edgehub/$bridge".into(),
        ))
        .build();

    // edgehub subscribes to any downstream topic command acknowledgement
    edgehub.subscribe("$downstream/#", QoS::AtLeastOnce).await;
    assert!(edgehub.subscriptions().next().await.is_some());

    // edgehub subscribes to twin response
    let payload = command("sub", "$iothub/device-1/twin/res/#", None);
    edgehub
        .publish_qos1("$upstream/rpc/1", payload, false)
        .await;
    assert_matches!(edgehub.publications().next().await, Some(ReceivedPublication {topic_name, ..}) if topic_name == "$downstream/rpc/ack/1");

    // edgehub makes a request to get a twin for the leaf device
    let payload = command(
        "pub",
        "$iothub/device-1/twin/get/?rid=1",
        Some(Vec::default()),
    );
    edgehub
        .publish_qos1("$upstream/rpc/2", payload, false)
        .await;
    assert_matches!(edgehub.publications().next().await, Some(ReceivedPublication {topic_name, ..}) if topic_name == "$downstream/rpc/ack/2");

    // upstream client awaits on twin request and responds with twin message
    assert_matches!(upstream.publications().next().await, Some(ReceivedPublication {topic_name, ..}) if topic_name == "$iothub/device-1/twin/get/?rid=1");

    let twin = "device-1 twin";
    upstream
        .publish_qos1("$iothub/device-1/twin/res/200/?rid=1", twin, false)
        .await;

    // edgehub verifies it received a twin response
    assert_matches!(edgehub.publications().next().await, Some(ReceivedPublication {topic_name, ..}) if topic_name == "$downstream/device-1/twin/res/200/?rid=1");

    // edgehub unsubscribes from twin response
    let payload = command("unsub", "$iothub/device-1/twin/res/#", None);
    edgehub
        .publish_qos1("$upstream/rpc/3", payload, false)
        .await;
    assert_matches!(edgehub.publications().next().await, Some(ReceivedPublication {topic_name, ..}) if topic_name == "$downstream/rpc/ack/3");

    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    edgehub.shutdown().await;
    upstream.shutdown().await;
}

#[tokio::test]
async fn handle_rpc_subscription_duplicates() {
    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        common::setup_brokers(AllowAll, AllowAll);

    let dir = tempfile::tempdir().expect("Failed to create temp dir");
    let storage_dir_override = dir.path().to_path_buf();

    let (controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-2",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        Vec::new(),
        &storage_dir_override,
    )
    .await;

    // wait for bridge controller subscribed to all required topics
    time::delay_for(Duration::from_millis(100)).await;

    // connect to the local broker with eh-core client
    let mut edgehub = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession(
            "edge-device-2/edgehub/$bridge".into(),
        ))
        .build();

    // edgehub subscribes to any downstream topic command acknowledgement
    edgehub.subscribe("$downstream/#", QoS::AtLeastOnce).await;
    assert!(edgehub.subscriptions().next().await.is_some());

    // edgehub subscribes to twin response #1
    let payload = command("sub", "$iothub/device-1/twin/res/#", None);
    edgehub
        .publish_qos1("$upstream/rpc/11", payload, false)
        .await;

    // edgehub subscribes to twin response #2
    let payload = command("sub", "$iothub/device-1/twin/res/#", None);
    edgehub
        .publish_qos1("$upstream/rpc/12", payload, false)
        .await;

    assert_matches!(edgehub.publications().next().await, Some(ReceivedPublication {topic_name, ..}) if topic_name == "$downstream/rpc/ack/11");
    assert_matches!(edgehub.publications().next().await, Some(ReceivedPublication {topic_name, ..}) if topic_name == "$downstream/rpc/ack/12");

    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    edgehub.shutdown().await;
}

fn command(cmd: &str, topic: &str, payload: Option<Vec<u8>>) -> Bytes {
    let mut command = doc! {
        "version": "v1",
        "cmd": cmd,
        "topic": topic
    };
    if let Some(payload) = payload {
        command.insert(
            "payload",
            bson::Binary {
                subtype: BinarySubtype::Generic,
                bytes: payload,
            },
        );
    }

    let mut payload = Vec::new();
    command.to_writer(&mut payload).unwrap();
    payload.into()
}
