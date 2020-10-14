#![allow(dead_code)]
use std::{
    collections::HashMap,
    convert::TryInto,
    fmt::{Display, Formatter, Result as FmtResult},
};

use futures_util::{
    future::{select, Either, FutureExt},
    pin_mut, select,
    stream::{StreamExt, TryStreamExt},
};
use tokio::sync::{mpsc::Sender, oneshot, oneshot::Receiver};
use tracing::{debug, error, info};

use mqtt3::PublishHandle;

use crate::{
    bridge::BridgeError,
    client::{ClientShutdownHandle, MqttClient},
    messages::MessageHandler,
    persist::{MessageLoader, PublicationStore, WakingMemoryStore},
    settings::{ConnectionSettings, Credentials, TopicRule},
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

/// Provides context used for logging in the bridge pumps and client implementations
#[derive(Debug, Clone)]
pub struct PumpContext {
    pump_type: PumpType,
    bridge_name: String,
}

impl PumpContext {
    pub fn new(pump_type: PumpType, bridge_name: String) -> Self {
        Self {
            pump_type,
            bridge_name,
        }
    }
}

impl Display for PumpContext {
    fn fmt(&self, f: &mut Formatter<'_>) -> FmtResult {
        write!(
            f,
            "{:?} pump on {} bridge",
            self.pump_type, self.bridge_name
        )
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
        device_id: String,
    ) -> Result<Self, BridgeError> {
        let local_client_id = format!(
            "{}/$edgeHub/$bridge/{}",
            device_id,
            connection_settings.name()
        );

        let forwards: HashMap<String, TopicRule> = connection_settings
            .forwards()
            .iter()
            .map(|sub| Self::format_key_value(sub))
            .collect();

        let subscriptions: HashMap<String, TopicRule> = connection_settings
            .subscriptions()
            .iter()
            .map(|sub| Self::format_key_value(sub))
            .collect();

        let outgoing_persist = PublicationStore::new_memory(BATCH_SIZE);
        let incoming_persist = PublicationStore::new_memory(BATCH_SIZE);
        let outgoing_loader = outgoing_persist.loader();
        let incoming_loader = incoming_persist.loader();

        let remote_pump = Self::prepare_pump(
            outgoing_loader,
            incoming_persist.clone(),
            outgoing_persist.clone(),
            connection_settings.address(),
            connection_settings.credentials(),
            PumpType::Remote,
            subscriptions,
            connection_settings,
        )?;

        let local_pump = Self::prepare_pump(
            incoming_loader,
            outgoing_persist.clone(),
            incoming_persist.clone(),
            system_address,
            &Credentials::Anonymous(local_client_id),
            PumpType::Local,
            forwards,
            connection_settings,
        )?;

        Ok(Self {
            local_pump,
            remote_pump,
        })
    }

    fn prepare_pump(
        loader: MessageLoader<WakingMemoryStore>,
        ingress_store: PublicationStore<WakingMemoryStore>,
        egress_store: PublicationStore<WakingMemoryStore>,
        address: &str,
        credentials: &Credentials,
        pump_type: PumpType,
        mut topic_mappings: HashMap<String, TopicRule>,
        connection_settings: &ConnectionSettings,
    ) -> Result<Pump, BridgeError> {
        let (subscriptions, topic_rules): (Vec<_>, Vec<_>) = topic_mappings.drain().unzip();
        let topic_filters = topic_rules
            .into_iter()
            .map(|topic| topic.try_into())
            .collect::<Result<Vec<_>, _>>()?;
        let pump_context = PumpContext::new(pump_type, connection_settings.name().to_string());
        let client = MqttClient::tls(
            address,
            connection_settings.keep_alive(),
            connection_settings.clean_session(),
            MessageHandler::new(ingress_store.clone(), topic_filters),
            credentials,
            pump_context.clone(),
        );
        Ok(Pump::new(
            client,
            subscriptions,
            loader,
            egress_store,
            pump_context,
        )?)
    }

    fn format_key_value(topic: &TopicRule) -> (String, TopicRule) {
        let key = if let Some(local) = topic.local() {
            format!("{}/{}", local, topic.pattern().to_string())
        } else {
            topic.pattern().into()
        };
        (key, topic.clone())
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
    publish_handle: PublishHandle,
    subscriptions: Vec<String>,
    loader: MessageLoader<WakingMemoryStore>,
    persist: PublicationStore<WakingMemoryStore>,
    pump_context: PumpContext,
}

impl Pump {
    fn new(
        client: MqttClient<MessageHandler<WakingMemoryStore>>,
        subscriptions: Vec<String>,
        loader: MessageLoader<WakingMemoryStore>,
        persist: PublicationStore<WakingMemoryStore>,
        pump_context: PumpContext,
    ) -> Result<Self, BridgeError> {
        let publish_handle = client
            .publish_handle()
            .map_err(BridgeError::PublishHandle)?;
        let client_shutdown = client.shutdown_handle()?;

        Ok(Self {
            client,
            client_shutdown,
            publish_handle,
            subscriptions,
            loader,
            persist,
            pump_context,
        })
    }

    pub async fn subscribe(&mut self) -> Result<(), BridgeError> {
        self.client
            .subscribe(&self.subscriptions)
            .await
            .map_err(BridgeError::Subscribe)?;

        Ok(())
    }

    pub async fn run(&mut self, shutdown: Receiver<()>) {
        debug!(
            "starting pumps for {} bridge...",
            self.pump_context.bridge_name
        );

        let (loader_shutdown, loader_shutdown_rx) = oneshot::channel::<()>();
        let publish_handle = self.publish_handle.clone();
        let persist = self.persist.clone();
        let mut loader = self.loader.clone();
        let mut client_shutdown = self.client_shutdown.clone();
        let pump_context = self.pump_context.clone();

        // egress pump
        let egress_pump = async {
            let mut receive_fut = loader_shutdown_rx.into_stream();

            info!("{} starting egress publication processing", pump_context);

            loop {
                let mut publish_handle = publish_handle.clone();
                match select(receive_fut.next(), loader.try_next()).await {
                    Either::Left((shutdown, _)) => {
                        info!(
                            "{} received shutdown signal for egress messages",
                            pump_context
                        );
                        if shutdown.is_none() {
                            error!("{} has unexpected behavior from shutdown signal while signalling bridge pump shutdown", pump_context);
                        }

                        break;
                    }
                    Either::Right((loaded_element, _)) => {
                        debug!("{} extracted publication from store", pump_context);

                        if let Ok(Some((key, publication))) = loaded_element {
                            debug!("{} publishing {:?}", pump_context, key);
                            if let Err(e) = publish_handle.publish(publication).await {
                                let err_msg = format!("{} failed publish", pump_context);
                                error!(message = err_msg.as_str(), err = %e);
                            }

                            if let Err(e) = persist.remove(key) {
                                let err_msg = format!(
                                    "{} failed removing publication from store",
                                    pump_context
                                );
                                error!(message = err_msg.as_str(), err = %e);
                            }
                        }
                    }
                }
            }

            info!("{} stopped sending egress messages", pump_context);
        };

        // ingress pump
        let pump_context = self.pump_context.clone();
        let ingress_pump_fut = async {
            debug!("{} starting ingress publication processing", pump_context);
            self.client.handle_events().await;
        };

        let egress_pump = egress_pump.fuse();
        let ingress_pump = ingress_pump_fut.fuse();
        let mut shutdown = shutdown.fuse();
        pin_mut!(egress_pump, ingress_pump);

        select! {
            _ = egress_pump => {
                error!("{} failed egress publication loop and exited", pump_context);
            },
            _ = ingress_pump => {
                error!("{} failed ingress publication loop and exited", pump_context);
            },
            _ = shutdown => {
                if let Err(e) = client_shutdown.shutdown().await {
                    let err_msg = format!("{} failed to shutdown ingress publication loop", pump_context);
                    error!(message = err_msg.as_str(), err = %e);
                }

                if let Err(e) = loader_shutdown.send(()) {
                    let err_msg = format!("{} failed to shutdown egress publication loop", pump_context);
                    error!("{} {:?}", err_msg, e);
                }
            },
        };

        egress_pump.await;
        ingress_pump.await;
    }
}
