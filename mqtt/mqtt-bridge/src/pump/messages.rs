use std::{convert::TryInto, fmt::Debug};

use async_trait::async_trait;
use mockall_double::double;
use mqtt3::{proto::QoS, proto::SubscribeTo};
use tokio::sync::mpsc;
use tracing::{debug, error, info};

use super::{PumpHandle, PumpMessage, TopicMapperUpdates};

#[double]
use crate::client::UpdateSubscriptionHandle;

/// A trait for all custom pump event handlers.
#[async_trait]
pub trait PumpMessageHandler {
    /// A custom pump message event type.
    type Message;

    /// Handles custom pump message event.
    async fn handle(&mut self, message: Self::Message);
}

/// Handles incoming control messsages for a pump.
pub(crate) struct MessagesProcessor<M>
where
    M: PumpMessageHandler,
{
    messages: mpsc::Receiver<PumpMessage<M::Message>>,
    pump_handle: Option<PumpHandle<M::Message>>,
    handler: M,
    subscription_handle: UpdateSubscriptionHandle,
    topic_mappers_updates: TopicMapperUpdates,
}

impl<M> MessagesProcessor<M>
where
    M: PumpMessageHandler,
{
    /// Creates a new instance of message processor.
    pub(crate) fn new(
        handler: M,
        messages: mpsc::Receiver<PumpMessage<M::Message>>,
        pump_handle: PumpHandle<M::Message>,
        subscription_handle: UpdateSubscriptionHandle,
        topic_mappers_updates: TopicMapperUpdates,
    ) -> Self {
        Self {
            messages,
            pump_handle: Some(pump_handle),
            handler,
            subscription_handle,
            topic_mappers_updates,
        }
    }

    /// Returns a shutdown handle of message processor.
    pub(crate) fn handle(&mut self) -> MessagesProcessorShutdownHandle<M::Message> {
        MessagesProcessorShutdownHandle(self.pump_handle.take())
    }

    /// Runs control messages processing.
    pub(crate) async fn run(mut self) -> Result<(), MessageProcessorError> {
        info!("starting pump messages processor...");
        while let Some(message) = self.messages.recv().await {
            match message {
                PumpMessage::Event(event) => self.handler.handle(event).await,
                PumpMessage::ConfigurationUpdate(update) => {
                    let (added, removed) = update.into_parts();
                    debug!(
                        "received updates added: {:?}, removed: {:?}",
                        added, removed
                    );

                    for sub in removed {
                        let subscribe_to = sub.subscribe_to();
                        let unsubscribe_result = self
                            .subscription_handle
                            .unsubscribe(subscribe_to.clone())
                            .await;

                        match unsubscribe_result {
                            Ok(_) => {
                                self.topic_mappers_updates.remove(&subscribe_to);
                            }
                            Err(e) => {
                                error!(
                                    "Failed to send unsubscribe update for {}. {}",
                                    subscribe_to, e
                                );
                            }
                        }
                    }

                    for sub in added {
                        let subscribe_to = sub.subscribe_to();
                        match sub.try_into() {
                            Ok(mapper) => {
                                self.topic_mappers_updates.insert(&subscribe_to, mapper);

                                if let Err(e) = self
                                    .subscription_handle
                                    .subscribe(SubscribeTo {
                                        topic_filter: subscribe_to,
                                        qos: QoS::AtLeastOnce, // TODO: get from config
                                    })
                                    .await
                                {
                                    error!("failed to send subscribe {}", e);
                                }
                            }
                            Err(e) => {
                                error!("topic rule could not be parsed {}. {}", subscribe_to, e)
                            }
                        }
                    }
                }
                PumpMessage::Shutdown => {
                    info!("stop requested");
                    break;
                }
            }
        }

        info!("pump messages processor stopped");
        Ok(())
    }
}

/// Messages processor shutdown handle.
pub(crate) struct MessagesProcessorShutdownHandle<M>(Option<PumpHandle<M>>);

impl<M: Debug + Send + 'static> MessagesProcessorShutdownHandle<M> {
    /// Sends a signal to shutdown message processor.
    pub(crate) async fn shutdown(mut self) {
        if let Some(mut sender) = self.0.take() {
            if let Err(e) = sender.send(PumpMessage::Shutdown).await {
                error!("unable to request shutdown for message processor. {}", e);
            }
        }
    }
}

#[derive(Debug, thiserror::Error)]
#[error("pump messages processor error")]
pub(crate) struct MessageProcessorError;
