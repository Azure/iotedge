use async_trait::async_trait;
use bytes::Buf;
use futures_util::{
    future::{self, Either},
    pin_mut,
};
use mpsc::{Receiver, UnboundedReceiver, UnboundedSender};
use tokio::sync::mpsc;
use tracing::{error, info, warn};
use uuid::Uuid;

use mqtt3::{
    proto::{Publication, QoS},
    PublishHandle, ReceivedPublication,
};
use trc_client::{MessageTestResult, TrcClient};

use crate::{
    parse_sequence_number, ExitedWork, MessageTesterError, ShutdownHandle, INITIATE_TOPIC_PREFIX,
    RELAY_TOPIC_PREFIX,
};

/// Responsible for receiving publications and taking some action.
#[async_trait]
pub trait MessageHandler {
    /// Starts handling messages sent to the handler
    async fn handle(&mut self, publication: ReceivedPublication) -> Result<(), MessageTesterError>;
}

/// Responsible for receiving publications and reporting result to the Test Result Coordinator.
/// Will filter messages by `batch_id` only if supplied.
pub struct ReportResultMessageHandler {
    reporting_client: TrcClient,
    tracking_id: String,
    report_source: String,
    batch_id: Option<Uuid>,
}

impl ReportResultMessageHandler {
    pub fn new(
        reporting_client: TrcClient,
        tracking_id: String,
        module_name: &str,
        batch_id: Option<Uuid>,
    ) -> Self {
        let report_source = format!("{}{}", module_name, ".receive");
        Self {
            reporting_client,
            tracking_id,
            report_source,
            batch_id,
        }
    }
}

#[async_trait]
impl MessageHandler for ReportResultMessageHandler {
    async fn handle(
        &mut self,
        received_publication: ReceivedPublication,
    ) -> Result<(), MessageTesterError> {
        let sequence_number = parse_sequence_number(&received_publication);
        let received_batch_id =
            Uuid::from_u128_le(received_publication.payload.slice(4..20).get_u128_le());

        // Filter by batch id only if supplied.
        if self.batch_id == None || Some(received_batch_id) == self.batch_id {
            info!(
                "reporting result for publication with sequence number {}",
                sequence_number,
            );
            let result = MessageTestResult::new(
                self.tracking_id.clone(),
                received_batch_id.to_string(),
                sequence_number,
            );

            let test_type = trc_client::TestType::Messages;
            let created_at = chrono::Utc::now();

            if let Err(e) = self
                .reporting_client
                .report_result(self.report_source.clone(), result, test_type, created_at)
                .await
            {
                error!("error reporting result to trc: {:?}", e);
            }
        } else {
            warn!("received publication with non-matching batch id");
        }

        Ok(())
    }
}

/// Responsible for receiving publications on the initiate topic and sending
/// them back on the relay topic.
pub struct RelayingMessageHandler {
    publish_handle: PublishHandle,
}

impl RelayingMessageHandler {
    pub fn new(publish_handle: PublishHandle) -> Self {
        Self { publish_handle }
    }
}

#[async_trait]
impl MessageHandler for RelayingMessageHandler {
    async fn handle(
        &mut self,
        received_publication: ReceivedPublication,
    ) -> Result<(), MessageTesterError> {
        let sequence_number = parse_sequence_number(&received_publication);

        info!(
            "relaying publication on topic {} with sequence number {}",
            received_publication.topic_name, sequence_number,
        );

        let relay_topic = received_publication
            .topic_name
            .replace(INITIATE_TOPIC_PREFIX, RELAY_TOPIC_PREFIX);
        let new_publication = Publication {
            topic_name: relay_topic,
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: received_publication.payload,
        };
        self.publish_handle
            .publish(new_publication)
            .await
            .map_err(MessageTesterError::Publish)?;

        Ok(())
    }
}

/// Serves as a channel between a mqtt client's received publications and a message handler.
/// Exposes a message channel to send messages to a separately running thread that will listen
/// for incoming messages and handle them according to custom message handler.
pub struct MessageChannel<H: ?Sized + MessageHandler + Send> {
    publication_sender: UnboundedSender<ReceivedPublication>,
    publication_receiver: UnboundedReceiver<ReceivedPublication>,
    shutdown_handle: ShutdownHandle,
    shutdown_recv: Receiver<()>,
    message_handler: Box<H>,
}

impl<H> MessageChannel<H>
where
    H: MessageHandler + ?Sized + Send,
{
    pub fn new(message_handler: Box<H>) -> Self {
        let (publication_sender, publication_receiver) =
            mpsc::unbounded_channel::<ReceivedPublication>();
        let (shutdown_send, shutdown_recv) = mpsc::channel::<()>(1);
        let shutdown_handle = ShutdownHandle::new(shutdown_send);

        Self {
            publication_sender,
            publication_receiver,
            shutdown_handle,
            shutdown_recv,
            message_handler,
        }
    }

    pub async fn run(mut self) -> Result<ExitedWork, MessageTesterError> {
        info!("starting message channel");
        loop {
            let received_pub = self.publication_receiver.recv();
            let shutdown_signal = self.shutdown_recv.recv();
            pin_mut!(received_pub, shutdown_signal);

            match future::select(received_pub, shutdown_signal).await {
                Either::Left((received_publication, _)) => {
                    if let Some(received_publication) = received_publication {
                        self.message_handler.handle(received_publication).await?;
                    } else {
                        error!("failed listening for incoming publication");
                        return Err(MessageTesterError::ListenForIncomingPublications);
                    }
                }
                Either::Right((shutdown_signal, _)) => {
                    if shutdown_signal.is_some() {
                        info!("received shutdown signal");
                        return Ok(ExitedWork::MessageChannel);
                    }
                    error!("failed listening for shutdown");
                    return Err(MessageTesterError::ListenForShutdown);
                }
            };
        }
    }

    pub fn send_handle(&self) -> UnboundedSender<ReceivedPublication> {
        self.publication_sender.clone()
    }

    pub fn shutdown_handle(&self) -> ShutdownHandle {
        self.shutdown_handle.clone()
    }
}
