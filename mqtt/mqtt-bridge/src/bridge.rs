use futures_util::{future::select, future::Either, pin_mut};
use mqtt3::ShutdownError;
use tokio::sync::{mpsc::error::SendError, oneshot, oneshot::Sender};
use tracing::{debug, error, info, info_span};
use tracing_futures::Instrument;

use crate::{
    client::ClientError,
    persist::PersistError,
    pump::{PumpMessage, PumpPair},
    rpc::RpcError,
    settings::ConnectionSettings,
};

#[derive(Debug)]
pub struct BridgeShutdownHandle {
    local_shutdown: Sender<()>,
    remote_shutdown: Sender<()>,
}

impl BridgeShutdownHandle {
    // TODO: Remove when we implement bridge controller shutdown
    #![allow(dead_code)]
    pub async fn shutdown(self) -> Result<(), BridgeError> {
        self.local_shutdown
            .send(())
            .map_err(BridgeError::ShutdownBridge)?;
        self.remote_shutdown
            .send(())
            .map_err(BridgeError::ShutdownBridge)?;
        Ok(())
    }
}

/// Bridge implementation that connects to local broker and remote broker and handles messages flow
pub struct Bridge {
    pumps: PumpPair,
    connection_settings: ConnectionSettings,
}

impl Bridge {
    pub async fn new(
        system_address: String,
        device_id: String,
        connection_settings: ConnectionSettings,
    ) -> Result<Self, BridgeError> {
        debug!("creating bridge...{}", connection_settings.name());

        let mut pumps = PumpPair::new(&connection_settings, &system_address, &device_id)?;

        let local_pump_span = info_span!("local pump");
        pumps
            .local_pump
            .subscribe()
            .instrument(local_pump_span)
            .await?;

        let remote_pump_span = info_span!("remote pump");
        pumps
            .remote_pump
            .subscribe()
            .instrument(remote_pump_span)
            .await?;

        debug!("created bridge...{}", connection_settings.name());
        Ok(Bridge {
            pumps,
            connection_settings,
        })
    }

    pub async fn run(mut self) -> Result<(), BridgeError> {
        info!("Starting {} bridge...", self.connection_settings.name());

        let (local_shutdown, local_shutdown_listener) = oneshot::channel::<()>();
        let (remote_shutdown, remote_shutdown_listener) = oneshot::channel::<()>();
        let shutdown_handle = BridgeShutdownHandle {
            local_shutdown,
            remote_shutdown,
        };

        let local_pump_span = info_span!("local pump");
        let local_pump = self
            .pumps
            .local_pump
            .run(local_shutdown_listener)
            .instrument(local_pump_span);

        let remote_pump_span = info_span!("remote pump");
        let remote_pump = self
            .pumps
            .remote_pump
            .run(remote_shutdown_listener)
            .instrument(remote_pump_span);
        pin_mut!(local_pump, remote_pump);

        debug!(
            "Starting pumps for {} bridge...",
            self.connection_settings.name()
        );
        match select(local_pump, remote_pump).await {
            Either::Left(_) => {
                shutdown_handle.shutdown().await?;
            }
            Either::Right(_) => {
                shutdown_handle.shutdown().await?;
            }
        }

        debug!("Bridge {} stopped...", self.connection_settings.name());
        Ok(())
    }
}

/// Bridge error.
#[derive(Debug, thiserror::Error)]
pub enum BridgeError {
    #[error("failed to save to store.")]
    Store(#[from] PersistError),

    #[error("failed to subscribe to topic.")]
    Subscribe(#[source] ClientError),

    #[error("failed to parse topic pattern.")]
    TopicFilterParse(#[from] mqtt_broker::Error),

    #[error("failed to load settings.")]
    LoadingSettings(#[from] config::ConfigError),

    #[error("failed to get send pump message.")]
    SenderToPump(#[from] SendError<PumpMessage>),

    #[error("failed to execute RPC command")]
    Rpc(#[from] RpcError),

    #[error("failed to signal bridge shutdown.")]
    ShutdownBridge(()),

    #[error("failed to get publish handle from client.")]
    PublishHandle(#[source] ClientError),

    #[error("failed to get publish handle from client.")]
    ClientShutdown(#[from] ShutdownError),
}
