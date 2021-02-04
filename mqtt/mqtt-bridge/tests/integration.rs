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
    let (controller_handle, controller_task) = common::setup_bridge_controller(
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs,
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

#[tokio::test]
async fn bridge_settings_update() {
    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        common::setup_brokers(AllowAll, AllowAll);
    let (mut controller_handle, controller_task) = common::setup_bridge_controller(
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        vec![],
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
    let (controller_handle, controller_task) = common::setup_bridge_controller(
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs,
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
    let (controller_handle, controller_task) = common::setup_bridge_controller(
        local_server_handle.address(),
        upstream_tls_address.clone(),
        subs,
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
    let (controller_handle, controller_task) = common::setup_bridge_controller(
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs.clone(),
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
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs,
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
    let (mut controller_handle, _) = common::setup_bridge_controller(
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        subs,
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
