use std::{cell::BorrowMutError, collections::HashMap, convert::TryInto};

use futures_util::{future::select, future::Either, pin_mut};
use mqtt3::ShutdownError;
use tokio::sync::oneshot;
use tokio::sync::oneshot::Sender;
use tracing::{debug, error, info};

use crate::{
    client::{ClientError, MqttClient},
    message_handler::MessageHandler,
    persist::{PersistError, PublicationStore},
    pump::{Pump, PumpType},
    settings::{ConnectionSettings, Credentials, TopicRule},
};

const BATCH_SIZE: usize = 10;

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
    local_pump: Pump,
    remote_pump: Pump,
    connection_settings: ConnectionSettings,
}

impl Bridge {
    pub async fn new(
        system_address: String,
        device_id: String,
        connection_settings: ConnectionSettings,
    ) -> Result<Self, BridgeError> {
        debug!("creating bridge...{}", connection_settings.name());

        let mut forwards: HashMap<String, TopicRule> = connection_settings
            .forwards()
            .iter()
            .map(|sub| Self::format_key_value(sub))
            .collect();

        let mut subscriptions: HashMap<String, TopicRule> = connection_settings
            .subscriptions()
            .iter()
            .map(|sub| Self::format_key_value(sub))
            .collect();

        let outgoing_persist = PublicationStore::new_memory(BATCH_SIZE);
        let incoming_persist = PublicationStore::new_memory(BATCH_SIZE);
        let outgoing_loader = outgoing_persist.loader();
        let incoming_loader = incoming_persist.loader();

        let (remote_subscriptions, remote_topic_rules): (Vec<_>, Vec<_>) =
            subscriptions.drain().unzip();
        let remote_topic_filters = remote_topic_rules
            .into_iter()
            .map(|topic| topic.try_into())
            .collect::<Result<Vec<_>, _>>()?;
        let remote_client = MqttClient::tls(
            connection_settings.address(),
            connection_settings.keep_alive(),
            connection_settings.clean_session(),
            MessageHandler::new(incoming_persist.clone(), remote_topic_filters),
            connection_settings.credentials(),
        );

        let local_client_id = format!(
            "{}/$edgeHub/$bridge/{}",
            device_id,
            connection_settings.name()
        );
        let (local_subscriptions, local_topic_rules): (Vec<_>, Vec<_>) = forwards.drain().unzip();
        let local_topic_filters = local_topic_rules
            .into_iter()
            .map(|topic| topic.try_into())
            .collect::<Result<Vec<_>, _>>()?;
        let local_client = MqttClient::tcp(
            system_address.as_str(),
            connection_settings.keep_alive(),
            connection_settings.clean_session(),
            MessageHandler::new(outgoing_persist.clone(), local_topic_filters),
            &Credentials::Anonymous(local_client_id),
        );

        let mut local_pump = Pump::new(
            local_client,
            local_subscriptions,
            incoming_loader,
            incoming_persist,
            PumpType::Local,
            connection_settings.name().to_string(),
        )?;
        let mut remote_pump = Pump::new(
            remote_client,
            remote_subscriptions,
            outgoing_loader,
            outgoing_persist,
            PumpType::Remote,
            connection_settings.name().to_string(),
        )?;

        local_pump.subscribe().await?;
        remote_pump.subscribe().await?;

        debug!("created bridge...{}", connection_settings.name());
        Ok(Bridge {
            local_pump,
            remote_pump,
            connection_settings,
        })
    }

    fn format_key_value(topic: &TopicRule) -> (String, TopicRule) {
        let key = if let Some(local) = topic.local() {
            format!("{}/{}", local, topic.pattern().to_string())
        } else {
            topic.pattern().into()
        };
        (key, topic.clone())
    }

    pub async fn run(mut self) -> Result<(), BridgeError> {
        info!("Starting {} bridge...", self.connection_settings.name());

        let (local_shutdown, local_shutdown_listener) = oneshot::channel::<()>();
        let (remote_shutdown, remote_shutdown_listener) = oneshot::channel::<()>();
        let shutdown_handle = BridgeShutdownHandle {
            local_shutdown,
            remote_shutdown,
        };

        let local_pump = self.local_pump.run(local_shutdown_listener);
        let remote_pump = self.remote_pump.run(remote_shutdown_listener);
        pin_mut!(local_pump);
        pin_mut!(remote_pump);

        // TODO PRE: Do we want to shut down?
        //           We can either recreate the pump or shut everything down and start over.
        //
        //           If there is a client error then this can potentially get reset without the pump shutting down
        //           Alternatively, if this client error shuts down the pump, we will need to recreate it.
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

/// Authentication error.
#[derive(Debug, thiserror::Error)]
pub enum BridgeError {
    #[error("Failed to save to store.")]
    Store(#[from] PersistError),

    #[error("Failed to subscribe to topic.")]
    Subscribe(#[source] ClientError),

    #[error("Failed to parse topic pattern.")]
    TopicFilterParse(#[from] mqtt_broker::Error),

    #[error("Failed to load settings.")]
    LoadingSettings(#[from] config::ConfigError),

    #[error("Failed to signal bridge shutdown.")]
    ShutdownBridge(()),

    #[error("Failed to get publish handle from client.")]
    PublishHandle(#[source] ClientError),

    #[error("Failed to get publish handle from client.")]
    ClientShutdown(#[from] ShutdownError),

    #[error("Failed to borrow persistence to store publication")]
    BorrowPersist(#[from] BorrowMutError),
}
