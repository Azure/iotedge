use std::{
    future::Future,
    path::{Path, PathBuf},
};

use anyhow::{Context, Result};
use futures_util::pin_mut;
use tracing::info;

use mqtt_broker::{
    auth::{authenticate_fn_ok, AllowAll, Authorizer},
    settings::BrokerConfig,
    AuthId, Broker, BrokerBuilder, BrokerHandle, BrokerReady, BrokerSnapshot, Error, Server,
    ServerCertificate,sidecar
};
use mqtt_generic::settings::{CertificateConfig, ListenerConfig, Settings};

use super::Bootstrap;

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
    _: &BrokerReady,
) -> Result<Broker<impl Authorizer>, Error> {
    let broker = BrokerBuilder::default()
        .with_authorizer(AllowAll)
        .with_state(state.unwrap_or_default())
        .with_config(config.clone())
        .build();

    Ok(broker)
}

pub async fn start_server<Z, F>(
    config: Settings,
    broker: Broker<Z>,
    shutdown_signal: F,
    _: BrokerReady,
) -> Result<BrokerSnapshot>
where
    Z: Authorizer + Send + 'static,
    F: Future<Output = ()>,
{
    info!("starting server...");
    let mut server = Server::from_broker(broker);

    if let Some(tcp) = config.listener().tcp() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        server.with_tcp(tcp.addr(), authenticator, None)?;
    }

    if let Some(tls) = config.listener().tls() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        let identity = load_server_certificate(tls.certificate())?;
        server.with_tls(tls.addr(), identity, authenticator, None)?;
    }

    pin_mut!(shutdown_signal);
    let state = server.serve(shutdown_signal).await?;
    Ok(state)
}

pub async fn add_sidecars(
    bootstrap: &mut Bootstrap,
    _: BrokerHandle,
    _: ListenerConfig,
)-> Result<()>{
    bootstrap.add_sidecar(sidecar::pending());
    
    Ok(())
}

fn load_server_certificate(config: &CertificateConfig) -> Result<ServerCertificate> {
    let identity = ServerCertificate::from_pem(config.cert_path(), config.private_key_path())
        .with_context(|| {
            ServerCertificateLoadError::ParseCertificate(
                config.cert_path().to_path_buf(),
                config.private_key_path().to_path_buf(),
            )
        })?;

    Ok(identity)
}

#[derive(Debug, thiserror::Error)]
pub enum ServerCertificateLoadError {
    #[error("unable to decode server certificate {0} and private key {1}")]
    ParseCertificate(PathBuf, PathBuf),
}
