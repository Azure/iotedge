use std::{
    fs,
    future::Future,
    path::{Path, PathBuf},
};

use anyhow::{Context, Result};
use native_tls::Identity;
use tracing::info;

use mqtt_broker::{Broker, BrokerBuilder, BrokerSnapshot, Error, Server};
use mqtt_broker_core::{
    auth::{authenticate_fn_ok, authorize_fn_ok, AuthId, Authorization, Authorizer},
    settings::BrokerConfig,
};
use mqtt_generic::settings::Settings;

pub fn config<P>(config_path: Option<P>) -> Result<Settings>
where
    P: AsRef<Path>,
{
    let config = if let Some(path) = config_path {
        info!("loading settings from a file {}", path.as_ref().display());
        Settings::from_file(path)?
    } else {
        info!("using default settings");
        Settings::default()
    };

    Ok(config)
}

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
    config: &Settings,
    broker: Broker<Z>,
    shutdown_signal: F,
) -> Result<BrokerSnapshot>
where
    Z: Authorizer + Send + 'static,
    F: Future<Output = ()> + Unpin,
{
    let mut server = Server::from_broker(broker);

    if let Some(tcp) = config.listener().tcp() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        server.tcp(tcp.addr(), authenticator);
    }

    if let Some(tls) = config.listener().tls() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        let identity = load_server_certificate(tls.cert_path())?;
        server.tls(tls.addr(), identity, authenticator)?;
    }

    let state = server.serve(shutdown_signal).await?;
    Ok(state)
}

fn load_server_certificate(path: &Path) -> Result<native_tls::Identity> {
    let cert_buffer = fs::read(path)
        .with_context(|| ServerCertificateLoadError::ReadCertificate(path.to_path_buf()))?;

    let identity = Identity::from_pkcs12(&cert_buffer, "")
        .with_context(|| ServerCertificateLoadError::ParseCertificate(path.to_path_buf()))?;
    Ok(identity)
}

#[derive(Debug, thiserror::Error)]
pub enum ServerCertificateLoadError {
    #[error("unable to read server certificate {0}")]
    ReadCertificate(PathBuf),

    #[error("unable to decode server certificate {0}")]
    ParseCertificate(PathBuf),
}
