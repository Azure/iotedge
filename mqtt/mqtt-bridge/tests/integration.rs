mod common;

use std::{any::Any, convert::Infallible, time::Duration};

use bytes::Bytes;
use futures_util::StreamExt;
use matches::assert_matches;

use mqtt3::{
    proto::{ClientId, QoS},
    ReceivedPublication,
};
use mqtt_bridge::{
    settings::{Direction, TopicRule},
    BridgeControllerUpdate,
};
use mqtt_broker::{
    auth::{Activity, AllowAll, Authorization, Authorizer, Operation},
    SystemEvent,
};
use mqtt_broker_tests_util::client::TestClientBuilder;

pub struct DummySubscribeAuthorizer(bool);

// Authorizer that rejects all subscriptions by default
// and can be updated to allow all subsscriptions
impl Authorizer for DummySubscribeAuthorizer {
    type Error = Infallible;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        let auth: Authorization = match activity.operation() {
            Operation::Subscribe(_) => {
                if self.0 {
                    Authorization::Allowed
                } else {
                    Authorization::Forbidden("denied".to_string())
                }
            }
            _ => Authorization::Allowed,
        };

        Ok(auth)
    }

    fn update(&mut self, update: Box<dyn Any>) -> Result<(), Self::Error> {
        self.0 = *update.downcast_ref::<bool>().expect("expected bool");
        Ok(())
    }
}

/// Scenario:
///	- Creates 2 brokers and a bridge to connect between the brokers.
///	- A client connects to local broker and subscribes to receive messages from upstream
/// - A client connects to remote broker and subscribes to receive messages from downstream
/// - Clients publish messages
///	- Expects to receive messages downstream -> upstream and upstream -> downstream
#[tokio::test]
async fn send_message_upstream_downstream() {
    let subs = vec![
        Direction::Out(TopicRule::new(
            "temp/#".into(),
            Some("to".into()),
            Some("upstream".into()),
        )),
        Direction::In(TopicRule::new(
            "filter/#".into(),
            Some("to".into()),
            Some("downstream".into()),
        )),
    ];

    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        common::setup_brokers(AllowAll, AllowAll);

    let dir = tempfile::tempdir().expect("Failed to create temp dir");
    let storage_dir_override = dir.path().to_path_buf();

    let (controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-1",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs,
        &storage_dir_override,
    )
    .await;

    let mut local_client = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("local_client".into()))
        .build();
    local_client
        .subscribe("downstream/filter/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    local_client.subscriptions().next().await;

    let mut upstream_client = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream_client".into()))
        .build();

    upstream_client
        .subscribe("upstream/temp/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    upstream_client.subscriptions().next().await;

    // send upstream
    local_client
        .publish_qos1("to/temp/1", "from local", false)
        .await;

    // send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream", false)
        .await;

    assert_matches!(
        upstream_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("from local")
    );

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("from upstream")
    );

    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

/// Scenario:
///	- Creates 2 brokers and a bridge to connect between the brokers.
///	- A client connects to local broker and subscribes to receive messages from upstream
/// - A client connects to remote broker and subscribes to receive messages from downstream
/// - Client publish message upstream
///	- Expects to receive messages downstream -> upstream
/// - Shutdown upstream to simulate disconnect
/// - Client publish message upstream with retain
/// - Disconnect bridge and shutdown (simulate process restart)
/// - Reconnect bridge
/// - Reconnect and resubscribe for upstream
/// - Expects to receive messages downstream -> upstream
#[tokio::test]
async fn send_message_upstream_with_crash_is_lossless() {
    let subs = vec![Direction::Out(TopicRule::new(
        "temp/#".into(),
        Some("to".into()),
        Some("upstream".into()),
    ))];

    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        common::setup_brokers(AllowAll, AllowAll);

    let mut local_client = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("local_client".into()))
        .build();

    let mut upstream_client = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream_client".into()))
        .build();

    let dir = tempfile::tempdir().expect("Failed to create temp dir");
    let storage_dir_override = dir.path().to_path_buf();

    let (controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-2",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs.clone(),
        &storage_dir_override,
    )
    .await;

    local_client
        .subscribe("downstream/filter/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    local_client.subscriptions().next().await;

    upstream_client
        .subscribe("upstream/temp/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    upstream_client.subscriptions().next().await;

    // send upstream
    local_client
        .publish_qos1("to/temp/1", "from local", false)
        .await;

    assert_matches!(
        upstream_client.publications().next().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from local")
    );

    upstream_server_handle.shutdown().await;

    // send upstream (with retain) after shutting down upstream
    local_client
        .publish_qos1("to/temp/1", "from local again", true)
        .await;

    // Simulate restart
    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    let (mut upstream_server_handle, _) = common::setup_upstream_broker(AllowAll, None, None);

    // re-subscribe and reconnect
    upstream_client = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream_client".into()))
        .build();

    let (controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-3",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs.clone(),
        &storage_dir_override,
    )
    .await;

    upstream_client
        .subscribe("upstream/temp/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    upstream_client.subscriptions().next().await;

    assert_matches!(
        upstream_client.publications().next().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from local again")
    );

    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

/// Scenario:
///	- Creates 2 brokers and a bridge to connect between the brokers,
///   but without any subscriptions to downstream and upstream.
///	- A client connects to local broker and subscribes to receive messages from upstream
/// - A client connects to remote broker and subscribes to receive messages from downstream
/// - Clients publish messages
/// - Subscription updates are sent to bridge
///	- Expects to receive only messages after subscription update (downstream -> upstream and upstream -> downstream)
#[tokio::test]
async fn bridge_settings_update() {
    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        common::setup_brokers(AllowAll, AllowAll);

    let dir = tempfile::tempdir().expect("Failed to create temp dir");
    let storage_dir_override = dir.path().to_path_buf();

    let (mut controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-4",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        vec![],
        &storage_dir_override,
    )
    .await;

    let mut local_client = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("local_client".into()))
        .build();

    local_client
        .subscribe("downstream/filter/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    local_client.subscriptions().next().await;

    let mut upstream_client = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream_client".into()))
        .build();

    upstream_client
        .subscribe("upstream/temp/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    upstream_client.subscriptions().next().await;

    // send upstream
    local_client
        .publish_qos1("to/temp/1", "from local before update", false)
        .await;

    // send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream before update", false)
        .await;

    let subs = vec![TopicRule::new(
        "filter/#".into(),
        Some("to".into()),
        Some("downstream".into()),
    )];
    let forwards = vec![TopicRule::new(
        "temp/#".into(),
        Some("to".into()),
        Some("upstream".into()),
    )];

    controller_handle
        .send_update(BridgeControllerUpdate::from_bridge_topic_rules(
            "$upstream",
            subs.as_ref(),
            forwards.as_ref(),
        ))
        .unwrap();

    // delay to propagate the update
    tokio::time::delay_for(Duration::from_secs(2)).await;

    // send upstream
    local_client
        .publish_qos1("to/temp/1", "from local after update", false)
        .await;

    // send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream after update", false)
        .await;

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("from upstream after update")
    );

    assert_matches!(
        upstream_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("from local after update")
    );

    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

/// Scenario:
///	- Creates 2 brokers and a bridge to connect between the brokers.
/// - Remote broker is set to deny the subscription from the bridge
///	- A client connects to local broker and subscribes to receive messages from upstream
/// - A client connects to remote broker and subscribes to receive messages from downstream
/// - Upstream clients publishes a message
/// - Update upstream broker to allow subscription
/// - Upstream clients publishes another message
///	- Expects to receive only message after subscription was allowed
#[tokio::test]
async fn subscribe_to_upstream_rejected_should_retry() {
    let (mut local_server_handle, _, mut upstream_server_handle, upstream_broker_handle) =
        common::setup_brokers(AllowAll, DummySubscribeAuthorizer(false));

    let subs = vec![
        Direction::Out(TopicRule::new(
            "temp/#".into(),
            Some("to".into()),
            Some("upstream".into()),
        )),
        Direction::In(TopicRule::new(
            "filter/#".into(),
            Some("to".into()),
            Some("downstream".into()),
        )),
    ];

    let dir = tempfile::tempdir().expect("Failed to create temp dir");
    let storage_dir_override = dir.path().to_path_buf();

    let (controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-5",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs,
        &storage_dir_override,
    )
    .await;

    let mut local_client = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("local_client".into()))
        .build();

    let mut upstream_client = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream_client".into()))
        .build();

    local_client
        .subscribe("downstream/filter/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    local_client.subscriptions().next().await;

    // send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from remote before update", false)
        .await;

    // send update to authorizer
    upstream_broker_handle
        .send(mqtt_broker::Message::System(
            SystemEvent::AuthorizationUpdate(Box::new(true)),
        ))
        .unwrap();

    // delay to have authorizer updated
    tokio::time::delay_for(Duration::from_secs(2)).await;

    // send upstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream after update", false)
        .await;

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("from upstream after update")
    );

    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

/// Scenario:
///	- Creates local brokers and a bridge to connect between downstream and upstream brokers.
/// - Remote broker is not started yet
/// - Local client subscribes to $internal/connectivity to receive connectivity events
///	- Expects to receive disconnected event
/// - Start upstream broker
/// - Expects to receive connected event
/// - Shutdown upstream broker
/// - Expects to receive disconnect event
/// - Start upstream broker again
///	- Expects to receive connected event
#[tokio::test]
async fn connect_to_upstream_failure_should_retry() {
    let (mut local_server_handle, _) = common::setup_local_broker(AllowAll);

    let subs = vec![
        Direction::Out(TopicRule::new(
            "temp/#".into(),
            Some("to".into()),
            Some("upstream".into()),
        )),
        Direction::In(TopicRule::new(
            "filter/#".into(),
            Some("to".into()),
            Some("downstream".into()),
        )),
    ];
    let upstream_tcp_address = "localhost:8801".to_string();
    let upstream_tls_address = "localhost:8802".to_string();

    let dir = tempfile::tempdir().expect("Failed to create temp dir");
    let storage_dir_override = dir.path().to_path_buf();

    let (controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-6",
        local_server_handle.address(),
        upstream_tls_address.clone(),
        subs,
        &storage_dir_override,
    )
    .await;
    let mut local_client = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("local_client".into()))
        .build();

    local_client
        .subscribe("$internal/connectivity", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    local_client.subscriptions().next().await;

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("{\"status\":\"Disconnected\"}")
    );

    let (mut upstream_server_handle, _) = common::setup_upstream_broker(
        AllowAll,
        Some(upstream_tcp_address.clone()),
        Some(upstream_tls_address.clone()),
    );

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("{\"status\":\"Connected\"}")
    );

    upstream_server_handle.shutdown().await;
    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("{\"status\":\"Disconnected\"}")
    );

    let (mut upstream_server_handle, _) = common::setup_upstream_broker(
        AllowAll,
        Some(upstream_tcp_address),
        Some(upstream_tls_address),
    );
    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("{\"status\":\"Connected\"}")
    );

    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    local_client.shutdown().await;
}

/// Scenario:
///	- Creates downstream broker, upstream broker and a bridge to connect between downstream and upstream brokers.
/// - Create local client and remote client and publish messages
///	- Expects to receive messages downstream -> upstream and upstream -> downstream
/// - Shutdown all bridges and send messages
/// - Start bridge and send messages
/// - Expects to receive only messages after bridge was started
#[tokio::test]
async fn bridge_forwards_messages_after_restart() {
    let subs = vec![
        Direction::Out(TopicRule::new(
            "temp/#".into(),
            Some("to".into()),
            Some("upstream".into()),
        )),
        Direction::In(TopicRule::new(
            "filter/#".into(),
            Some("to".into()),
            Some("downstream".into()),
        )),
    ];

    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        common::setup_brokers(AllowAll, AllowAll);

    let dir = tempfile::tempdir().expect("Failed to create temp dir");
    let storage_dir_override = dir.path().to_path_buf();

    let (controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-7",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs.clone(),
        &storage_dir_override,
    )
    .await;

    // connect to local server and subscribe for downstream topic
    let mut local_client = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("local_client".into()))
        .build();
    local_client
        .subscribe("downstream/filter/#", QoS::AtLeastOnce)
        .await;
    local_client.subscriptions().next().await;

    // connect to upstream server and subscribe for upstream topic
    let mut upstream_client = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream_client".into()))
        .build();
    upstream_client
        .subscribe("upstream/temp/#", QoS::AtLeastOnce)
        .await;
    upstream_client.subscriptions().next().await;

    // send upstream
    local_client
        .publish_qos1("to/temp/1", "from local 1", false)
        .await;

    // send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream 1", false)
        .await;

    assert_matches!(
        upstream_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("from local 1")
    );

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("from upstream 1")
    );

    // shutdown all bridges
    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    // send upstream
    local_client
        .publish_qos1("to/temp/1", "from local 2", false)
        .await;

    // send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream 2", false)
        .await;

    // restart bridge
    let (controller_handle, controller_task) = common::setup_bridge_controller(
        "edge-device-8",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs,
        &storage_dir_override,
    )
    .await;

    // wait until the bridges up and running
    tokio::time::delay_for(Duration::from_secs(1)).await;

    // send upstream
    local_client
        .publish_qos1("to/temp/1", "from local 3", false)
        .await;

    // send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream 3", false)
        .await;

    assert_matches!(
        upstream_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("from local 3")
    );

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication { payload, .. }) if payload == Bytes::from("from upstream 3")
    );

    controller_handle.shutdown();
    controller_task.await.expect("controller task");

    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

/// Scenario:
///	- Creates downstream broker, upstream broker and a bridge to connect between downstream and upstream brokers.
/// - Send shutdown signal to close the $usptream bridge
/// - Create local client and remote client and publish messages
///	- Expects to receive messages downstream -> upstream and upstream -> downstream
///   which means bridge was recreated
#[tokio::test]
async fn recreate_upstream_bridge_when_fails() {
    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        common::setup_brokers(AllowAll, AllowAll);

    let subs = vec![
        Direction::Out(TopicRule::new(
            "temp/#".into(),
            Some("to".into()),
            Some("upstream".into()),
        )),
        Direction::In(TopicRule::new(
            "filter/#".into(),
            Some("to".into()),
            Some("downstream".into()),
        )),
    ];

    let dir = tempfile::tempdir().expect("Failed to create temp dir");
    let storage_dir_override = dir.path().to_path_buf();

    let (mut controller_handle, _) = common::setup_bridge_controller(
        "edge-device-9",
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs,
        &storage_dir_override,
    )
    .await;

    controller_handle.shutdown_bridge("$upstream");
    let mut local_client = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("local_client".into()))
        .build();

    local_client
        .subscribe("downstream/filter/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    local_client.subscriptions().next().await;

    let mut upstream_client = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream_client".into()))
        .build();

    upstream_client
        .subscribe("upstream/temp/#", QoS::AtLeastOnce)
        .await;

    // wait to receive subscription ack
    upstream_client.subscriptions().next().await;

    // send upstream
    local_client
        .publish_qos1("to/temp/1", "from local", false)
        .await;

    // send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream", false)
        .await;

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from upstream")
    );

    assert_matches!(
        upstream_client.publications().next().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from local")
    );

    controller_handle.shutdown();
    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    upstream_client.shutdown().await;
    local_client.shutdown().await;
}
