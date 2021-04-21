use future::{select_all, Either};
use futures_util::{future, stream::StreamExt, stream::TryStreamExt};
use mpsc::UnboundedSender;
use tokio::{
    sync::mpsc::{self, Receiver, Sender},
    time,
};
use tracing::{error, info, info_span};
use tracing_futures::Instrument;

use mqtt3::{
    proto::{QoS, SubscribeTo},
    Client, Event, ReceivedPublication, SubscriptionUpdateEvent, UpdateSubscriptionHandle,
};
use mqtt_broker_tests_util::client;
use mqtt_util::ClientIoSource;
use trc_client::TrcClient;

use crate::{
    message_channel::{
        MessageChannel, MessageHandler, RelayingMessageHandler, ReportResultMessageHandler,
    },
    message_initiator::MessageInitiator,
    settings::{Settings, TestScenario},
    ExitedWork, MessageTesterError, ShutdownHandle,
};

const EDGEHUB_CONTAINER_ADDRESS: &str = "edgeHub:8883";

#[derive(Debug, Clone)]
pub struct MessageTesterShutdownHandle {
    poll_client_shutdown: Sender<()>,
    message_channel_shutdown: ShutdownHandle,
    message_initiator_shutdown: Option<ShutdownHandle>,
}

impl MessageTesterShutdownHandle {
    fn new(
        poll_client_shutdown: Sender<()>,
        message_channel_shutdown: ShutdownHandle,
        message_initiator_shutdown: Option<ShutdownHandle>,
    ) -> Self {
        Self {
            poll_client_shutdown,
            message_channel_shutdown,
            message_initiator_shutdown,
        }
    }

    pub async fn shutdown(mut self, exited: ExitedWork) {
        match exited {
            ExitedWork::NoneOrUnknown => {
                self.shutdown_message_initiator().await;
                self.shutdown_message_channel().await;
                self.shutdown_poll_client().await;
            }
            ExitedWork::MessageChannel => {
                self.shutdown_message_initiator().await;
                self.shutdown_poll_client().await;
            }
            ExitedWork::MessageInitiator => {
                self.shutdown_message_channel().await;
                self.shutdown_poll_client().await;
            }
            ExitedWork::PollClient => {
                self.shutdown_message_initiator().await;
                self.shutdown_message_channel().await;
            }
        }
    }

    async fn shutdown_message_channel(&self) {
        if let Err(e) = self.message_channel_shutdown.clone().shutdown().await {
            error!("couldn't shutdown message channel: {:?}", e);
        }
    }

    async fn shutdown_poll_client(&mut self) {
        if let Err(e) = self.poll_client_shutdown.clone().send(()).await {
            error!("couldn't shutdown client poll: {:?}", e);
        }
    }

    async fn shutdown_message_initiator(&self) {
        if let Some(message_initiator_shutdown) = self.message_initiator_shutdown.clone() {
            if let Err(e) = message_initiator_shutdown.shutdown().await {
                error!("couldn't shutdown message initiator: {:?}", e);
            }
        }
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
    message_channel: MessageChannel<dyn MessageHandler + Send>,
    message_initiator: Option<MessageInitiator>,
    shutdown_handle: MessageTesterShutdownHandle,
    poll_client_shutdown_recv: Receiver<()>,
}

impl MessageTester {
    pub async fn new(settings: Settings) -> Result<Self, MessageTesterError> {
        info!("initializing MessageTester");

        let client = client::create_client_from_module_env(EDGEHUB_CONTAINER_ADDRESS)
            .map_err(MessageTesterError::ParseEnvironment)?;
        let publish_handle = client
            .publish_handle()
            .map_err(MessageTesterError::PublishHandle)?;

        let tracking_id = settings.tracking_id();
        let relay_topic = settings.relay_topic();
        let module_name = settings.module_name();
        let test_result_coordinator_url = settings.trc_url();
        let reporting_client = TrcClient::new(test_result_coordinator_url);

        let message_handler: Box<dyn MessageHandler + Send> = match settings.test_scenario() {
            TestScenario::Initiate => {
                let batch_id = settings
                    .batch_id()
                    .ok_or(MessageTesterError::MissingBatchId)?;
                Box::new(ReportResultMessageHandler::new(
                    reporting_client.clone(),
                    tracking_id,
                    batch_id,
                    &module_name,
                ))
            }
            TestScenario::Relay => Box::new(RelayingMessageHandler::new(
                publish_handle.clone(),
                relay_topic,
            )),
        };
        let message_channel = MessageChannel::new(message_handler);

        let mut message_initiator = None;
        let mut message_initiator_shutdown = None;
        if let TestScenario::Initiate = settings.test_scenario() {
            let initiator = MessageInitiator::new(publish_handle, reporting_client, &settings)?;

            message_initiator_shutdown = Some(initiator.shutdown_handle());
            message_initiator = Some(initiator);
        }

        let (poll_client_shutdown_send, poll_client_shutdown_recv) = mpsc::channel::<()>(1);
        let message_handler_shutdown = message_channel.shutdown_handle();
        let shutdown_handle = MessageTesterShutdownHandle::new(
            poll_client_shutdown_send,
            message_handler_shutdown,
            message_initiator_shutdown,
        );

        info!("finished initializing message tester");
        Ok(Self {
            settings,
            client,
            message_channel,
            message_initiator,
            shutdown_handle,
            poll_client_shutdown_recv,
        })
    }

    pub async fn run(self) -> Result<(), MessageTesterError> {
        // start poll client
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

        // make subscription
        Self::subscribe(client_sub_handle, self.settings.clone()).await?;

        // run message channel
        let message_channel_join = tokio::spawn(
            self.message_channel
                .run()
                .instrument(info_span!("message channel")),
        );

        let mut tasks = vec![message_channel_join, poll_client_join];

        // maybe start message initiator depending on mode
        if let Some(message_initiator) = self.message_initiator {
            info!(
                "waiting for test start delay of {:?}",
                self.settings.test_start_delay()
            );
            time::delay_for(self.settings.test_start_delay()).await;

            let message_loop = tokio::spawn(message_initiator.run());
            tasks.push(message_loop);
        }

        info!("waiting for tasks to exit");
        let (exited, _, join_handles) = select_all(tasks).await;
        match exited {
            Err(e) => {
                error!("Stopping test run because task exited with error: {:?}", e);
                self.shutdown_handle
                    .shutdown(ExitedWork::NoneOrUnknown)
                    .await;
            }
            Ok(Err(e)) => {
                error!("Stopping test run because task exited with error: {:?}", e);
                self.shutdown_handle
                    .shutdown(ExitedWork::NoneOrUnknown)
                    .await;
            }
            Ok(Ok(exited)) => {
                info!(
                    "Stopping test run because {} exited gracefully",
                    exited.to_string()
                );
                self.shutdown_handle.shutdown(exited).await;
            }
        }

        for handle in join_handles {
            handle
                .await
                .map_err(MessageTesterError::WaitForShutdown)??;
        }

        info!("test successfully shutdown all componenets");

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
                    topic_filter: settings.relay_topic(),
                    qos: QoS::AtLeastOnce,
                })
                .await
                .map_err(MessageTesterError::UpdateSubscription)?,
            TestScenario::Relay => client_sub_handle
                .subscribe(SubscribeTo {
                    topic_filter: settings.initiate_topic(),
                    qos: QoS::AtLeastOnce,
                })
                .await
                .map_err(MessageTesterError::UpdateSubscription)?,
        };

        info!("finished subscribing to test topics");
        Ok(())
    }
}

async fn poll_client(
    message_send_handle: UnboundedSender<ReceivedPublication>,
    mut client: Client<ClientIoSource>,
    mut shutdown_recv: Receiver<()>,
) -> Result<ExitedWork, MessageTesterError> {
    info!("starting poll client");
    loop {
        let message_send_handle = message_send_handle.clone();
        let event = client.try_next();
        let shutdown = shutdown_recv.next();

        match future::select(event, shutdown).await {
            Either::Left((event, _)) => {
                if let Ok(Some(event)) = event {
                    process_event(event, &message_send_handle)?;
                }
            }
            Either::Right((shutdown, _)) => {
                info!("received shutdown signal");

                if shutdown.is_none() {
                    error!("shutdown channel was full when shutdown initiated");
                }

                break;
            }
        }
    }

    Ok(ExitedWork::PollClient)
}

fn process_event(
    event: Event,
    message_send_handle: &UnboundedSender<ReceivedPublication>,
) -> Result<(), MessageTesterError> {
    match event {
        Event::NewConnection { .. } => {
            info!("received new connection");
        }
        Event::Publication(publication) => {
            info!("received publication {:?}", publication);
            message_send_handle
                .send(publication)
                .map_err(MessageTesterError::SendPublicationInChannel)?;
        }
        Event::SubscriptionUpdates(sub_updates) => {
            info!("received subscription update {:?}", sub_updates);
            for subscription_update in sub_updates {
                if let SubscriptionUpdateEvent::RejectedByServer(rejection) = subscription_update {
                    error!("received rejected subscription update: {:?}", rejection);
                    return Err(MessageTesterError::RejectedSubscription(
                        rejection.topic_filter,
                    ));
                }
            }
        }
        Event::Disconnected(_) => {
            info!("received disconnect");
        }
    };

    Ok(())
}
