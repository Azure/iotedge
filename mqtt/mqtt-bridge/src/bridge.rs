use mqtt3::ShutdownError;
use tokio::{
    select,
    sync::{mpsc::error::SendError, oneshot::Sender},
};
use tracing::{debug, error, info, info_span};
use tracing_futures::Instrument;

use crate::{
    client::ClientError,
    messages::LocalUpstreamHandler,
    messages::MessageHandler,
    persist::PersistError,
    persist::PublicationStore,
    persist::WakingMemoryStore,
    pump::{self, Pump, PumpMessage},
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
    local_pump: Pump<LocalUpstreamHandler<WakingMemoryStore>>,
    remote_pump: Pump<MessageHandler<WakingMemoryStore>>,
    connection_settings: ConnectionSettings,
}

impl Bridge {
    pub async fn new(
        system_address: String,
        device_id: String,
        connection_settings: ConnectionSettings,
    ) -> Result<Self, BridgeError> {
        const BATCH_SIZE: usize = 10;

        debug!("creating bridge...{}", connection_settings.name());

        let outgoing_persist = PublicationStore::new_memory(BATCH_SIZE);
        let incoming_persist = PublicationStore::new_memory(BATCH_SIZE);

        let mut local_pump = pump::local_pump(
            &connection_settings,
            system_address,
            device_id,
            incoming_persist.clone(),
            outgoing_persist.clone(),
        )?;

        let mut remote_pump =
            pump::remote_pump(&connection_settings, incoming_persist, outgoing_persist)?;

        local_pump
            .subscribe()
            .instrument(info_span!("pump", name = "local"))
            .await?;

        remote_pump
            .subscribe()
            .instrument(info_span!("pump", name = "remote"))
            .await?;

        debug!("created bridge...{}", connection_settings.name());
        Ok(Bridge {
            local_pump,
            remote_pump,
            connection_settings,
        })
    }

    pub async fn run(self) -> Result<(), BridgeError> {
        info!("Starting {} bridge...", self.connection_settings.name());

        let local_pump = self
            .local_pump
            .run()
            .instrument(info_span!("pump", name = "local"));

        let remote_pump = self
            .remote_pump
            .run()
            .instrument(info_span!("pump", name = "remote"));

        debug!(
            "Starting pumps for {} bridge...",
            self.connection_settings.name()
        );

        select! {
            _ = local_pump => {
                // TODO shutdown remote pump
                // shutdown_handle.shutdown().await?;
            }

            _ = remote_pump => {
                // TODO shutdown local pump
                // shutdown_handle.shutdown().await?;
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

    #[error("Failed to get send pump message.")]
    SendToPump(#[from] SendError<PumpMessage>),

    #[error("failed to execute RPC command")]
    Rpc(#[from] RpcError),

    #[error("failed to signal bridge shutdown.")]
    ShutdownBridge(()),

    #[error("failed to get publish handle from client.")]
    PublishHandle(#[source] ClientError),

    #[error("failed to get publish handle from client.")]
    ClientShutdown(#[from] ShutdownError),
}
