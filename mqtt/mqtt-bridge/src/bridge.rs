use mqtt3::ShutdownError;
use tokio::{select, sync::oneshot::Sender};
use tracing::{debug, error, info, info_span};
use tracing_futures::Instrument;

use crate::{
    client::{ClientError, MqttClientConfig},
    persist::{PersistError, PublicationStore, StreamWakeableState, WakingMemoryStore},
    pump::{Builder, Pump},
    settings::{ConnectionSettings, Credentials},
    upstream::{
        ConnectivityError, LocalUpstreamMqttEventHandler, LocalUpstreamPumpEventHandler,
        RemoteUpstreamMqttEventHandler, RemoteUpstreamPumpEventHandler, RpcError,
    },
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

        let (mut local_pump, mut remote_pump) = Builder::default()
            .with_local(|pump| {
                pump.with_config(MqttClientConfig::new(
                    &system_address,
                    settings.keep_alive(),
                    settings.clean_session(),
                    Credentials::Anonymous(format!(
                        "{}/$edgeHub/$bridge/{}",
                        device_id,
                        settings.name()
                    )),
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

        // TODO move subscriptions into run method
        local_pump
            .subscribe()
            .instrument(info_span!("pump", name = "local"))
            .await?;

        remote_pump
            .subscribe()
            .instrument(info_span!("pump", name = "remote"))
            .await?;

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
