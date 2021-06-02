mod builder;
mod egress;
mod ingress;
mod messages;

use std::{collections::HashMap, error::Error as StdError, fmt::Debug, future::Future, sync::Arc};

pub use builder::Builder;
use egress::{Egress, EgressError, EgressShutdownHandle};
use ingress::{Ingress, IngressError, IngressShutdownHandle};
pub use messages::PumpMessageHandler;
use messages::{MessageProcessorError, MessagesProcessor, MessagesProcessorShutdownHandle};

use futures_util::{
    future::{self, Either},
    join, pin_mut,
};
use mockall::automock;
use parking_lot::RwLock;
use tokio::sync::mpsc;
use tracing::{debug, error, info, warn};

use crate::{
    bridge::BridgeError,
    client::{MqttClient, MqttClientExt, MqttEventHandler},
    config_update::PumpDiff,
    messages::TopicMapper,
    persist::{PublicationStore, StreamWakeableState},
};

#[cfg(test)]
pub fn channel<M: Debug + Send + 'static>() -> (PumpHandle<M>, mpsc::Receiver<PumpMessage<M>>) {
    let (tx, rx) = tokio::sync::mpsc::channel(10);
    (PumpHandle::new(tx), rx)
}

#[derive(Debug, thiserror::Error)]
pub enum PumpError {
    #[error("unable to send command to pump. Caused by: {0:?}")]
    Send(Box<dyn StdError + Send>),

    #[error("error ocurred when running pump. Caused by: {0}")]
    Run(Box<dyn StdError + Send + Sync>),
}

/// Pump is used to connect to either local broker or remote brokers
/// (including the upstream edge device)
///
/// It contains several tasks running in parallel: ingress, egress and events processing.
///
/// During `ingress` pump handles incoming MQTT publications and puts them
/// into the store. The opposite pump will read publications from a store
/// and forwards them to the corresponding broker.
///
/// During `egress` pump reads pulications from its own store and sends them
/// to the broker MQTT client connected to.
///
/// Messages processing is intended to control pump behavior: initiate pump
/// shutdown, handle configuration update or another specific event.
pub struct Pump<S, H, M>
where
    M: PumpMessageHandler,
{
    messages_send: mpsc::Sender<PumpMessage<M::Message>>,
    messages: MessagesProcessor<M>,
    egress: Egress<S>,
    ingress: Ingress<H>,
}

impl<S, H, M> Pump<S, H, M>
where
    H: MqttEventHandler,
    M: PumpMessageHandler,
    M::Message: Debug + Send + 'static,
    S: StreamWakeableState,
{
    /// Creates a new instance of pump.
    fn new(
        messages_send: mpsc::Sender<PumpMessage<M::Message>>,
        client: MqttClient<H>,
        store: PublicationStore<S>,
        messages: MessagesProcessor<M>,
    ) -> Result<Self, BridgeError> {
        let client_shutdown = client.shutdown_handle()?;
        let publish_handle = client
            .publish_handle()
            .map_err(BridgeError::PublishHandle)?;

        let egress = Egress::new(publish_handle, store);
        let ingress = Ingress::new(client, client_shutdown);

        Ok(Self {
            messages_send,
            messages,
            egress,
            ingress,
        })
    }

    /// Returns a handle to send control messages to a pump.
    pub fn handle(&self) -> PumpHandle<M::Message> {
        PumpHandle::new(self.messages_send.clone())
    }

    /// Orchestrates starting of egress, ingress and controll messages
    /// processing and waits for all of them to finish.
    ///
    /// Attempts to start all routines in the same task in parallel and
    /// waits for any of them to finish. It sends shutdown to other ones
    /// and waits until all of them stopped.
    pub async fn run(mut self) -> Result<(), PumpError> {
        info!("starting pump...");

        let shutdown_egress = self.egress.handle();
        let egress = self.egress.run();

        let shutdown_ingress = self.ingress.handle();
        let ingress = self.ingress.run();

        let shutdown_messages = self.messages.handle();
        let messages = self.messages.run();

        pin_mut!(egress, ingress, messages);

        match future::select(&mut messages, future::select(&mut egress, &mut ingress)).await {
            Either::Left((messages, _)) => {
                if let Err(e) = &messages {
                    error!(error = %e, "pump messages processor exited with error");
                } else {
                    debug!("pump messages processor exited");
                }

                debug!("shutting down both ingress and egress...");
                join!(
                    stop_ingress(ingress, shutdown_ingress),
                    stop_egress(egress, shutdown_egress)
                );

                messages.map_err(|e| PumpError::Run(e.into()))
            }
            Either::Right((Either::Left((egress, ingress)), messages)) => {
                if let Err(e) = &egress {
                    error!(error = %e, "egress processing exited with error");
                } else {
                    debug!("egress processing exited");
                }

                debug!("shutting down both ingress and messages processor...");
                join!(
                    stop_ingress(ingress, shutdown_ingress),
                    stop_messages(messages, shutdown_messages),
                );

                egress.map_err(|e| PumpError::Run(e.into()))
            }
            Either::Right((Either::Right((ingress, egress)), messages)) => {
                if let Err(e) = &ingress {
                    error!(error = %e, "ingress processing exited with error");
                } else {
                    debug!("ingress processing exited");
                }

                debug!("shutting down both egress and messages processor...");
                join!(
                    stop_egress(egress, shutdown_egress),
                    stop_messages(messages, shutdown_messages)
                );

                ingress.map_err(|e| PumpError::Run(e.into()))
            }
        }?;

        info!("pump stopped");
        Ok(())
    }
}

async fn stop_ingress<F>(ingress: F, shutdown_handle: IngressShutdownHandle)
where
    F: Future<Output = Result<(), IngressError>>,
{
    let (_, ingress) = join!(shutdown_handle.shutdown(), ingress);

    if let Err(e) = ingress {
        error!(error = %e, "ingress processing exited with error");
    } else {
        debug!("ingress processing exited");
    }
}

async fn stop_egress<F>(egress: F, shutdown_handle: EgressShutdownHandle)
where
    F: Future<Output = Result<(), EgressError>>,
{
    let (_, egress) = join!(shutdown_handle.shutdown(), egress);

    if let Err(e) = egress {
        error!(error = %e, "egress processing exited with error");
    } else {
        debug!("egress processing exited");
    }
}

async fn stop_messages<F, M>(messages: F, shutdown_handle: MessagesProcessorShutdownHandle<M>)
where
    F: Future<Output = Result<(), MessageProcessorError>>,
    M: Debug + Send + 'static,
{
    let (_, messages) = join!(shutdown_handle.shutdown(), messages);

    if let Err(e) = messages {
        error!(error = %e, "pump messages processor exited with error");
    } else {
        debug!("pump messages processor exited");
    }
}

/// A message to control pump behavior.
#[derive(Debug, PartialEq)]
pub enum PumpMessage<E> {
    Event(E),
    ConfigurationUpdate(PumpDiff),
    Shutdown,
}

/// A handle to send control messages to the pump.
pub struct PumpHandle<M> {
    sender: mpsc::Sender<PumpMessage<M>>,
}

#[automock]
impl<M: Debug + Send + 'static> PumpHandle<M> {
    /// Creates a new instance of pump handle.
    fn new(sender: mpsc::Sender<PumpMessage<M>>) -> Self {
        Self { sender }
    }

    /// Sends a control message to a pump.
    pub async fn send(&mut self, message: PumpMessage<M>) -> Result<(), PumpError> {
        self.sender
            .send(message)
            .await
            .map_err(|e| PumpError::Send(Box::new(e)))
    }

    /// Sends a shutdown control message to a pump.
    pub async fn shutdown(mut self) {
        if let Err(e) = self.send(PumpMessage::Shutdown).await {
            warn!(error = %e, "unable to request shutdown for pump. probably the pump is about to exit");
        }
    }
}

/// Topic settings received as updates from twin or from initial configuration in the default config file
#[derive(Clone)]
pub struct TopicMapperUpdates(Arc<RwLock<HashMap<String, TopicMapper>>>);

impl TopicMapperUpdates {
    pub fn new(mappings: HashMap<String, TopicMapper>) -> Self {
        Self(Arc::new(RwLock::new(mappings)))
    }

    pub fn insert(&self, topic_filter: &str, mapper: TopicMapper) -> Option<TopicMapper> {
        self.0.write().insert(topic_filter.into(), mapper)
    }

    pub fn remove(&self, topic_filter: &str) -> Option<TopicMapper> {
        self.0.write().remove(topic_filter)
    }

    pub fn get(&self, topic_filter: &str) -> Option<TopicMapper> {
        self.0.read().get(topic_filter).cloned()
    }

    pub fn contains_key(&self, topic_filter: &str) -> bool {
        self.0.read().contains_key(topic_filter)
    }

    pub fn subscriptions(&self) -> Vec<String> {
        self.0
            .read()
            .values()
            .map(TopicMapper::subscribe_to)
            .collect()
    }
}
