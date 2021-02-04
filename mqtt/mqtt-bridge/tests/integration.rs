use std::{any::Any, convert::Infallible, time::Duration, vec};

use bson::{doc, spec::BinarySubtype};
use bytes::Bytes;
use futures_util::StreamExt;
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
        setup_brokers(AllowAll, AllowAll);
    let controller_handle = setup_bridge_controller(
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
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from local")
    );

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from upstream")
    );

    controller_handle.shutdown();
    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

#[tokio::test]
async fn bridge_settings_update() {
    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        setup_brokers(AllowAll, AllowAll);
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
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from upstream after update")
    );

    assert_matches!(
        upstream_client.publications().next().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from local after update")
    );

    controller_handle.shutdown();
    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

#[tokio::test]
async fn subscribe_to_upstream_rejected_should_retry() {
    let (mut local_server_handle, _, mut upstream_server_handle, upstream_broker_handle) =
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
    let controller_handle = setup_bridge_controller(
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
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("from upstream after update")
    );

    controller_handle.shutdown();
    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    upstream_client.shutdown().await;
    local_client.shutdown().await;
}

#[tokio::test]
async fn connect_to_upstream_failure_should_retry() {
    let (mut local_server_handle, _) = setup_local_broker(AllowAll);

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
    let controller_handle = setup_bridge_controller(
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
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("{\"status\":\"Disconnected\"}")
    );

    let (mut upstream_server_handle, _) = setup_upstream_broker(
        AllowAll,
        Some(upstream_tcp_address.clone()),
        Some(upstream_tls_address.clone()),
    );

    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("{\"status\":\"Connected\"}")
    );

    upstream_server_handle.shutdown().await;
    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("{\"status\":\"Disconnected\"}")
    );

    let (mut upstream_server_handle, _) = setup_upstream_broker(
        AllowAll,
        Some(upstream_tcp_address),
        Some(upstream_tls_address),
    );
    assert_matches!(
        local_client.publications().next().await,
        Some(ReceivedPublication{payload, .. }) if payload == Bytes::from("{\"status\":\"Connected\"}")
    );

    controller_handle.shutdown();
    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    local_client.shutdown().await;
}

#[tokio::test]
async fn get_twin_update_via_rpc() {
    let (mut local_server_handle, _, mut upstream_server_handle, _) =
        setup_brokers(AllowAll, AllowAll);
    let controller_handle = setup_bridge_controller(
        local_server_handle.address(),
        upstream_server_handle.tls_address().unwrap(),
        Vec::new(),
    )
    .await;

    // connect to the remote broker to emulate upstream interaction
    let mut upstream = TestClientBuilder::new(upstream_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("upstream".into()))
        .build();
    upstream
        .subscribe("$iothub/+/twin/get/#", QoS::AtLeastOnce)
        .await;
    assert!(upstream.subscriptions().next().await.is_some());

    // connect to the local broker with eh-core client
    let mut edgehub = TestClientBuilder::new(local_server_handle.address())
        .with_client_id(ClientId::IdWithExistingSession("edgehub".into()))
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
    local_server_handle.shutdown().await;
    upstream_server_handle.shutdown().await;
    edgehub.shutdown().await;
    upstream.shutdown().await;
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

fn setup_brokers<Z, T>(
    local_authorizer: Z,
    upstream_authorizer: T,
) -> (ServerHandle, BrokerHandle, ServerHandle, BrokerHandle)
where
    Z: Authorizer + Send + 'static,
    T: Authorizer + Send + 'static,
{
    let (local_server_handle, local_broker_handle) = setup_local_broker(local_authorizer);
    let (upstream_server_handle, upstream_broker_handle) =
        setup_upstream_broker(upstream_authorizer, None, None);

    (
        local_server_handle,
        local_broker_handle,
        upstream_server_handle,
        upstream_broker_handle,
    )
}

fn setup_upstream_broker<Z>(
    authorizer: Z,
    tcp_addr: Option<String>,
    tls_addr: Option<String>,
) -> (ServerHandle, BrokerHandle)
where
    Z: Authorizer + Send + 'static,
{
    let upstream_broker = BrokerBuilder::default().with_authorizer(authorizer).build();
    let upstream_broker_handle = upstream_broker.handle();
    let identity = ServerCertificate::from_pem_pair(CERTIFICATE, PRIVATE_KEY).unwrap();

    let upstream_server_handle = start_server_with_tls(
        identity,
        upstream_broker,
        DummyAuthenticator::with_id("device_1"),
        tcp_addr,
        tls_addr,
    );

    (upstream_server_handle, upstream_broker_handle)
}

fn setup_local_broker<Z>(authorizer: Z) -> (ServerHandle, BrokerHandle)
where
    Z: Authorizer + Send + 'static,
{
    let local_broker = BrokerBuilder::default().with_authorizer(authorizer).build();
    let local_broker_handle = local_broker.handle();
    let local_server_handle = start_server(local_broker, DummyAuthenticator::with_id("local"));

    (local_server_handle, local_broker_handle)
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
