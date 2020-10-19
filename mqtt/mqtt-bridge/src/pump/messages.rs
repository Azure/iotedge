use async_trait::async_trait;
use futures_util::stream::StreamExt;
use tokio::sync::mpsc;
use tracing::{error, info};

use super::{PumpHandle, PumpMessage};

/// A trait for all custom pump event handlers.
#[async_trait]
pub trait PumpMessageHandler {
    /// A custom pump message event type.
    type Message;

    /// Handles custom pump message event.
    async fn handle(&self, message: Self::Message);
}

/// Handles incoming control messsages for a pump.
pub(crate) struct MessagesProcessor<M>
where
    M: PumpMessageHandler,
{
    messages: mpsc::Receiver<PumpMessage<M::Message>>,
    pump_handle: Option<PumpHandle<M::Message>>,
    handler: M,
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
    ) -> Self {
        Self {
            messages,
            pump_handle: Some(pump_handle),
            handler,
        }
    }

    /// Returns a shutdown handle of message processor.
    pub(crate) fn handle(&mut self) -> MessagesProcessorShutdownHandle<M::Message> {
        MessagesProcessorShutdownHandle(self.pump_handle.take())
    }

    /// Runs control messages processing.
    pub(crate) async fn run(mut self) {
        info!("starting pump messages processor...");
        while let Some(message) = self.messages.next().await {
            match message {
                PumpMessage::Event(event) => self.handler.handle(event).await,
                PumpMessage::ConfigurationUpdate(_) => {}
                PumpMessage::Shutdown => {
                    info!("stop requested");
                    break;
                }
            }
        }

        info!("finished pump messages processor");
    }
}

/// Messages processor shutdown handle.
pub(crate) struct MessagesProcessorShutdownHandle<M>(Option<PumpHandle<M>>);

impl<M> MessagesProcessorShutdownHandle<M> {
    /// Sends a signal to shutdown message processor.
    pub(crate) async fn shutdown(mut self) {
        if let Some(mut sender) = self.0.take() {
            if let Err(e) = sender.send(PumpMessage::Shutdown).await {
                error!("unable to request shutdown for message processor. {}", e);
            }
        }
    }
}
