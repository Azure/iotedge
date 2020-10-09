use std::{
    env,
    future::Future,
    path::{Path, PathBuf},
};

use anyhow::{bail, Context, Result};
use chrono::{DateTime, Duration, Utc};
use futures_util::{
    future::{self, Either},
    pin_mut, FutureExt,
};
use thiserror::Error;
use tokio::{sync::broadcast, time};
use tracing::{error, info, warn};

use super::SidecarManager;
use mqtt_bridge::settings::BridgeSettings;
use mqtt_bridge::BridgeController;
use mqtt_broker::{
    auth::{AllowAll, Authorizer},
    Broker, BrokerBuilder, BrokerConfig, BrokerHandle, BrokerSnapshot, Server, ServerCertificate,
};
use mqtt_edgehub::{
    auth::{EdgeHubAuthenticator, EdgeHubAuthorizer, LocalAuthenticator, LocalAuthorizer},
    command::{
        AuthorizedIdentitiesCommand, CommandHandler, CommandHandlerError, DisconnectCommand,
        PolicyUpdateCommand, ShutdownHandle,
    },
    connection::MakeEdgeHubPacketProcessor,
    settings::{ListenerConfig, Settings},
};

const DEVICE_ID_ENV: &str = "IOTEDGE_DEVICEID";

pub fn config<P>(config_path: Option<P>) -> Result<Settings>
where
    P: AsRef<Path>,
{
    let config = if let Some(path) = config_path {
        info!("loading settings from a file {}", path.as_ref().display());
        Settings::from_file(path)?
    } else {
        info!("using default settings");
        Settings::new()?
    };

    Ok(config)
}

pub async fn broker(
    config: &BrokerConfig,
    state: Option<BrokerSnapshot>,
) -> Result<Broker<impl Authorizer>> {
    // TODO: Use AllowAll as bottom level authorizer until Policies are sent over from EdgeHub
    let authorizer = LocalAuthorizer::new(EdgeHubAuthorizer::new(AllowAll));

    let broker = BrokerBuilder::default()
        .with_authorizer(authorizer)
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
    F: Future<Output = ()>,
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
    let (broker_ready, _) = broadcast::channel(1);

    if let Some(tcp) = config.listener().tcp() {
        let broker_ready = Some(broker_ready.subscribe());
        server.with_tcp(tcp.addr(), authenticator.clone(), broker_ready)?;
    }

    // Add regular MQTT over TLS transport
    let renewal_signal = match config.listener().tls() {
        Some(tls) => {
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
            let renew_at = identity.not_after();

            let broker_ready = Some(broker_ready.subscribe());
            server.with_tls(tls.addr(), identity, authenticator.clone(), broker_ready)?;

            let renewal_signal = server_certificate_renewal(renew_at);
            Either::Left(renewal_signal)
        }
        None => Either::Right(future::pending()),
    };

    // Prepare shutdown signal which is either SYSTEM shutdown signal or cert renewal timout
    pin_mut!(shutdown_signal);
    pin_mut!(renewal_signal);
    let shutdown = future::select(shutdown_signal, renewal_signal).map(drop);

    // TODO remove this call when broker readiness is implemented
    tokio::spawn(async move {
        tokio::time::delay_for(std::time::Duration::from_millis(500)).await;
        broker_ready.send(()).expect("ready signal");
    });

    // Start serving new connections
    let state = server.serve(shutdown).await?;

    Ok(state)
}

#[derive(Debug, Error)]
pub enum SidecarError {
    #[error("Failed to shutdown command handler")]
    CommandHandlerShutdown(#[from] CommandHandlerError),
}

#[derive(Clone, Debug)]
pub struct SidecarShutdownHandle {
    command_handler_shutdown: ShutdownHandle,
}

impl SidecarShutdownHandle {
    pub fn new(command_handler_shutdown: ShutdownHandle) -> Self {
        Self {
            command_handler_shutdown,
        }
    }

    pub async fn shutdown(self) -> Result<(), SidecarError> {
        self.command_handler_shutdown
            .shutdown()
            .await
            .map_err(SidecarError::CommandHandlerShutdown)
    }
}

pub async fn start_sidecars(
    broker_handle: BrokerHandle,
    listener_settings: ListenerConfig,
) -> Result<Option<SidecarManager>> {
    info!("starting sidecars...");

    let system_address = listener_settings.system().addr().to_string();

    // command handler
    let device_id = env::var(DEVICE_ID_ENV)?;
    let mut command_handler = CommandHandler::new(system_address.clone(), &device_id);
    command_handler.add_command(DisconnectCommand::new(&broker_handle));
    command_handler.add_command(AuthorizedIdentitiesCommand::new(&broker_handle));
    command_handler.add_command(PolicyUpdateCommand::new(&broker_handle));
    command_handler.init().await?;
    let command_handler_shutdown = command_handler.shutdown_handle()?;
    let command_handler_join_handle = tokio::spawn(command_handler.run());

    // bridge
    let settings = BridgeSettings::new()?;
    let bridge_controller_join_handle =
        tokio::spawn(BridgeController::run(system_address, device_id, settings));

    let join_handles = vec![command_handler_join_handle, bridge_controller_join_handle];
    let shutdown_handle = SidecarShutdownHandle::new(command_handler_shutdown);

    Ok(Some(SidecarManager::new(join_handles, shutdown_handle)))
}

async fn server_certificate_renewal(renew_at: DateTime<Utc>) {
    let delay = renew_at - Utc::now();
    if delay > Duration::zero() {
        info!(
            "scheduled server certificate renewal timer for {}",
            renew_at
        );
        let delay = delay.to_std().expect("duration must not be negative");
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

pub const WORKLOAD_URI: &str = "IOTEDGE_WORKLOADURI";
pub const EDGE_DEVICE_HOST_NAME: &str = "EdgeDeviceHostName";
pub const MODULE_ID: &str = "IOTEDGE_MODULEID";
pub const MODULE_GENERATION_ID: &str = "IOTEDGE_MODULEGENERATIONID";

pub const CERTIFICATE_VALIDITY_DAYS: i64 = 90;

async fn download_server_certificate() -> Result<ServerCertificate> {
    let uri = env::var(WORKLOAD_URI).context(WORKLOAD_URI)?;
    let hostname = env::var(EDGE_DEVICE_HOST_NAME).context(EDGE_DEVICE_HOST_NAME)?;
    let module_id = env::var(MODULE_ID).context(MODULE_GENERATION_ID)?;
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

#[cfg(test)]
mod tests {
    use std::{
        env,
        time::{Duration as StdDuration, Instant},
    };

    use chrono::{Duration, Utc};
    use mockito::mock;
    use serde_json::json;

    use super::{
        download_server_certificate, server_certificate_renewal, EDGE_DEVICE_HOST_NAME,
        MODULE_GENERATION_ID, MODULE_ID, WORKLOAD_URI,
    };

    const PRIVATE_KEY: &str = include_str!("../../../../mqtt-broker/test/tls/pkey.pem");

    const CERTIFICATE: &str = include_str!("../../../../mqtt-broker/test/tls/cert.pem");

    #[tokio::test]
    async fn it_downloads_server_cert() {
        let expiration = Utc::now() + Duration::days(90);
        let res = json!(
            {
                "privateKey": { "type": "key", "bytes": PRIVATE_KEY },
                "certificate": CERTIFICATE,
                "expiration": expiration.to_rfc3339()
            }
        );

        let _m = mock(
            "POST",
            "/modules/$edgeHub/genid/12345678/certificate/server?api-version=2019-01-30",
        )
        .with_status(201)
        .with_body(serde_json::to_string(&res).unwrap())
        .create();

        env::set_var(WORKLOAD_URI, mockito::server_url());
        env::set_var(EDGE_DEVICE_HOST_NAME, "localhost");
        env::set_var(MODULE_ID, "$edgeHub");
        env::set_var(MODULE_GENERATION_ID, "12345678");

        let res = download_server_certificate().await;
        assert!(res.is_ok());
    }

    #[tokio::test]
    async fn it_schedules_cert_renewal_in_future() {
        let now = Instant::now();

        let renew_at = Utc::now() + Duration::milliseconds(100);
        server_certificate_renewal(renew_at).await;

        let elapsed = now.elapsed();
        assert!(elapsed > StdDuration::from_millis(100));
        assert!(elapsed < StdDuration::from_millis(500));
    }

    #[tokio::test]
    async fn it_does_not_schedule_cert_renewal_in_past() {
        let now = Instant::now();

        let renew_at = Utc::now();
        server_certificate_renewal(renew_at).await;

        assert!(now.elapsed() < StdDuration::from_millis(100));
    }
}
