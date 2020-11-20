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
    FutureExt,
};
use tracing::{debug, error, info};

use mqtt_bridge::{settings::BridgeSettings, BridgeController};
use mqtt_broker::{
    auth::Authorizer,
    sidecar::{Sidecar, SidecarShutdownHandle},
    Broker, BrokerBuilder, BrokerHandle, BrokerReady, BrokerSnapshot, FilePersistor,
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
    settings::Settings,
};

use super::{shutdown, Bootstrap};

const DEVICE_ID_ENV: &str = "IOTEDGE_DEVICEID";
const IOTHUB_HOSTNAME_ENV: &str = "IOTEDGE_IOTHUBHOSTNAME";

#[derive(Default)]
pub struct EdgeHubBootstrap {
    broker_ready: BrokerReady,
}

#[async_trait]
impl Bootstrap for EdgeHubBootstrap {
    type Settings = Settings;

    fn load_config<P: AsRef<Path>>(&self, path: P) -> Result<Self::Settings> {
        info!("loading settings from a file {}", path.as_ref().display());
        Ok(Self::Settings::from_file(path)?)
    }

    type Authorizer = LocalAuthorizer<EdgeHubAuthorizer<PolicyAuthorizer>>;

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

        let device_id = env::var(DEVICE_ID_ENV).context(DEVICE_ID_ENV)?;
        let iothub_id = env::var(IOTHUB_HOSTNAME_ENV).context(IOTHUB_HOSTNAME_ENV)?;

        let authorizer = LocalAuthorizer::new(EdgeHubAuthorizer::new(
            PolicyAuthorizer::new(device_id.clone(), self.broker_ready.handle()),
            device_id,
            iothub_id,
            self.broker_ready.handle(),
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
    ) -> Result<BrokerSnapshot> {
        let broker_handle = broker.handle();
        let sidecars = make_sidecars(&broker_handle, &config)?;

        info!("starting server...");
        let server = make_server(config, broker, self.broker_ready).await?;

        let shutdown_signal = shutdown_signal(&server);
        let server = tokio::spawn(server.serve(shutdown_signal));

        info!("starting sidecars...");

        let mut shutdowns = Vec::new();
        let mut sidecar_joins = Vec::new();

        for sidecar in sidecars {
            shutdowns.push(sidecar.shutdown_handle()?);
            sidecar_joins.push(tokio::spawn(sidecar.run()));
        }

        let state = match future::select(server, future::select_all(sidecar_joins)).await {
            // server exited first
            Either::Left((snapshot, sidecars)) => {
                // send shutdown event to each sidecar
                let shutdowns = shutdowns.into_iter().map(SidecarShutdownHandle::shutdown);
                future::join_all(shutdowns).await;

                // awaits for at least one to finish
                let (_res, _stopped, sidecars) = sidecars.await;

                // wait for the rest to exit
                future::join_all(sidecars).await;

                snapshot??
            }
            // one of sidecars exited first
            Either::Right(((res, stopped, sidecars), server)) => {
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

                // signal server
                broker_handle.send(Message::System(SystemEvent::Shutdown))?;
                server.await??
            }
        };

        Ok(state)
    }
}

async fn make_server<Z>(
    config: Settings,
    broker: Broker<Z>,
    broker_ready: BrokerReady,
) -> Result<Server<Z, MakeEdgeHubPacketProcessor<MakeMqttPacketProcessor>>>
where
    Z: Authorizer + Send + 'static,
{
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

        let broker_ready = Some(broker_ready.signal());
        server.with_tls(tls.addr(), identity, authenticator.clone(), broker_ready)?;
    };

    Ok(server)
}

fn make_sidecars(
    broker_handle: &BrokerHandle,
    config: &Settings,
) -> Result<Vec<Box<dyn Sidecar + Send>>> {
    let mut sidecars: Vec<Box<dyn Sidecar + Send>> = Vec::new();

    let system_address = config.listener().system().addr().to_string();
    let device_id = env::var(DEVICE_ID_ENV).context(DEVICE_ID_ENV)?;

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

fn shutdown_signal<Z, P>(server: &Server<Z, P>) -> impl Future<Output = ()> {
    server
        .listeners()
        .iter()
        .find_map(|listener| listener.transport().identity())
        .map_or_else(
            || Either::Left(shutdown::shutdown()),
            |identity| {
                let system_or_cert_expired = future::select(
                    Box::pin(server_certificate_renewal(identity.not_after())),
                    Box::pin(shutdown::shutdown()),
                );
                Either::Right(system_or_cert_expired.map(drop))
            },
        )
}

async fn server_certificate_renewal(renew_at: DateTime<Utc>) {
    let delay = renew_at - Utc::now();
    if delay > Duration::zero() {
        info!(
            "scheduled server certificate renewal timer for {}",
            renew_at
        );
        let delay = delay.to_std().expect("duration must not be negative");
        crate::time::sleep(delay).await;

        info!("restarting the broker to perform certificate renewal");
    } else {
        error!("server certificate expired at {}", renew_at);
    }
}

#[derive(Debug, thiserror::Error)]
pub enum ServerCertificateLoadError {
    #[error("unable to load server certificate from file {0} and private key {1}")]
    File(PathBuf, PathBuf),

    #[error("unable to download certificate from edgelet")]
    Edgelet,
}
