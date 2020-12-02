use std::{time::Duration, fs, path::{Path, PathBuf}};

use anyhow::{Context, Result};
use async_trait::async_trait;
use futures_util::pin_mut;
use tracing::{error, info};

use mqtt_broker::{
    auth::{AllowAll, Authorizer, authenticate_fn_ok},
    AuthId, Broker, BrokerBuilder, BrokerSnapshot, FilePersistor,
    MakeMqttPacketProcessor, Persist, Server, ServerCertificate, VersionedFileFormat,
};
use mqtt_generic::settings::{CertificateConfig, Settings};

use super::{shutdown, Bootstrap};

#[derive(Default)]
pub struct GenericBootstrap;

#[async_trait]
impl Bootstrap for GenericBootstrap {
    type Settings = Settings;

    fn load_config<P: AsRef<Path>>(&self, path: P) -> Result<Self::Settings> {
        info!("loading settings from a file {}", path.as_ref().display());
        Ok(Self::Settings::from_file(path)?)
    }

    type Authorizer = AllowAll;

    async fn make_broker(
        &self, 
        settings: &Self::Settings,
    ) -> Result<(Broker<Self::Authorizer>, FilePersistor<VersionedFileFormat>)> {
        info!("loading state...");
        let persistence_config = settings.broker().persistence();
        let state_dir = persistence_config.file_path();

        fs::create_dir_all(state_dir.clone())?;
        let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
        let state = persistor.load().await?;
        info!("state loaded.");

        let broker = BrokerBuilder::default()
            .with_authorizer(AllowAll)
            .with_state(state.unwrap_or_default())
            .with_config(settings.broker().clone())
            .build();

        Ok((broker, persistor))
    }

    fn snapshot_interval(&self, settings: &Self::Settings) -> Duration {
        settings.broker().persistence().time_interval()
    }

    fn session_expiration(&self, settings: &Self::Settings) -> Duration {
        settings.broker().session().expiration()
    }

    fn session_cleanup_interval(&self, settings: &Self::Settings) -> Duration {
        settings.broker().session().cleanup_interval()
    }

    async fn run(
        self,
        config: Self::Settings,
        broker: Broker<Self::Authorizer>,
    ) -> Result<BrokerSnapshot> {
        let shutdown_signal = shutdown::shutdown();
        pin_mut!(shutdown_signal);

        info!("starting server...");
        let server = make_server(config, broker).await?;
        let state = server.serve(shutdown_signal).await?;

        Ok(state)
    }
}

async fn make_server<Z>(
    config: Settings,
    broker: Broker<Z>,
) -> Result<Server<Z, MakeMqttPacketProcessor>>
where
    Z: Authorizer + Send + 'static,
{
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

    Ok(server)
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
