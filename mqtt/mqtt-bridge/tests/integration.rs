use std::{any::Any, convert::Infallible, time::Duration, vec};

use bytes::Bytes;
use matches::assert_matches;
use mqtt3::{
    proto::{ClientId, QoS},
    ReceivedPublication,
};
use mqtt_bridge::{
    settings::{BridgeSettings, Direction, TopicRule},
    BridgeController, BridgeControllerHandle, BridgeControllerUpdate,
};
use mqtt_broker::{
    auth::{Activity, AllowAll, Authorization, Authorizer, Operation},
    sidecar::Sidecar,
    BrokerBuilder, BrokerHandle, ServerCertificate, SystemEvent,
};
use mqtt_broker_tests_util::{
    client::TestClientBuilder,
    server::{start_server, start_server_with_tls, DummyAuthenticator, ServerHandle},
};
use mqtt_util::client_io::{AuthenticationSettings, Credentials};

const PRIVATE_KEY: &str = include_str!("../tests/tls/pkey.pem");
const CERTIFICATE: &str = include_str!("../tests/tls/cert.pem");
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
async fn send_message_upstream() {
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

    let (local_server_handle, upstream_server_handle, _) = setup_brokers(AllowAll, AllowAll);
    let _controller_handle = setup_bridge_controller(
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
    local_client.subscriptions().recv().await;

    let mut upstream_client = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream_client".into()))
        .build();

    upstream_client
        .subscribe("upstream/temp/#", QoS::AtLeastOnce)
        .await;
    // wait to receive subscription ack
    upstream_client.subscriptions().recv().await;

    // Send upstream
    local_client
        .publish_qos1("to/temp/1", "from local", false)
        .await;

    assert_matches!(
        upstream_client.publications().recv().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from local")
    );

    // Send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream", false)
        .await;

    assert_matches!(
        local_client.publications().recv().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from upstream")
    );

    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

#[tokio::test]
async fn bridge_settings_update() {
    let (local_server_handle, upstream_server_handle, _) = setup_brokers(AllowAll, AllowAll);
    let mut controller_handle = setup_bridge_controller(
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
    local_client.subscriptions().recv().await;

    let mut upstream_client = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream_client".into()))
        .build();

    upstream_client
        .subscribe("upstream/temp/#", QoS::AtLeastOnce)
        .await;
    // wait to receive subscription ack
    upstream_client.subscriptions().recv().await;

    // Send upstream
    local_client
        .publish_qos1("to/temp/1", "from local before update", false)
        .await;

    // Send downstream
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

    tokio::time::delay_for(Duration::from_secs(2)).await;

    // Send upstream
    local_client
        .publish_qos1("to/temp/1", "from local after update", false)
        .await;

    // Send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream after update", false)
        .await;

    assert_matches!(
        local_client.publications().recv().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from upstream after update")
    );

    assert_matches!(
        upstream_client.publications().recv().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from local after update")
    );

    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

#[tokio::test]
async fn subscribe_to_upstream_rejected_should_retry() {
    let (local_server_handle, upstream_server_handle, upstream_broker_handle) =
        setup_brokers(AllowAll, DummySubscribeAuthorizer(false));

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
    setup_bridge_controller(
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
    local_client.subscriptions().recv().await;

    // Send downstream
    upstream_client
        .publish_qos1("to/filter/1", "from remote before update", false)
        .await;

    // send update to authorizer
    upstream_broker_handle
        .send(mqtt_broker::Message::System(
            SystemEvent::AuthorizationUpdate(Box::new(true)),
        ))
        .unwrap();

    tokio::time::delay_for(Duration::from_secs(2)).await;

    // Send upstream
    upstream_client
        .publish_qos1("to/filter/1", "from upstream after update", false)
        .await;

    assert_matches!(
        local_client.publications().recv().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from upstream after update")
    );
}

fn setup_brokers<Z, T>(
    local_authorizer: Z,
    upstream_authorizer: T,
) -> (ServerHandle, ServerHandle, BrokerHandle)
where
    Z: Authorizer + Send + 'static,
    T: Authorizer + Send + 'static,
{
    let local_broker = BrokerBuilder::default()
        .with_authorizer(local_authorizer)
        .build();
    let local_server_handle = start_server(local_broker, DummyAuthenticator::with_id("local"));

    let upstream_broker = BrokerBuilder::default()
        .with_authorizer(upstream_authorizer)
        .build();
    let upstream_broker_handle = upstream_broker.handle();
    let identity = ServerCertificate::from_pem_pair(CERTIFICATE, PRIVATE_KEY).unwrap();
    let upstream_server_handle = start_server_with_tls(
        identity,
        upstream_broker,
        DummyAuthenticator::with_id("device_1"),
    );

    (
        local_server_handle,
        upstream_server_handle,
        upstream_broker_handle,
    )
}

async fn setup_bridge_controller(
    local_address: String,
    upstream_address: String,
    subs: Vec<Direction>,
) -> BridgeControllerHandle {
    let credentials = Credentials::PlainText(AuthenticationSettings::new(
        "bridge".into(),
        "pass".into(),
        "bridge".into(),
        Some(CERTIFICATE.into()),
    ));

    let settings = BridgeSettings::from_upstream_details(
        upstream_address,
        credentials,
        subs,
        true,
        Duration::from_secs(5),
    )
    .unwrap();

    let controller = BridgeController::new(local_address, "bridge".into(), settings);
    let controller_handle = controller.handle();
    let controller: Box<dyn Sidecar + Send> = Box::new(controller);

    tokio::spawn(controller.run());

    controller_handle
}
