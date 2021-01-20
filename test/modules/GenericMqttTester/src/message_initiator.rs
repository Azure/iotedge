use std::time::Duration;

use bytes::Bytes;
use futures_util::{
    future::{self, Either},
    pin_mut, StreamExt,
};
use mqtt3::{
    proto::{Publication, QoS},
    PublishHandle,
};
use tokio::{
    sync::mpsc::{self, Receiver},
    time,
};
use tracing::info;
use trc_client::{MessageTestResult, TrcClient};

use crate::{MessageTesterError, ShutdownHandle, FORWARDS_TOPIC, SEND_SOURCE};

/// Responsible for starting to send the messages that will be relayed and
/// tracked by the test module.
pub struct MessageInitiator {
    publish_handle: PublishHandle,
    shutdown_recv: Receiver<()>,
    shutdown_handle: ShutdownHandle,
    tracking_id: String,
    batch_id: String,
    reporting_client: TrcClient,
    message_frequency: Duration,
}

impl MessageInitiator {
    pub fn new(
        publish_handle: PublishHandle,
        tracking_id: String,
        batch_id: String,
        reporting_client: TrcClient,
        message_frequency: Duration,
    ) -> Self {
        let (shutdown_send, shutdown_recv) = mpsc::channel::<()>(1);
        let shutdown_handle = ShutdownHandle(shutdown_send);

        Self {
            publish_handle,
            shutdown_recv,
            shutdown_handle,
            tracking_id,
            batch_id,
            reporting_client,
            message_frequency,
        }
    }

    pub async fn run(mut self) -> Result<(), MessageTesterError> {
        info!("starting message loop");

        let mut seq_num: u32 = 0;
        let mut publish_handle = self.publish_handle.clone();
        loop {
            info!("publishing message {} to upstream broker", seq_num);
            let publication = Publication {
                topic_name: FORWARDS_TOPIC.to_string(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: Bytes::from(seq_num.to_string()),
            };

            let shutdown_recv_fut = self.shutdown_recv.next();
            let publish_fut = publish_handle.publish(publication);
            pin_mut!(publish_fut);

            match future::select(shutdown_recv_fut, publish_fut).await {
                Either::Left((shutdown, _)) => {
                    info!("received shutdown signal");
                    shutdown.ok_or(MessageTesterError::ListenForShutdown)?;
                    break;
                }
                Either::Right((publish, _)) => {
                    publish.map_err(MessageTesterError::Publish)?;
                }
            };

            self.report_message_sent(seq_num).await?;
            seq_num += 1;

            time::delay_for(self.message_frequency).await;
        }

        Ok(())
    }

    pub fn shutdown_handle(&self) -> ShutdownHandle {
        self.shutdown_handle.clone()
    }

    async fn report_message_sent(&self, sequence_number: u32) -> Result<(), MessageTesterError> {
        let result = MessageTestResult::new(
            self.tracking_id.clone(),
            self.batch_id.clone(),
            sequence_number,
        );

        let test_type = trc_client::TestType::Messages;
        let created_at = chrono::Utc::now();
        self.reporting_client
            .report_result(SEND_SOURCE.to_string(), result, test_type, created_at)
            .await
            .map_err(MessageTesterError::ReportResult)?;

        Ok(())
    }
}
