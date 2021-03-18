use std::{path::PathBuf, time::Duration};

use tokio::task::JoinHandle;

use mqtt_bridge::{
    settings::{BridgeSettings, Direction},
    BridgeController, BridgeControllerHandle,
};
use mqtt_broker::{
    auth::Authorizer, sidecar::Sidecar, BrokerBuilder, BrokerHandle, ServerCertificate,
};
use mqtt_broker_tests_util::server::{
    start_server, start_server_with_tls, DummyAuthenticator, ServerHandle,
};
use mqtt_util::{AuthenticationSettings, Credentials};

const PRIVATE_KEY: &str = include_str!("../tests/tls/pkey.pem");
const CERTIFICATE: &str = include_str!("../tests/tls/cert.pem");

pub fn setup_brokers<Z, T>(
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

pub fn setup_upstream_broker<Z>(
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

pub fn setup_local_broker<Z>(authorizer: Z) -> (ServerHandle, BrokerHandle)
where
    Z: Authorizer + Send + 'static,
{
    let local_broker = BrokerBuilder::default().with_authorizer(authorizer).build();
    let local_broker_handle = local_broker.handle();
    let local_server_handle = start_server(local_broker, DummyAuthenticator::with_id("local"));

    (local_server_handle, local_broker_handle)
}

pub async fn setup_bridge_controller(
    device_id: &str,
    local_address: String,
    upstream_address: String,
    subs: Vec<Direction>,
    storage_dir_override: &PathBuf,
) -> (BridgeControllerHandle, JoinHandle<()>) {
    let credentials = Credentials::PlainText(AuthenticationSettings::new(
        device_id.into(),
        format!("{}/edgehub", device_id),
        "pass".into(),
        Some(CERTIFICATE.into()),
    ));

    let settings = BridgeSettings::from_upstream_details(
        upstream_address,
        credentials,
        subs,
        false,
        Duration::from_secs(5),
        storage_dir_override,
    )
    .unwrap();

    let controller = BridgeController::new(local_address, device_id.into(), settings);
    let controller_handle = controller.handle();
    let controller: Box<dyn Sidecar + Send> = Box::new(controller);

    let join = tokio::spawn(controller.run());

    (controller_handle, join)
}
