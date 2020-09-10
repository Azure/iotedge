use mqtt_edgehub::command::Command;
use std::collections::HashMap;
use std::{
    env,
    future::Future,
    path::{Path, PathBuf},
};

use anyhow::{bail, Context, Result};
use chrono::{DateTime, Duration, Utc};
use thiserror::Error;

use futures_util::{
    future::{self, Either},
    pin_mut, FutureExt,
};
use tokio::{
    sync::oneshot::{channel, Sender},
    task::JoinHandle,
    time,
};
use tracing::{error, info, warn};

use mqtt_bridge::BridgeController;
use mqtt_broker::BrokerHandle;
use mqtt_broker::{
    auth::Authorizer, Broker, BrokerBuilder, BrokerConfig, BrokerSnapshot, Server,
    ServerCertificate,
};
use mqtt_edgehub::{
    auth::{EdgeHubAuthenticator, EdgeHubAuthorizer, LocalAuthenticator, LocalAuthorizer},
    command::init_commands,
    command_handler::CommandHandler,
    connection::MakeEdgeHubPacketProcessor,
    settings::Settings,
};

pub struct SidecarShutdownHandle(Sender<()>);

impl SidecarShutdownHandle {
    pub fn shutdown(self) -> Result<(), SidecarError> {
        self.0.send(()).map_err(|_| SidecarError::SidecarShutdown)
    }
}

#[derive(Debug, Error)]
pub enum SidecarError {
    #[error("An error occurred shutting down sidecars")]
    SidecarShutdown,
}

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
) -> Result<Broker<LocalAuthorizer<EdgeHubAuthorizer>>> {
    let broker = BrokerBuilder::default()
        .with_authorizer(LocalAuthorizer::new(EdgeHubAuthorizer::default()))
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
    let broker_handle = broker.handle();

    let mut server =
        Server::from_broker(broker).packet_processor(MakeEdgeHubPacketProcessor::default());

    // Add system transport to allow communication between edgehub components
    let authenticator = LocalAuthenticator::new();
    server.tcp(config.listener().system().addr(), authenticator);

    // Add regular MQTT over TCP transport
    let authenticator = EdgeHubAuthenticator::new(config.auth().url());
    if let Some(tcp) = config.listener().tcp() {
        server.tcp(tcp.addr(), authenticator.clone());
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
            server.tls(tls.addr(), identity, authenticator.clone())?;

            let renewal_signal = server_certificate_renewal(renew_at);
            Either::Left(renewal_signal)
        }
        None => Either::Right(future::pending()),
    };

    // Prepare shutdown signal which is either SYSTEM shutdown signal or cert renewal timout
    pin_mut!(renewal_signal);
    let shutdown = future::select(shutdown_signal, renewal_signal).map(drop);

    // Start the sidecars
    let system_address = config.listener().system().addr().to_string();
    let (sidecar_shutdown_handle, sidecar_join_handle) =
        start_sidecars(broker_handle, system_address).await?;

    // Start serving new connections
    let state = server.serve(shutdown).await?;

    // Shutdown the sidecars
    sidecar_shutdown_handle.shutdown()?;
    sidecar_join_handle.await??;

    Ok(state)
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

// TODO: We need to elevate failable init steps out of the async block running in it's own thread.
//       This is because we need to have defined setup steps, that once complete, the sidecars can run without failing.
//       Once this is done, complex startup order will be easier to implement.
//       We can also stop the broker process if sidecars fail to init.
//
//       This change depends on restructuring the startup logic so that:
//         1. sidecars and server run at same time
//         2. if one fails the other shuts down
async fn start_sidecars(
    broker_handle: BrokerHandle,
    system_address: String,
) -> Result<(SidecarShutdownHandle, JoinHandle<Result<()>>)> {
    let (sidecar_termination_handle, sidecar_termination_receiver) = channel::<()>();

    let mut bridge_controller = BridgeController::new();
    let bridge = bridge_controller.start();
    bridge.await?;

    let sidecars = tokio::spawn(async move {
        let device_id = env::var(DEVICE_ID_ENV)?;
        let commands: HashMap<String, Box<dyn Command + Send>> = init_commands();
        let command_handler =
            CommandHandler::new(broker_handle, system_address, device_id.as_str(), commands)
                .await?;
        let command_handler_shutdown_handle = command_handler.shutdown_handle()?;

        let command_handler_join_handle = tokio::spawn(command_handler.run());

        if let Err(e) = sidecar_termination_receiver.await {
            error!(message = "failed to listen to sidecar termination", error = %e);
        }

        if let Err(e) = command_handler_shutdown_handle.shutdown().await {
            error!(message = "failed shutting down command handler", error = %e);
        }
        if let Err(e) = command_handler_join_handle.await {
            error!(message = "failed waiting for command handler shutdown", error = %e);
        }

        Ok(())
    });

    Ok((SidecarShutdownHandle(sidecar_termination_handle), sidecars))
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
