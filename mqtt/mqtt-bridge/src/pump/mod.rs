mod builder;

pub use builder::Builder;

// #![allow(dead_code, unused_imports, unused_variables)]
use futures_util::{
    stream::{StreamExt, TryStreamExt},
    FutureExt,
};
use tokio::{
    select,
    sync::{
        mpsc::{self, error::SendError},
        oneshot,
    },
};
use tracing::{debug, error, info};

use mqtt3::PublishHandle;

use crate::{
    bridge::BridgeError,
    client::{ClientShutdownHandle, EventHandler, MqttClient},
    connectivity::ConnectivityState,
    persist::{MessageLoader, PublicationStore, WakingMemoryStore},
    rpc::{CommandId, RpcCommand},
    settings::ConnectionSettings,
};

#[derive(Debug, PartialEq)]
pub enum PumpMessage {
    ConnectivityUpdate(ConnectivityState),
    ConfigurationUpdate(ConnectionSettings),
    RpcCommand(CommandId, RpcCommand),
    RpcAck(CommandId, RpcCommand),
    Shutdown,
}

pub struct PumpHandle {
    sender: mpsc::Sender<PumpMessage>,
}

impl PumpHandle {
    fn new(sender: mpsc::Sender<PumpMessage>) -> Self {
        Self { sender }
    }

    pub async fn send(&mut self, message: PumpMessage) -> Result<(), PumpError> {
        self.sender.send(message).await.map_err(PumpError)
    }
}

#[cfg(test)]
pub fn channel() -> (PumpHandle, mpsc::Receiver<PumpMessage>) {
    let (tx, rx) = tokio::sync::mpsc::channel(10);
    (PumpHandle::new(tx), rx)
}

#[derive(Debug, thiserror::Error)]
#[error(transparent)]
pub struct PumpError(SendError<PumpMessage>);

/// Pump used to connect to either local broker or remote brokers (including the upstream edge device)
/// It contains an mqtt client that connects to a local/remote broker
/// After connection there are two simultaneous processes:
/// 1) persist incoming messages into an ingress store to be used by another pump
/// 2) publish outgoing messages from an egress store to the local/remote broker
pub struct Pump<H> {
    subscriptions: Vec<String>,
    messages_send: mpsc::Sender<PumpMessage>,
    messages: MessagesProcessor,
    egress: Egress,
    ingress: Ingress<H>,
}

impl<H: EventHandler> Pump<H> {
    fn new(
        messages_send: mpsc::Sender<PumpMessage>,
        messages_recv: mpsc::Receiver<PumpMessage>,
        client: MqttClient<H>,
        subscriptions: Vec<String>,
        loader: MessageLoader<WakingMemoryStore>,
        store: PublicationStore<WakingMemoryStore>,
    ) -> Result<Self, BridgeError> {
        let client_shutdown = client.shutdown_handle()?;
        let publish_handle = client
            .publish_handle()
            .map_err(BridgeError::PublishHandle)?;

        let egress = Egress::new(publish_handle, loader, store);
        let ingress = Ingress::new(client, client_shutdown);

        let handle = PumpHandle::new(messages_send.clone());
        let messages = MessagesProcessor::new(messages_recv, handle);

        Ok(Self {
            subscriptions,
            messages_send,
            messages,
            egress,
            ingress,
        })
    }

    pub fn handle(&self) -> PumpHandle {
        PumpHandle::new(self.messages_send.clone())
    }

    pub async fn subscribe(&mut self) -> Result<(), BridgeError> {
        self.ingress
            .client
            .subscribe(&self.subscriptions) //TODO react on PumpMessage instead
            .await
            .map_err(BridgeError::Subscribe)?;

        Ok(())
    }

    pub async fn run(mut self) {
        info!("starting pump");

        let shutdown_egress = self.egress.handle();
        let shutdown_ingress = self.ingress.handle();
        let shutdown_messages = self.messages.handle();

        select! {
            _= self.egress.run().fuse() => {
                error!("egress stopped unexpectedly");
                shutdown_ingress.shutdown().await;
                shutdown_messages.shutdown().await;
            },
            _= self.ingress.run().fuse() => {
                error!("ingress stopped unexpectedly");
                shutdown_egress.shutdown().await;
                shutdown_messages.shutdown().await;
            },
            _= self.messages.run().fuse() => {
                info!("stopping pump");
                shutdown_ingress.shutdown().await;
                shutdown_egress.shutdown().await;
            }
        };

        info!("stopped pump");
    }
}

struct MessagesProcessorShutdownHandle(Option<PumpHandle>);

impl MessagesProcessorShutdownHandle {
    async fn shutdown(mut self) {
        if let Some(mut sender) = self.0.take() {
            if let Err(e) = sender.send(PumpMessage::Shutdown).await {
                error!("unable to request shutdown for message processor. {}", e);
            }
        }
    }
}

struct MessagesProcessor {
    messages: mpsc::Receiver<PumpMessage>,
    handle: Option<PumpHandle>,
}

impl MessagesProcessor {
    fn new(messages: mpsc::Receiver<PumpMessage>, handle: PumpHandle) -> Self {
        Self {
            messages,
            handle: Some(handle),
        }
    }

    fn handle(&mut self) -> MessagesProcessorShutdownHandle {
        MessagesProcessorShutdownHandle(self.handle.take())
    }

    async fn run(mut self) {
        info!("starting pump messages processor");
        while let Some(message) = self.messages.next().await {
            match message {
                PumpMessage::ConnectivityUpdate(_) => {}
                PumpMessage::ConfigurationUpdate(_) => {}
                PumpMessage::RpcCommand(_, _) => {}
                PumpMessage::RpcAck(_, _) => {}
                PumpMessage::Shutdown => {
                    info!("stop requested");
                    break;
                }
            }
        }

        info!("finished pump messages processor");
    }
}

struct IngressShutdownHandle(Option<ClientShutdownHandle>);

impl IngressShutdownHandle {
    async fn shutdown(mut self) {
        if let Some(mut sender) = self.0.take() {
            if let Err(e) = sender.shutdown().await {
                error!("unable to request shutdown for ingress. {}", e);
            }
        }
    }
}

struct Ingress<H> {
    client: MqttClient<H>,
    shutdown_client: Option<ClientShutdownHandle>,
}

impl<H> Ingress<H>
where
    H: EventHandler,
{
    fn new(client: MqttClient<H>, shutdown_client: ClientShutdownHandle) -> Self {
        Self {
            client,
            shutdown_client: Some(shutdown_client),
        }
    }

    fn handle(&mut self) -> IngressShutdownHandle {
        IngressShutdownHandle(self.shutdown_client.take())
    }

    async fn run(mut self) {
        debug!("starting ingress publication processing",);
        self.client.handle_events().await;
    }
}

struct EgressShutdownHandle(Option<oneshot::Sender<()>>);

impl EgressShutdownHandle {
    async fn shutdown(mut self) {
        if let Some(sender) = self.0.take() {
            if sender.send(()).is_err() {
                error!("unable to request shutdown for egress.");
            }
        }
    }
}

struct Egress {
    publish_handle: PublishHandle,
    loader: MessageLoader<WakingMemoryStore>,
    store: PublicationStore<WakingMemoryStore>,
    shutdown_send: Option<oneshot::Sender<()>>,
    shutdown_recv: oneshot::Receiver<()>,
}

impl Egress {
    fn new(
        publish_handle: PublishHandle,
        loader: MessageLoader<WakingMemoryStore>,
        store: PublicationStore<WakingMemoryStore>,
    ) -> Egress {
        let (shutdown_send, shutdown_recv) = oneshot::channel();

        Self {
            publish_handle,
            loader,
            store,
            shutdown_send: Some(shutdown_send),
            shutdown_recv,
        }
    }

    fn handle(&mut self) -> EgressShutdownHandle {
        EgressShutdownHandle(self.shutdown_send.take())
    }

    async fn run(self) {
        let Egress {
            mut publish_handle,
            loader,
            store,
            shutdown_recv,
            ..
        } = self;

        info!("starting egress publication processing");

        let mut shutdown = shutdown_recv.fuse();
        let mut loader = loader.fuse();

        loop {
            select! {
                _ = &mut shutdown => {
                    info!(" received shutdown signal for egress messages");
                    break;
                }
                maybe_publication = loader.try_next() => {
                    debug!("extracted publication from store");

                    if let Ok(Some((key, publication))) = maybe_publication {
                        debug!("publishing {:?}", key);
                        if let Err(e) = publish_handle.publish(publication).await {
                            error!(err = %e, "failed publish");
                        }

                        if let Err(e) = store.remove(key) {
                            error!(err = %e, "failed removing publication from store");
                        }
                    }
                }
            }
        }

        info!("finished egress publication processing");
    }
}
