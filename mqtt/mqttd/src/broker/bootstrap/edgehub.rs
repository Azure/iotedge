use std::{env, fs, path::Path};

use anyhow::{bail, Result};
use chrono::{Duration, Utc};

use mqtt_broker::{Broker, BrokerBuilder, BrokerConfig, BrokerSnapshot, Server};
use mqtt_broker_core::auth::Authorizer;
use mqtt_edgehub::{
    auth::{EdgeHubAuthenticator, EdgeHubAuthorizer, LocalAuthenticator, LocalAuthorizer},
    edgelet,
    tls::Identity,
};

pub async fn broker(
    config: &BrokerConfig,
    state: Option<BrokerSnapshot>,
) -> Result<Broker<LocalAuthorizer<EdgeHubAuthorizer>>> {
    let broker = BrokerBuilder::default()
        .with_authorizer(LocalAuthorizer::new(EdgeHubAuthorizer::default()))
        .with_state(state.unwrap_or_default())
        .with_config(config.clone())
        .build();

    Ok(broker)
}

pub async fn server<Z>(config: &BrokerConfig, broker: Broker<Z>) -> Result<Server<Z>>
where
    Z: Authorizer + Send + 'static,
{
    // TODO read from config
    let url = "http://localhost:7120/authenticate/".into();
    let authenticator = EdgeHubAuthenticator::new(url);

    let mut server = Server::from_broker(broker);

    if let Some(tcp) = config.transports().tcp() {
        server.tcp(tcp.addr(), authenticator.clone());
    }

    if let Some(tls) = config.transports().tls() {
        dowload_server_certificate(tls.cert_path()).await?;
        server.tls(tls.addr(), tls.cert_path(), authenticator.clone())?;
    }

    // TODO read from config
    server.tcp("localhost:1882", LocalAuthenticator::new());

    Ok(server)
}

pub const WORKLOAD_URI: &str = "IOTEDGE_WORKLOADURI";
pub const EDGE_DEVICE_HOST_NAME: &str = "EDGEDEVICEHOSTNAME";
pub const MODULE_ID: &str = "IOTEDGE_MODULEID";
pub const MODULE_GENERATION_ID: &str = "IOTEDGE_MODULEGENERATIONID";

pub const CERTIFICATE_VALIDITY_DAYS: i64 = 90;

async fn dowload_server_certificate(path: impl AsRef<Path>) -> Result<()> {
    let uri = env::var(WORKLOAD_URI)?;
    let hostname = env::var(EDGE_DEVICE_HOST_NAME)?;
    let module_id = env::var(MODULE_ID)?;
    let generation_id = env::var(MODULE_GENERATION_ID)?;
    let expiration = Utc::now() + Duration::days(CERTIFICATE_VALIDITY_DAYS);

    let client = edgelet::workload(&uri)?;
    let cert = client
        .create_server_cert(&module_id, &generation_id, &hostname, expiration)
        .await?;

    if cert.private_key().type_() != "key" {
        bail!(
            "unknown type of private key: {}",
            cert.private_key().type_()
        );
    }

    if let Some(private_key) = cert.private_key().bytes() {
        let identity = Identity::try_from(cert.certificate(), private_key)?;
        fs::write(path, identity)?;
    } else {
        bail!("missing private key");
    }

    Ok(())
}
