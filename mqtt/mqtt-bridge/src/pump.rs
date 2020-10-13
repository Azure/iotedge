#![allow(dead_code)]
use std::{cell::RefCell, collections::HashMap, convert::TryInto, rc::Rc};

use futures_util::{
    future::{select, Either, FutureExt},
    pin_mut, select,
    stream::{StreamExt, TryStreamExt},
};
use tokio::sync::{oneshot, oneshot::Receiver};
use tracing::debug;
use tracing::error;

use mqtt3::PublishHandle;

use crate::{
    bridge::BridgeError,
    client::{ClientShutdownHandle, MqttClient},
    message_handler::MessageHandler,
    persist::{MessageLoader, PublicationStore, WakingMemoryStore},
    settings::{ConnectionSettings, Credentials, TopicRule},
};

const BATCH_SIZE: usize = 10;

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
        connection_settings: ConnectionSettings,
        system_address: String,
        local_client_id: String,
    ) -> Result<Self, BridgeError> {
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
        let remote_pump_context =
            PumpContext::new(PumpType::Remote, connection_settings.name().to_string());
        let remote_client = MqttClient::tls(
            connection_settings.address(),
            connection_settings.keep_alive(),
            connection_settings.clean_session(),
            MessageHandler::new(incoming_persist.clone(), remote_topic_filters),
            connection_settings.credentials(),
            remote_pump_context.clone(),
        );
        let remote_pump = Pump::new(
            remote_client,
            remote_subscriptions,
            outgoing_loader,
            outgoing_persist.clone(),
            remote_pump_context,
        )?;

        let (local_subscriptions, local_topic_rules): (Vec<_>, Vec<_>) = forwards.drain().unzip();
        let local_topic_filters = local_topic_rules
            .into_iter()
            .map(|topic| topic.try_into())
            .collect::<Result<Vec<_>, _>>()?;
        let local_pump_context =
            PumpContext::new(PumpType::Local, connection_settings.name().to_string());
        let local_client = MqttClient::tcp(
            system_address.as_str(),
            connection_settings.keep_alive(),
            connection_settings.clean_session(),
            MessageHandler::new(outgoing_persist, local_topic_filters),
            &Credentials::Anonymous(local_client_id),
            local_pump_context.clone(),
        );
        let local_pump = Pump::new(
            local_client,
            local_subscriptions,
            incoming_loader,
            incoming_persist,
            local_pump_context,
        )?;

        Ok(Self {
            local_pump,
            remote_pump,
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
    loader: Rc<RefCell<MessageLoader<WakingMemoryStore>>>,
    persist: PublicationStore<WakingMemoryStore>,
    pump_context: PumpContext,
}

impl Pump {
    pub fn new(
        client: MqttClient<MessageHandler<WakingMemoryStore>>,
        subscriptions: Vec<String>,
        loader: Rc<RefCell<MessageLoader<WakingMemoryStore>>>,
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
        let loader = self.loader.clone();
        let mut client_shutdown = self.client_shutdown.clone();
        let bridge_name = self.pump_context.bridge_name.clone();
        let pump_type = self.pump_context.pump_type.clone();

        // egress pump
        let f1 = async move {
            let mut loader_borrow = loader.borrow_mut();
            let mut receive_fut = loader_shutdown_rx.into_stream();

            debug!(
                "{} bridge starting egress publication processing for {:?} pump...",
                bridge_name, pump_type
            );

            loop {
                let mut publish_handle = publish_handle.clone();
                match select(receive_fut.next(), loader_borrow.try_next()).await {
                    Either::Left((shutdown, _)) => {
                        debug!("egress pump received shutdown signal");
                        if let None = shutdown {
                            error!(message = "unexpected behavior from shutdown signal while signalling bridge pump shutdown")
                        }

                        debug!("bridge pump stopped");
                        break;
                    }
                    Either::Right((loaded_element, _)) => {
                        debug!("egress pump extracted publication from store");

                        if let Ok(Some((key, publication))) = loaded_element {
                            debug!("publishing publication {:?} for egress pump", key);
                            if let Err(e) = publish_handle.publish(publication).await {
                                error!(message = "failed publishing publication for bridge pump", err = %e);
                            }

                            if let Err(e) = persist.remove(key) {
                                error!(message = "failed removing publication from store", err = %e);
                            }
                        }
                    }
                }
            }

            debug!("pumps for {} bridge stopped...", bridge_name);
        };

        // incoming pump
        let bridge_name = self.pump_context.bridge_name.clone();
        let pump_type = self.pump_context.pump_type.clone();
        let f2 = async move {
            debug!(
                "{} bridge starting ingress publication processing for pump {:?}...",
                bridge_name, pump_type
            );
            self.client.handle_events().await;
        };

        let f1 = f1.fuse();
        let f2 = f2.fuse();
        let mut shutdown = shutdown.fuse();
        pin_mut!(f1);
        pin_mut!(f2);

        select! {
            _ = f1 => {
                error!(message = "publish loop failed and exited for bridge pump");
            },
            _ = f2 => {
                error!(message = "incoming publication loop failed and exited for bridge pump");
            },
            _ = shutdown => {
                if let Err(e) = client_shutdown.shutdown().await {
                    error!(message = "failed to shutdown incoming publication loop for bridge pump", err = %e);
                }

                if let Err(e) = loader_shutdown.send(()) {
                    error!(message = "failed to shutdown publish loop for bridge pump");
                }
            },
        }

        f1.await;
        f2.await;
    }
}
