use std::{
    future::Future,
    path::{Path, PathBuf},
};

use anyhow::{Context, Result};
use tracing::info;

use mqtt_broker::{
    auth::{authenticate_fn_ok, AllowAll, Authorizer},
    settings::BrokerConfig,
    AuthId, Broker, BrokerBuilder, BrokerSnapshot, Error, Server, ServerCertificate,
};
use mqtt_generic::settings::{CertificateConfig, Settings};

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
) -> Result<BrokerSnapshot>
where
    Z: Authorizer + Send + 'static,
    F: Future<Output = ()> + Unpin,
{
    let mut server = Server::from_broker(broker);

    // TODO remove after tests

    // let ready = std::sync::Arc::new(tokio::sync::Notify::new());
    let (tx,_rx) = tokio::sync::broadcast::channel(1);
    

    if let Some(tcp) = config.listener().tcp() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        server.tcp(tcp.addr(), authenticator, Some(tx.subscribe()));
    }

    
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        server.tcp("0.0.0.0:1111", authenticator, Some(tx.subscribe()));
    

    if let Some(tls) = config.listener().tls() {
        let authenticator = authenticate_fn_ok(|_| Some(AuthId::Anonymous));
        let identity = load_server_certificate(tls.certificate())?;
        server.tls(tls.addr(), identity, authenticator, None);
    }

    tokio::spawn(async move{
        tokio::time::delay_for(std::time::Duration::from_secs(5)).await;
        tx.send(()).unwrap();
    });

    let state = server.serve(shutdown_signal).await?;
    Ok(state)
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
