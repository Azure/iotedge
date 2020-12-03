use async_trait::async_trait;
use mpsc::{Receiver, UnboundedReceiver, UnboundedSender};
use tokio::sync::mpsc;

use mqtt3::{PublishHandle, ReceivedPublication};

use crate::{MessageTesterError, ShutdownHandle};

/// Responsible for receiving publications and taking some action.
#[async_trait]
pub trait MessageHandler {
    /// Starts handling messages sent to the handler
    async fn run(self) -> Result<(), MessageTesterError>;

    /// Sends a publication to be handled by the message handler
    fn publication_sender_handle(&self) -> UnboundedSender<ReceivedPublication>;

    // Get the shutdown handle to stop the run() method
    fn shutdown_handle(&self) -> ShutdownHandle;
}

/// Responsible for receiving publications and reporting result to the TRC.
pub struct ReportResultMessageHandler {}

impl ReportResultMessageHandler {
    pub fn new() -> Self {
        todo!()
    }
}

#[async_trait]
impl MessageHandler for ReportResultMessageHandler {
    async fn run(mut self) -> Result<(), MessageTesterError> {
        todo!()
    }

    fn publication_sender_handle(&self) -> UnboundedSender<ReceivedPublication> {
        todo!()
    }

    fn shutdown_handle(&self) -> ShutdownHandle {
        todo!()
    }
}

/// Responsible for receiving publications and sending them back to the downstream edge.
pub struct RelayingMessageHandler {
    publication_sender: UnboundedSender<ReceivedPublication>,
    publication_receiver: UnboundedReceiver<ReceivedPublication>,
    shutdown_handle: ShutdownHandle,
    shutdown_recv: Receiver<()>,
    publish_handle: PublishHandle,
}

impl RelayingMessageHandler {
    pub fn new(publish_handle: PublishHandle) -> Self {
        let (publication_sender, publication_receiver) =
            mpsc::unbounded_channel::<ReceivedPublication>();
        let (shutdown_send, shutdown_recv) = mpsc::channel::<()>(1);
        let shutdown_handle = ShutdownHandle::new(shutdown_send);

        Self {
            publication_sender,
            publication_receiver,
            shutdown_handle,
            shutdown_recv,
            publish_handle,
        }
    }
}

#[async_trait]
impl MessageHandler for RelayingMessageHandler {
    async fn run(mut self) -> Result<(), MessageTesterError> {
        todo!()
    }

    fn publication_sender_handle(&self) -> UnboundedSender<ReceivedPublication> {
        todo!()
    }

    fn shutdown_handle(&self) -> ShutdownHandle {
        todo!()
    }
}
