use mpsc::UnboundedSender;
use tokio::{sync::mpsc, task::JoinHandle};

use mqtt3::{PublishHandle, ReceivedPublication};

use crate::{MessageTesterError, ShutdownHandle};

/// Responsible for receiving publications and taking some action.
pub trait MessageHandler {
    /// Starts handling messages sent to the handler
    fn run(self) -> (JoinHandle<Result<(), MessageTesterError>>, ShutdownHandle);

    /// Sends a publication to be handled by the message handler
    fn publication_sender_handle(&self) -> UnboundedSender<ReceivedPublication>;
}

/// Responsible for receiving publications and reporting result to the TRC.
pub struct ReportResultMessageHandler {}

impl ReportResultMessageHandler {
    pub fn new() -> Self {
        todo!()
    }
}

impl MessageHandler for ReportResultMessageHandler {
    fn run(self) -> (JoinHandle<Result<(), MessageTesterError>>, ShutdownHandle) {
        todo!()
    }

    fn publication_sender_handle(&self) -> UnboundedSender<ReceivedPublication> {
        todo!()
    }
}

/// Responsible for receiving publications and sending them back to the downstream edge.
pub struct SendBackMessageHandler {
    publication_sender: UnboundedSender<ReceivedPublication>,
    shutdown_handle: ShutdownHandle,
    publish_handle: PublishHandle,
}

impl SendBackMessageHandler {
    pub fn new(publish_handle: PublishHandle) -> Self {
        let (publication_sender, publication_receiver) =
            mpsc::unbounded_channel::<ReceivedPublication>();
        let (shutdown_send, shutdown_recv) = mpsc::channel::<()>(1);
        let shutdown_handle = ShutdownHandle::new(shutdown_send);

        Self {
            publication_sender,
            shutdown_handle,
            publish_handle,
        }
    }
}

impl MessageHandler for SendBackMessageHandler {
    fn run(self) -> (JoinHandle<Result<(), MessageTesterError>>, ShutdownHandle) {
        todo!()
    }

    fn publication_sender_handle(&self) -> UnboundedSender<ReceivedPublication> {
        todo!()
    }
}
