#![allow(dead_code, unused_variables, unused_imports)]
mod bootstrap;
mod shutdown;
mod snapshot;

use std::{fs, path::Path, time::Duration};

use anyhow::{Context, Result};
use async_trait::async_trait;
use futures_util::pin_mut;
use tracing::{error, info};

use mqtt_broker::{
    auth::Authenticator, auth::Authorizer, sidecar::Sidecar, Broker, BrokerHandle, BrokerReady,
    BrokerSnapshot, FilePersistor, MakePacketProcessor, Persist, Server, VersionedFileFormat,
};

use crate::broker::snapshot::start_snapshotter;

use self::bootstrap::BootstrapImpl;

pub async fn run<P>(config_path: Option<P>) -> Result<()>
where
    P: AsRef<Path>,
{
    let settings = bootstrap::config(config_path).context(LoadConfigurationError)?;
    let listener_settings = settings.listener().clone();

    info!("loading state...");
    let persistence_config = settings.broker().persistence();
    let state_dir = persistence_config.file_path();

    fs::create_dir_all(state_dir.clone())?;
    let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
    let state = persistor.load().await?;
    info!("state loaded.");

    let broker_ready = BrokerReady::new();

    let broker = bootstrap::broker(settings.broker(), state, &broker_ready).await?;
    let broker_handle = broker.handle();

    let snapshot_interval = persistence_config.time_interval();
    let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
        start_snapshotter(broker.handle(), persistor, snapshot_interval).await;

    let shutdown_signal = shutdown::shutdown();
    let server = bootstrap::start_server(settings, broker, shutdown_signal, broker_ready);

    let mut bootstrap = BootstrapImpl::new();
    bootstrap::add_sidecars(&mut bootstrap, broker_handle.clone(), listener_settings)?;
    let state = bootstrap.run(broker_handle, server).await?;

    snapshotter_shutdown_handle.shutdown().await?;
    let mut persistor = snapshotter_join_handle.await?;
    info!("state snapshotter shutdown.");

    info!("persisting state before exiting...");
    persistor.store(state).await?;
    info!("state persisted.");

    info!("exiting... goodbye");
    Ok(())
}

#[derive(Debug, thiserror::Error)]
#[error("An error occurred loading configuration.")]
pub struct LoadConfigurationError;

pub struct App<B>
where
    B: Bootstrap,
{
    bootstrap: B,
    settings: B::Settings,
}

impl<B: Bootstrap> App<B> {
    pub fn new(bootstrap: B) -> Self {
        Self {
            bootstrap,
            settings: B::Settings::default(),
        }
    }

    pub fn setup<P>(&mut self, config_path: P) -> Result<()>
    where
        P: AsRef<Path>,
    {
        self.settings = self
            .bootstrap
            .load_config(config_path)
            .context("An error occurred loading configuration.")?;
        Ok(())
    }

    pub async fn run(self) -> Result<()> {
        let broker_ready = BrokerReady::new();

        let (broker, persistor) = self
            .bootstrap
            .make_broker(&self.settings, &broker_ready)
            .await?;

        let broker_handle = broker.handle();

        let snapshot_interval = self.bootstrap.snapshot_interval(&self.settings);
        let (mut snapshotter_shutdown_handle, snapshotter_join_handle) =
            start_snapshotter(broker.handle(), persistor, snapshot_interval).await;

        // let server = self
        //     .bootstrap
        //     .make_server(self.settings, broker, broker_ready)
        //     .await?;

        let state = self
            .bootstrap
            .run(self.settings, broker, broker_ready)
            .await?;

        snapshotter_shutdown_handle.shutdown().await?;
        let mut persistor = snapshotter_join_handle.await?;
        info!("state snapshotter shutdown.");

        info!("persisting state before exiting...");
        persistor.store(state).await?;
        info!("state persisted.");

        info!("exiting... goodbye");
        Ok(())
    }
}

#[async_trait]
pub trait Bootstrap {
    type Settings: Default;

    fn load_config<P: AsRef<Path>>(&self, path: P) -> Result<Self::Settings>;

    type Authorizer: Authorizer + Send + 'static;

    async fn make_broker(
        &self,
        settings: &Self::Settings,
        broker_ready: &BrokerReady,
    ) -> Result<(Broker<Self::Authorizer>, FilePersistor<VersionedFileFormat>)>;

    fn snapshot_interval(&self, settings: &Self::Settings) -> std::time::Duration;

    async fn run(
        self,
        config: Self::Settings,
        broker: Broker<Self::Authorizer>,
        broker_ready: BrokerReady,
    ) -> Result<BrokerSnapshot>;
}

pub mod edgehub {
    use std::{
        env, fs,
        future::Future,
        path::{Path, PathBuf},
    };

    use anyhow::{bail, Context, Result};
    use async_trait::async_trait;
    use chrono::{DateTime, Duration, Utc};
    use futures_util::{
        future::{self, Either},
        pin_mut, FutureExt,
    };
    use tokio::time;
    use tracing::{debug, error, info, warn};

    use mqtt_bridge::{settings::BridgeSettings, BridgeController};
    use mqtt_broker::{
        auth::Authorizer, sidecar::Sidecar, sidecar::SidecarShutdownHandle, Broker, BrokerBuilder,
        BrokerConfig, BrokerHandle, BrokerReady, BrokerSnapshot, FilePersistor,
        MakeMqttPacketProcessor, Message, Persist, Server, ServerCertificate, SystemEvent,
        VersionedFileFormat,
    };
    use mqtt_edgehub::{
        auth::{
            EdgeHubAuthenticator, EdgeHubAuthorizer, LocalAuthenticator, LocalAuthorizer,
            PolicyAuthorizer,
        },
        command::{
            AuthorizedIdentitiesCommand, BridgeUpdateCommand, CommandHandler, DisconnectCommand,
            PolicyUpdateCommand,
        },
        connection::MakeEdgeHubPacketProcessor,
        settings::{ListenerConfig, Settings},
    };

    use super::{shutdown, Bootstrap};

    const DEVICE_ID_ENV: &str = "IOTEDGE_DEVICEID";

    pub struct EdgeHubBootstrap;

    #[async_trait]
    impl Bootstrap for EdgeHubBootstrap {
        type Settings = mqtt_edgehub::settings::Settings;

        fn load_config<P: AsRef<Path>>(&self, path: P) -> Result<Self::Settings> {
            info!("loading settings from a file {}", path.as_ref().display());
            Ok(Self::Settings::from_file(path)?)
        }

        type Authorizer = LocalAuthorizer<EdgeHubAuthorizer<PolicyAuthorizer>>;

        async fn make_broker(
            &self,
            settings: &Self::Settings,
            broker_ready: &BrokerReady,
        ) -> Result<(Broker<Self::Authorizer>, FilePersistor<VersionedFileFormat>)> {
            info!("loading state...");
            let persistence_config = settings.broker().persistence();
            let state_dir = persistence_config.file_path();

            fs::create_dir_all(state_dir.clone())?;
            let mut persistor = FilePersistor::new(state_dir, VersionedFileFormat::default());
            let state = persistor.load().await?;
            info!("state loaded.");

            let device_id = env::var(DEVICE_ID_ENV).context(DEVICE_ID_ENV)?;

            let authorizer = LocalAuthorizer::new(EdgeHubAuthorizer::new(
                PolicyAuthorizer::new(device_id, broker_ready.handle()),
                broker_ready.handle(),
            ));

            let broker = BrokerBuilder::default()
                .with_authorizer(authorizer)
                .with_state(state.unwrap_or_default())
                .with_config(settings.broker().clone())
                .build();

            Ok((broker, persistor))
        }

        fn snapshot_interval(&self, settings: &Self::Settings) -> std::time::Duration {
            settings.broker().persistence().time_interval()
        }

        async fn run(
            self,
            config: Self::Settings,
            broker: Broker<Self::Authorizer>,
            broker_ready: BrokerReady,
        ) -> Result<BrokerSnapshot> {
            let mut broker_handle = broker.handle();

            let mut shutdowns = Vec::new();
            let mut sidecars = Vec::new();

            for sidecar in make_sidecars(&broker_handle, &config)? {
                shutdowns.push(sidecar.shutdown_handle()?);
                sidecars.push(tokio::spawn(sidecar.run()));
            }

            let shutdown_signal = shutdown::shutdown();
            pin_mut!(shutdown_signal);

            let server = make_server(config, broker, broker_ready).await?;
            let server = server.serve(shutdown_signal);
            pin_mut!(server);

            let state = match future::select(server, future::select_all(sidecars)).await {
                // server exited first
                Either::Left((snapshot, sidecars)) => {
                    // send shutdown event to each sidecar
                    let shutdowns = shutdowns.into_iter().map(SidecarShutdownHandle::shutdown);
                    future::join_all(shutdowns).await;

                    // awaits for at least one to finish
                    let (_res, _stopped, sidecars) = sidecars.await;

                    // wait for the rest to exit
                    future::join_all(sidecars).await;

                    snapshot?
                }
                // one of sidecars exited first
                Either::Right(((res, stopped, sidecars), server)) => {
                    // signal server
                    broker_handle.send(Message::System(SystemEvent::Shutdown))?;
                    let snapshot = server.await;

                    debug!("a sidecar has stopped. shutting down all sidecars...");
                    if let Err(e) = res {
                        error!(message = "failed waiting for sidecar shutdown", error = %e);
                    }

                    // send shutdown event to each of the rest sidecars
                    shutdowns.remove(stopped);
                    let shutdowns = shutdowns.into_iter().map(SidecarShutdownHandle::shutdown);
                    future::join_all(shutdowns).await;

                    // wait for the rest to exit
                    future::join_all(sidecars).await;

                    snapshot?
                }
            };

            Ok(state)
        }
    }

    async fn make_server<Z>(
        config: mqtt_edgehub::settings::Settings,
        broker: Broker<Z>,
        broker_ready: BrokerReady,
    ) -> Result<Server<Z, MakeEdgeHubPacketProcessor<MakeMqttPacketProcessor>>>
    where
        Z: Authorizer + Send + 'static,
    {
        info!("starting server...");

        let broker_handle = broker.handle();

        let make_processor = MakeEdgeHubPacketProcessor::new_default(broker_handle.clone());
        let mut server = Server::from_broker(broker).with_packet_processor(make_processor);

        // Add system transport to allow communication between edgehub components
        let authenticator = LocalAuthenticator::new();
        server.with_tcp(config.listener().system().addr(), authenticator, None)?;

        // Add regular MQTT over TCP transport
        let authenticator = EdgeHubAuthenticator::new(config.auth().url());

        if let Some(tcp) = config.listener().tcp() {
            let broker_ready = Some(broker_ready.signal());
            server.with_tcp(tcp.addr(), authenticator.clone(), broker_ready)?;
        }

        // Add regular MQTT over TLS transport
        if let Some(tls) = config.listener().tls() {
            let identity = if let Some(config) = tls.certificate() {
                info!("loading identity from {}", config.cert_path().display());
                ServerCertificate::from_pem(config.cert_path(), config.private_key_path())
                    .with_context(|| {
                        ServerCertificateLoadError::File(
                            config.cert_path().to_path_buf(),
                            config.private_key_path().to_path_buf(),
                        )
                    })?
            } else {
                info!("downloading identity from edgelet");
                download_server_certificate()
                    .await
                    .with_context(|| ServerCertificateLoadError::Edgelet)?
            };
        };

        Ok(server)
    }

    fn make_sidecars(
        broker_handle: &BrokerHandle,
        config: &Settings,
    ) -> Result<Vec<Box<dyn Sidecar>>> {
        let mut sidecars: Vec<Box<dyn Sidecar>> = Vec::new();

        let system_address = config.listener().system().addr().to_string();
        let device_id = env::var(DEVICE_ID_ENV)?;

        let settings = BridgeSettings::new()?;
        let bridge_controller =
            BridgeController::new(system_address.clone(), device_id.to_owned(), settings);
        let bridge_controller_handle = bridge_controller.handle();

        sidecars.push(Box::new(bridge_controller));

        let mut command_handler = CommandHandler::new(system_address, &device_id);
        command_handler.add_command(DisconnectCommand::new(&broker_handle));
        command_handler.add_command(AuthorizedIdentitiesCommand::new(&broker_handle));
        command_handler.add_command(PolicyUpdateCommand::new(broker_handle));
        command_handler.add_command(BridgeUpdateCommand::new(bridge_controller_handle));
        sidecars.push(Box::new(command_handler));

        Ok(sidecars)
    }

    pub const WORKLOAD_URI: &str = "IOTEDGE_WORKLOADURI";
    pub const EDGE_DEVICE_HOST_NAME: &str = "EdgeDeviceHostName";
    pub const MODULE_ID: &str = "IOTEDGE_MODULEID";
    pub const MODULE_GENERATION_ID: &str = "IOTEDGE_MODULEGENERATIONID";

    pub const CERTIFICATE_VALIDITY_DAYS: i64 = 90;

    async fn download_server_certificate() -> Result<ServerCertificate> {
        let uri = env::var(WORKLOAD_URI).context(WORKLOAD_URI)?;
        let hostname = env::var(EDGE_DEVICE_HOST_NAME).context(EDGE_DEVICE_HOST_NAME)?;
        let module_id = env::var(MODULE_ID).context(MODULE_ID)?;
        let generation_id = env::var(MODULE_GENERATION_ID).context(MODULE_GENERATION_ID)?;
        let expiration = Utc::now() + Duration::days(CERTIFICATE_VALIDITY_DAYS);

        let client = edgelet_client::workload(&uri)?;
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
            let identity = ServerCertificate::from_pem_pair(cert.certificate(), private_key)?;
            Ok(identity)
        } else {
            bail!("missing private key");
        }
    }

    async fn server_certificate_renewal(renew_at: DateTime<Utc>) {
        let delay = renew_at - Utc::now();
        if delay > Duration::zero() {
            info!(
                "scheduled server certificate renewal timer for {}",
                renew_at
            );
            // let delay = delay.to_std().expect("duration must not be negative");
            let delay = Duration::days(1).to_std().unwrap();
            time::delay_for(delay).await;

            info!("restarting the broker to perform certificate renewal");
        } else {
            warn!("server certificate expired at {}", renew_at);
        }
    }

    #[derive(Debug, thiserror::Error)]
    pub enum ServerCertificateLoadError {
        #[error("unable to load server certificate from file {0} and private key {1}")]
        File(PathBuf, PathBuf),

        #[error("unable to download certificate from edgelet")]
        Edgelet,
    }
}
