use std::time::Duration;

use bytes::BufMut;
use bytes::BytesMut;
use futures_util::{
    future::{self, Either},
    pin_mut, StreamExt,
};
use tokio::{
    sync::mpsc::{self, Receiver},
    time,
};
use tracing::error;
use tracing::info;
use uuid::{self, Uuid};

use mqtt3::{
    proto::{Publication, QoS},
    PublishHandle,
};
use trc_client::{MessageTestResult, TrcClient};

use crate::{settings::Settings, ExitedWork, MessageTesterError, ShutdownHandle};

const POST_MESSAGE_WAIT: Duration = Duration::from_secs(60);

/// Responsible for starting to send the messages that will be relayed and
/// tracked by the test module.
pub struct MessageInitiator {
    publish_handle: PublishHandle,
    shutdown_recv: Receiver<()>,
    shutdown_handle: ShutdownHandle,
    reporting_client: TrcClient,
    payload_size: usize,
    messages_to_send: Option<u32>,
    initiate_topic: String,
    message_frequency: Duration,
    batch_id: Uuid,
    tracking_id: String,
    report_source: String,
}

impl MessageInitiator {
    pub fn new(
        publish_handle: PublishHandle,
        reporting_client: TrcClient,
        settings: &Settings,
    ) -> Result<Self, MessageTesterError> {
        let (shutdown_send, shutdown_recv) = mpsc::channel::<()>(1);
        let shutdown_handle = ShutdownHandle(shutdown_send);

        let batch_id = settings
            .batch_id()
            .ok_or(MessageTesterError::MissingBatchId)?;
        let report_source = format!("{}{}", settings.module_name(), ".send");

        Ok(Self {
            publish_handle,
            shutdown_recv,
            shutdown_handle,
            reporting_client,
            payload_size: settings.message_size_in_bytes() as usize,
            messages_to_send: settings.messages_to_send(),
            initiate_topic: settings.initiate_topic()?,
            message_frequency: settings.message_frequency(),
            batch_id,
            tracking_id: settings
                .tracking_id()
                .ok_or(MessageTesterError::MissingTrackingId)?,
            report_source,
        })
    }

    pub async fn run(mut self) -> Result<ExitedWork, MessageTesterError> {
        info!("starting message loop");

        let mut seq_num: u32 = 0;
        let mut publish_handle = self.publish_handle.clone();

        let dummy_data = &vec![b'a'; self.payload_size];
        loop {
            if Some(seq_num) == self.messages_to_send {
                info!("stopping test as we have sent max messages",);
                time::delay_for(POST_MESSAGE_WAIT).await;
                break;
            }

            info!("publishing message {}", seq_num);
            let mut payload = BytesMut::with_capacity(self.payload_size + 4);
            payload.put_u32(seq_num);
            payload.put_u128_le(self.batch_id.to_u128_le());
            payload.put_slice(&dummy_data);
            let publication = Publication {
                topic_name: self.initiate_topic.clone(),
                qos: QoS::ExactlyOnce,
                retain: true,
                payload: payload.into(),
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

            self.report_message_sent(seq_num).await;
            seq_num += 1;

            time::delay_for(self.message_frequency).await;
        }

        Ok(ExitedWork::MessageInitiator)
    }

    pub fn shutdown_handle(&self) -> ShutdownHandle {
        self.shutdown_handle.clone()
    }

    async fn report_message_sent(&self, sequence_number: u32) {
        let result = MessageTestResult::new(
            self.tracking_id.clone(),
            self.batch_id.to_string(),
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
    }
}
