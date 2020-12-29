use bytes::Bytes;
use future::{select_all, Either};
use futures_util::{future, pin_mut, stream::StreamExt, stream::TryStreamExt};
use mpsc::UnboundedSender;
use time::Duration;
use tokio::{
    sync::mpsc::{self, Receiver, Sender},
    time,
};
use tracing::{info, info_span};
use tracing_futures::Instrument;

use mqtt3::{
    proto::{Publication, QoS, SubscribeTo},
    Client, Event, PublishHandle, ReceivedPublication, UpdateSubscriptionHandle,
};
use mqtt_broker_tests_util::client;
use mqtt_util::client_io::ClientIoSource;
use trc_client::TrcClient;

use crate::{
    message_channel::{
        MessageChannel, MessageHandler, RelayingMessageHandler, ReportResultMessageHandler,
    },
    settings::{Settings, TestScenario},
    MessageTesterError, BACKWARDS_TOPIC, FORWARDS_TOPIC,
};

const EDGEHUB_CONTAINER_ADDRESS: &str = "edgeHub:8883";

#[derive(Debug, Clone)]
pub struct MessageTesterShutdownHandle {
    poll_client_shutdown: Sender<()>,
    send_messages_shutdown: Sender<()>,
}

impl MessageTesterShutdownHandle {
    fn new(poll_client_shutdown: Sender<()>, send_messages_shutdown: Sender<()>) -> Self {
        Self {
            poll_client_shutdown,
            send_messages_shutdown,
        }
    }

    pub async fn shutdown(mut self) -> Result<(), MessageTesterError> {
        self.poll_client_shutdown
            .send(())
            .await
            .map_err(MessageTesterError::SendShutdownSignal)?;
        self.send_messages_shutdown
            .send(())
            .await
            .map_err(MessageTesterError::SendShutdownSignal)?;
        Ok(())
    }
}

/// Abstracts the test logic for this generic mqtt telemetry test module.
/// This module is designed to test generic (non-iothub) mqtt telemetry in both a single-node and nested environment.
/// The module will run in one of two modes. The behavior depends on this mode.
///
/// 1: Initiate mode
/// - If nested scenario, test module runs on the lowest node in the topology.
/// - Spawn a thread that publishes messages continuously to upstream edge.
/// - Receives same message routed back from upstream edge and reports the result to the Test Result Coordinator test module.
///
/// 2: Relay mode
/// - If nested scenario, test module runs on the middle node in the topology.
/// - Receives a message from downstream edge and relays it back to downstream edge.
pub struct MessageTester {
    settings: Settings,
    client: Client<ClientIoSource>,
    publish_handle: PublishHandle,
    message_channel: MessageChannel<dyn MessageHandler + Send>,
    shutdown_handle: MessageTesterShutdownHandle,
    poll_client_shutdown_recv: Receiver<()>,
    message_loop_shutdown_recv: Receiver<()>,
}

impl MessageTester {
    pub async fn new(settings: Settings) -> Result<Self, MessageTesterError> {
        info!("initializing MessageTester");

        let client = client::create_client_from_module_env(EDGEHUB_CONTAINER_ADDRESS)
            .map_err(MessageTesterError::ParseEnvironment)?;
        let publish_handle = client
            .publish_handle()
            .map_err(MessageTesterError::PublishHandle)?;

        let test_result_coordinator_url = settings.test_result_coordinator_url().to_string();
        let reporting_client = TrcClient::new(test_result_coordinator_url);
        let message_handler: Box<dyn MessageHandler + Send> = match settings.test_scenario() {
            TestScenario::Initiate => Box::new(ReportResultMessageHandler::new(reporting_client)),
            TestScenario::Relay => Box::new(RelayingMessageHandler::new(publish_handle.clone())),
        };
        let message_channel = MessageChannel::new(message_handler);

        let (poll_client_shutdown_send, poll_client_shutdown_recv) = mpsc::channel::<()>(1);
        let (message_loop_shutdown_send, message_loop_shutdown_recv) = mpsc::channel::<()>(1);
        let shutdown_handle =
            MessageTesterShutdownHandle::new(poll_client_shutdown_send, message_loop_shutdown_send);

        info!("finished initializing message tester");
        Ok(Self {
            settings,
            client,
            publish_handle,
            message_channel,
            shutdown_handle,
            poll_client_shutdown_recv,
            message_loop_shutdown_recv,
        })
    }

    pub async fn run(self) -> Result<(), MessageTesterError> {
        // start poll client and make subs
        let client_sub_handle = self
            .client
            .update_subscription_handle()
            .map_err(MessageTesterError::UpdateSubscriptionHandle)?;
        let message_send_handle = self.message_channel.message_channel();
        let poll_client_join = tokio::spawn(
            poll_client(
                message_send_handle,
                self.client,
                self.poll_client_shutdown_recv,
            )
            .instrument(info_span!("client")),
        );
        Self::subscribe(client_sub_handle, self.settings.clone()).await?;

        // run message channel
        let message_channel_shutdown = self.message_channel.shutdown_handle();
        let message_channel_join = tokio::spawn(
            self.message_channel
                .run()
                .instrument(info_span!("message channel")),
        );

        let mut tasks = vec![message_channel_join, poll_client_join];

        // maybe start message loop depending on mode
        if let TestScenario::Initiate = self.settings.test_scenario() {
            let message_loop = tokio::spawn(
                send_initial_messages(self.publish_handle.clone(), self.message_loop_shutdown_recv)
                    .instrument(info_span!("initiation message loop")),
            );

            tasks.push(message_loop);
        }

        info!("waiting for tasks to exit");
        let (exited, _, join_handles) = select_all(tasks).await;
        exited.map_err(MessageTesterError::WaitForShutdown)??;
        message_channel_shutdown.shutdown().await?;
        for handle in join_handles {
            handle
                .await
                .map_err(MessageTesterError::WaitForShutdown)??;
        }

        Ok(())
    }

    pub fn shutdown_handle(&self) -> MessageTesterShutdownHandle {
        self.shutdown_handle.clone()
    }

    async fn subscribe(
        mut client_sub_handle: UpdateSubscriptionHandle,
        settings: Settings,
    ) -> Result<(), MessageTesterError> {
        info!("subscribing to test topics");
        match settings.test_scenario() {
            TestScenario::Initiate => client_sub_handle
                .subscribe(SubscribeTo {
                    topic_filter: BACKWARDS_TOPIC.to_string(),
                    qos: QoS::AtLeastOnce,
                })
                .await
                .map_err(MessageTesterError::UpdateSubscription)?,
            TestScenario::Relay => client_sub_handle
                .subscribe(SubscribeTo {
                    topic_filter: FORWARDS_TOPIC.to_string(),
                    qos: QoS::AtLeastOnce,
                })
                .await
                .map_err(MessageTesterError::UpdateSubscription)?,
        };

        info!("finished subscribing to test topics");
        Ok(())
    }
}

async fn send_initial_messages(
    mut publish_handle: PublishHandle,
    mut shutdown_recv: Receiver<()>,
) -> Result<(), MessageTesterError> {
    info!("starting message loop");

    let mut seq_num: u32 = 0;
    loop {
        info!("publishing message {} to upstream broker", seq_num);
        let publication = Publication {
            topic_name: "forwards/1".to_string(),
            qos: QoS::ExactlyOnce,
            retain: true,
            payload: Bytes::from(seq_num.to_string()),
        };

        let shutdown_recv_fut = shutdown_recv.next();
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

        time::delay_for(Duration::from_secs(1)).await;

        seq_num += 1;
    }

    Ok(())
}

async fn poll_client(
    message_send_handle: UnboundedSender<ReceivedPublication>,
    mut client: Client<ClientIoSource>,
    mut shutdown_recv: Receiver<()>,
) -> Result<(), MessageTesterError> {
    info!("starting poll client");
    loop {
        let message_send_handle = message_send_handle.clone();
        let event = client.try_next();
        let shutdown = shutdown_recv.next();
        match future::select(event, shutdown).await {
            Either::Left((event, _)) => {
                if let Ok(Some(event)) = event {
                    process_event(event, message_send_handle)?;
                }
            }
            Either::Right((shutdown, _)) => {
                break;
            }
        }
    }

    Ok(())
}

fn process_event(
    event: Event,
    message_send_handle: UnboundedSender<ReceivedPublication>,
) -> Result<(), MessageTesterError> {
    match event {
        Event::NewConnection { .. } => {
            info!("received new connection");
        }
        Event::Publication(publication) => {
            info!("received publication");
            message_send_handle
                .send(publication)
                .map_err(MessageTesterError::SendPublicationInChannel)?;
        }
        Event::SubscriptionUpdates(sub) => {
            info!("received subscription update {:?}", sub);
        }
        Event::Disconnected(_) => {
            info!("received disconnect");
        }
    };

    Ok(())
}
