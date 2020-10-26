use mqtt3::ShutdownError;
use tokio::{select, sync::oneshot::Sender};
use tracing::{debug, error, info, info_span};
use tracing_futures::Instrument;

use crate::{
    client::{ClientError, MqttClientConfig},
    config_update::BridgeDiff,
    persist::{PersistError, PublicationStore, StreamWakeableState, WakingMemoryStore},
    pump::{Builder, Pump, PumpError, PumpHandle, PumpMessage},
    settings::{ConnectionSettings, Credentials},
    upstream::{
        ConnectivityError, LocalUpstreamMqttEventHandler, LocalUpstreamPumpEvent,
        LocalUpstreamPumpEventHandler, RemoteUpstreamMqttEventHandler, RemoteUpstreamPumpEvent,
        RemoteUpstreamPumpEventHandler, RpcError,
    },
};

pub struct BridgeHandle {
    local_pump_handle: PumpHandle<LocalUpstreamPumpEvent>,
    remote_pump_handle: PumpHandle<RemoteUpstreamPumpEvent>,
}

impl BridgeHandle {
    pub fn new(
        local_pump_handle: PumpHandle<LocalUpstreamPumpEvent>,
        remote_pump_handle: PumpHandle<RemoteUpstreamPumpEvent>,
    ) -> Self {
        Self {
            local_pump_handle,
            remote_pump_handle,
        }
    }

    pub async fn send(&mut self, message: BridgeDiff) -> Result<(), BridgeError> {
        let (local_updates, remote_updates) = message.into_parts();

        if local_updates.has_updates() {
            debug!("sending update to local pump {:?}", local_updates);
            self.local_pump_handle
                .send(PumpMessage::ConfigurationUpdate(local_updates))
                .await?;
        }

        if remote_updates.has_updates() {
            debug!("sending update to remote pump {:?}", remote_updates);
            self.remote_pump_handle
                .send(PumpMessage::ConfigurationUpdate(remote_updates))
                .await?;
        }

        Ok(())
    }
}

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
pub struct Bridge<S> {
    local_pump: Pump<S, LocalUpstreamMqttEventHandler<S>, LocalUpstreamPumpEventHandler>,
    remote_pump: Pump<S, RemoteUpstreamMqttEventHandler<S>, RemoteUpstreamPumpEventHandler>,
    connection_settings: ConnectionSettings,
}

impl Bridge<WakingMemoryStore> {
    pub async fn new_upstream(
        system_address: String,
        device_id: String,
        settings: ConnectionSettings,
    ) -> Result<Self, BridgeError> {
        const BATCH_SIZE: usize = 10;

        debug!("creating bridge...");

        let (local_pump, remote_pump) = Builder::default()
            .with_local(|pump| {
                pump.with_config(MqttClientConfig::new(
                    &system_address,
                    settings.keep_alive(),
                    settings.clean_session(),
                    Credentials::Anonymous(format!("{}/{}/$bridge", settings.name(), device_id,)),
                ))
                .with_rules(settings.forwards());
            })
            .with_remote(|pump| {
                pump.with_config(MqttClientConfig::new(
                    settings.address(),
                    settings.keep_alive(),
                    settings.clean_session(),
                    settings.credentials().clone(),
                ))
                .with_rules(settings.subscriptions());
            })
            .with_store(|| PublicationStore::new_memory(BATCH_SIZE))
            .build()?;

        debug!("created bridge...");

        Ok(Bridge {
            local_pump,
            remote_pump,
            connection_settings: settings,
        })
    }
}

impl<S> Bridge<S>
where
    S: StreamWakeableState + Send,
{
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
            "starting pumps for {} bridge...",
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

        debug!("bridge {} stopped...", self.connection_settings.name());
        Ok(())
    }

    pub fn handle(&self) -> BridgeHandle {
        BridgeHandle::new(self.local_pump.handle(), self.remote_pump.handle())
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
    SendToPump,

    #[error("Failed to send message to pump: {0}")]
    SendBridgeUpdate(#[from] PumpError),

    #[error("failed to execute RPC command")]
    Rpc(#[from] RpcError),

    #[error("failed to execute connectivity event")]
    Connectivity(#[from] ConnectivityError),

    #[error("failed to signal bridge shutdown.")]
    ShutdownBridge(()),

    #[error("failed to get publish handle from client.")]
    PublishHandle(#[source] ClientError),

    #[error("failed to get subscribe handle from client.")]
    UpdateSubscriptionHandle(#[source] ClientError),

    #[error("failed to get publish handle from client.")]
    ClientShutdown(#[from] ShutdownError),
}
