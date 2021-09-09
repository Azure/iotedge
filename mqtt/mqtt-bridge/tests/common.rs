use std::{num::NonZeroU64, path::Path, time::Duration};

use tokio::task::JoinHandle;

use mqtt_bridge::{
    settings::{
        BridgeSettings, ConnectionSettings, Direction, RingBufferSettings, StorageSettings,
    },
    BridgeController, BridgeControllerHandle, FlushOptions,
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
    storage_dir_override: &Path,
) -> (BridgeControllerHandle, JoinHandle<()>) {
    let credentials = Credentials::PlainText(AuthenticationSettings::new(
        device_id,
        format!("{}/edgehub", device_id),
        "pass",
        Some(CERTIFICATE.into()),
    ));

    let settings = create_bridge_from_upstream_details(
        upstream_address,
        credentials,
        subs,
        false,
        Duration::from_secs(5),
        StorageSettings::RingBuffer(RingBufferSettings::new(
            NonZeroU64::new(33_554_432).expect("33554432"), //32mb
            storage_dir_override.to_path_buf(),
            FlushOptions::AfterEachWrite,
        )),
    );

    let controller = BridgeController::new(local_address, device_id.into(), settings);
    let controller_handle = controller.handle();
    let controller: Box<dyn Sidecar + Send> = Box::new(controller);

    let join = tokio::spawn(controller.run());

    (controller_handle, join)
}

fn create_bridge_from_upstream_details(
    addr: String,
    credentials: Credentials,
    subs: Vec<Direction>,
    clean_session: bool,
    keep_alive: Duration,
    storage_settings: StorageSettings,
) -> BridgeSettings {
    let upstream_connection_settings = ConnectionSettings::new(
        "$upstream",
        addr,
        credentials,
        subs,
        keep_alive,
        clean_session,
    );
    BridgeSettings::new(
        Some(upstream_connection_settings),
        Vec::new(),
        storage_settings,
    )
}
