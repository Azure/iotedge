use std::{
    fs,
    future::Future,
    path::{Path, PathBuf},
};

use anyhow::{Context, Result};
use native_tls::Identity;

use mqtt_broker::{Broker, BrokerBuilder, BrokerConfig, BrokerSnapshot, Error, Server};
use mqtt_broker_core::auth::{
    authenticate_fn_ok, authorize_fn_ok, AuthId, Authorization, Authorizer,
};

pub async fn broker(
    config: &BrokerConfig,
    state: Option<BrokerSnapshot>,
) -> Result<Broker<impl Authorizer>, Error> {
    let broker = BrokerBuilder::default()
        .with_authorizer(authorize_fn_ok(|_| Authorization::Allowed))
        .with_state(state.unwrap_or_default())
        .with_config(config.clone())
        .build();

    Ok(broker)
}

pub async fn start_server<Z, F>(
    config: &BrokerConfig,
    broker: Broker<Z>,
    shutdown_signal: F,
) -> Result<BrokerSnapshot>
where
    Z: Authorizer + Send + 'static,
    F: Future<Output = ()> + Unpin,
{
    let mut server = Server::from_broker(broker);

    if let Some(tcp) = config.transports().tcp() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        server.tcp(tcp.addr(), authenticator);
    }

    if let Some(tls) = config.transports().tls() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        let identity = load_server_certificate(tls.cert_path())?;
        server.tls(tls.addr(), identity, authenticator)?;
    }

    let state = server.serve(shutdown_signal).await?;
    Ok(state)
}

fn load_server_certificate(path: Option<&Path>) -> Result<native_tls::Identity> {
    let path = path.ok_or_else(|| ServerCertificateLoadError::MissingPath)?;

    let cert_buffer = fs::read(&path)
        .with_context(|| ServerCertificateLoadError::ReadCertificate(path.to_path_buf()))?;

    let identity = Identity::from_pkcs12(&cert_buffer, "")
        .with_context(|| ServerCertificateLoadError::ParseCertificate(path.to_path_buf()))?;
    Ok(identity)
}

#[derive(Debug, thiserror::Error)]
pub enum ServerCertificateLoadError {
    #[error("missing path to server certificate")]
    MissingPath,

    #[error("unable to read server certificate {0}")]
    ReadCertificate(PathBuf),

    #[error("unable to decode server certificate {0}")]
    ParseCertificate(PathBuf),
}
