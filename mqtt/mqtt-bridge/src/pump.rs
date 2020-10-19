#![allow(dead_code)]
use std::{
    collections::HashMap,
    convert::TryInto,
    fmt::{Display, Formatter, Result as FmtResult},
};

use futures_util::{
    future::{select, Either, FutureExt},
    pin_mut,
    stream::{StreamExt, TryStreamExt},
};
use tokio::sync::{mpsc::Sender, oneshot, oneshot::Receiver};
use tracing::{debug, error, info};

use crate::{
    bridge::BridgeError,
    client::{ClientShutdownHandle, InFlightPublishHandle, MqttClient},
    messages::MessageHandler,
    persist::{MessageLoader, PublicationStore, WakingMemoryStore},
    settings::{ConnectionSettings, Credentials, Direction, TopicRule},
};

const BATCH_SIZE: usize = 10;

#[derive(Debug, PartialEq)]
pub enum PumpMessage {
    ConnectivityUpdate(ConnectivityState),
    ConfigurationUpdate(ConnectionSettings),
}

pub struct PumpHandle {
    sender: Sender<PumpMessage>,
}

impl PumpHandle {
    pub fn new(sender: Sender<PumpMessage>) -> Self {
        Self { sender }
    }

    pub async fn send(&mut self, message: PumpMessage) -> Result<(), BridgeError> {
        self.sender
            .send(message)
            .await
            .map_err(BridgeError::SenderToPump)
    }
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum ConnectivityState {
    Connected,
    Disconnected,
}

impl Display for ConnectivityState {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        match self {
            Self::Connected => write!(f, "Connected"),
            Self::Disconnected => write!(f, "Disconnected"),
        }
    }
}

/// Specifies if a pump is local or remote
#[derive(Debug, Clone)]
pub enum PumpType {
    Local,
    Remote,
}

/// Pumps always exists in a pair
/// This abstraction provides a convenience function to create two pumps at once
pub struct PumpPair {
    pub local_pump: Pump,
    pub remote_pump: Pump,
}

impl PumpPair {
    pub fn new(
        connection_settings: &ConnectionSettings,
        system_address: &str,
        device_id: &str,
        max_in_flight: usize,
    ) -> Result<Self, BridgeError> {
        let local_client_id = format!(
            "{}/$edgeHub/$bridge/{}",
            device_id,
            connection_settings.name()
        );

        let forwards: HashMap<String, TopicRule> = connection_settings
            .subscriptions()
            .iter()
            .filter_map(|s| {
                if *s.direction() == Direction::Out {
                    Some(Self::format_key_value(s))
                } else {
                    None
                }
            })
            .collect();

        let subscriptions: HashMap<String, TopicRule> = connection_settings
            .subscriptions()
            .iter()
            .filter_map(|s| {
                if *s.direction() == Direction::In {
                    Some(Self::format_key_value(s))
                } else {
                    None
                }
            })
            .collect();

        let outgoing_persist = PublicationStore::new_memory(BATCH_SIZE);
        let incoming_persist = PublicationStore::new_memory(BATCH_SIZE);
        let outgoing_loader = outgoing_persist.loader();
        let incoming_loader = incoming_persist.loader();

        let remote_pump = Self::prepare_pump(
            outgoing_loader,
            &incoming_persist,
            &outgoing_persist,
            connection_settings.address(),
            connection_settings.credentials(),
            PumpType::Remote,
            subscriptions,
            connection_settings,
            max_in_flight,
        )?;

        let local_pump = Self::prepare_pump(
            incoming_loader,
            &outgoing_persist,
            &incoming_persist,
            system_address,
            &Credentials::Anonymous(local_client_id),
            PumpType::Local,
            forwards,
            connection_settings,
            max_in_flight,
        )?;

        Ok(Self {
            local_pump,
            remote_pump,
        })
    }

    // If this grows by even more we can find a workaround
    #[allow(clippy::too_many_arguments)]
    fn prepare_pump(
        loader: MessageLoader<WakingMemoryStore>,
        ingress_store: &PublicationStore<WakingMemoryStore>,
        egress_store: &PublicationStore<WakingMemoryStore>,
        address: &str,
        credentials: &Credentials,
        pump_type: PumpType,
        mut topic_mappings: HashMap<String, TopicRule>,
        connection_settings: &ConnectionSettings,
        max_in_flight: usize,
    ) -> Result<Pump, BridgeError> {
        let (subscriptions, topic_rules): (Vec<_>, Vec<_>) = topic_mappings.drain().unzip();
        let topic_filters = topic_rules
            .into_iter()
            .map(|topic| topic.try_into())
            .collect::<Result<Vec<_>, _>>()?;

        let client = match pump_type {
            PumpType::Local => MqttClient::tcp(
                address,
                connection_settings.keep_alive(),
                connection_settings.clean_session(),
                MessageHandler::new(ingress_store.clone(), topic_filters),
                credentials,
                max_in_flight,
            ),
            PumpType::Remote => MqttClient::tls(
                address,
                connection_settings.keep_alive(),
                connection_settings.clean_session(),
                MessageHandler::new(ingress_store.clone(), topic_filters),
                credentials,
                max_in_flight,
            ),
        }
        .map_err(BridgeError::PublishHandle)?;

        Ok(Pump::new(
            client,
            subscriptions,
            loader,
            egress_store.clone(),
            pump_type,
        )?)
    }

    fn format_key_value(subscription: &TopicRule) -> (String, TopicRule) {
        let key = if let Some(local) = subscription.in_prefix() {
            format!("{}/{}", local, subscription.topic().to_string())
        } else {
            subscription.topic().into()
        };
        (key, subscription.clone())
    }
}

/// Pump used to connect to either local broker or remote brokers (including the upstream edge device)
/// It contains an mqtt client that connects to a local/remote broker
/// After connection there are two simultaneous processes:
/// 1) persist incoming messages into an ingress store to be used by another pump
/// 2) publish outgoing messages from an egress store to the local/remote broker
pub struct Pump {
    client: MqttClient<MessageHandler<WakingMemoryStore>>,
    client_shutdown: ClientShutdownHandle,
    publish_handle: InFlightPublishHandle,
    subscriptions: Vec<String>,
    loader: MessageLoader<WakingMemoryStore>,
    persist: PublicationStore<WakingMemoryStore>,
    pump_type: PumpType,
}

impl Pump {
    fn new(
        client: MqttClient<MessageHandler<WakingMemoryStore>>,
        subscriptions: Vec<String>,
        loader: MessageLoader<WakingMemoryStore>,
        persist: PublicationStore<WakingMemoryStore>,
        pump_type: PumpType,
    ) -> Result<Self, BridgeError> {
        let publish_handle = client.publish_handle();
        let client_shutdown = client.shutdown_handle()?;

        Ok(Self {
            client,
            client_shutdown,
            publish_handle,
            subscriptions,
            loader,
            persist,
            pump_type,
        })
    }

    pub async fn subscribe(&mut self) -> Result<(), BridgeError> {
        self.client
            .subscribe(&self.subscriptions)
            .await
            .map_err(BridgeError::Subscribe)?;

        Ok(())
    }

    #[allow(clippy::too_many_lines)]
    pub async fn run(&mut self, shutdown: Receiver<()>) {
        debug!("starting pump");

        let (loader_shutdown, loader_shutdown_rx) = oneshot::channel::<()>();
        let publish_handle = self.publish_handle.clone();
        let persist = self.persist.clone();
        let mut loader = self.loader.clone();
        let mut client_shutdown = self.client_shutdown.clone();

        // egress loop
        let egress_loop = async {
            let mut receive_fut = loader_shutdown_rx.into_stream();

            info!("starting egress publication processing");

            loop {
                let publish_handle = publish_handle.clone();
                match select(receive_fut.next(), loader.try_next()).await {
                    Either::Left((shutdown, _)) => {
                        info!("received shutdown signal for egress messages",);
                        if shutdown.is_none() {
                            error!("has unexpected behavior from shutdown signal while signaling bridge pump shutdown");
                        }

                        break;
                    }
                    Either::Right((loaded_element, _)) => {
                        debug!("extracted publication from store");

                        if let Ok(Some((key, publication))) = loaded_element {
                            debug!("publishing publication {:?}", key);
                            publish_handle.publish(publication).await;

                            if let Err(e) = persist.remove(key) {
                                error!(err = %e, "failed removing publication from store");
                            }
                        }
                    }
                }
            }

            info!("stopped sending egress messages");
        };

        // ingress loop
        let ingress_loop = async {
            debug!("starting ingress publication processing");
            self.client.handle_events().await;
        };

        // run pumps
        let egress_loop = egress_loop.fuse();
        let ingress_loop = ingress_loop.fuse();
        let shutdown = shutdown.fuse();
        pin_mut!(egress_loop, ingress_loop);
        let pump_processes = select(egress_loop, ingress_loop);

        // wait for shutdown
        match select(pump_processes, shutdown).await {
            // early-stop error
            Either::Left((pump_processes, _)) => {
                error!("stopped early so will shut down");

                match pump_processes {
                    Either::Left((_, ingress_loop)) => {
                        if let Err(e) = client_shutdown.shutdown().await {
                            error!(err = %e, "failed to shutdown ingress publication loop");
                        }

                        ingress_loop.await;
                    }
                    Either::Right((_, egress_loop)) => {
                        if let Err(e) = loader_shutdown.send(()) {
                            error!("failed to shutdown egress publication loop {:?}", e);
                        }

                        egress_loop.await;
                    }
                }
            }
            // shutdown was signaled
            Either::Right((shutdown, pump_processes)) => {
                if let Err(e) = shutdown {
                    error!(err = %e, "failed listening for shutdown");
                }

                if let Err(e) = client_shutdown.shutdown().await {
                    error!(err = %e, "failed to shutdown ingress publication loop");
                }

                if let Err(e) = loader_shutdown.send(()) {
                    error!("failed to shutdown egress publication loop {:?}", e);
                }

                match pump_processes.await {
                    Either::Left((_, ingress_loop)) => {
                        ingress_loop.await;
                    }
                    Either::Right((_, egress_loop)) => {
                        egress_loop.await;
                    }
                }
            }
        }
    }
}
