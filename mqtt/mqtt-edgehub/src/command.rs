use std::{collections::HashSet, time::Duration};

use futures_util::future::BoxFuture;
use serde_json::error::Error as SerdeError;
use tokio::{
    net::TcpStream,
    stream::StreamExt,
    sync::mpsc::{self, Receiver, Sender},
};
use tracing::{debug, error, info};

use mqtt3::{
    proto, Client, Event, IoSource, ShutdownError, SubscriptionUpdateEvent, UpdateSubscriptionError,
};
use mqtt_broker::{BrokerHandle, ClientId, Error, Message, SystemEvent};

const DISCONNECT_TOPIC: &str = "$edgehub/disconnect";

pub struct ShutdownHandle {
    command_handler_shutdown: Sender<()>,
    client_shutdown: mqtt3::ShutdownHandle,
}

impl ShutdownHandle {
    pub async fn shutdown(mut self) -> Result<(), CommandHandlerError> {
        debug!("signaling command handler shutdown");
        self.command_handler_shutdown
            .send(())
            .await
            .map_err(|_| CommandHandlerError::ShutdownError())?;

        debug!("shutting down command handler mqtt client");
        self.client_shutdown
            .shutdown()
            .await
            .map_err(CommandHandlerError::ShutdownClient)?;

        Ok(())
    }
}

pub struct BrokerConnection {
    address: String,
}

impl IoSource for BrokerConnection {
    type Io = TcpStream;
    type Error = std::io::Error;
    type Future = BoxFuture<'static, Result<(TcpStream, Option<String>), std::io::Error>>;

    fn connect(&mut self) -> Self::Future {
        let address = self.address.clone();
        Box::pin(async move {
            let io = TcpStream::connect(address).await;
            io.map(|io| (io, None))
        })
    }
}

pub struct CommandHandler {
    broker_handle: BrokerHandle,
    client: Client<BrokerConnection>,
    termination_handle: Sender<()>,
    termination_receiver: Receiver<()>,
}

impl CommandHandler {
    pub async fn new(
        broker_handle: BrokerHandle,
        address: String,
        device_id: &str,
    ) -> Result<Self, CommandHandlerError> {
        let client_id = format!("{}/$edgeHub/$broker", device_id);

        let mut client = Client::new(
            Some(client_id),
            None,
            None,
            BrokerConnection { address },
            Duration::from_secs(1),
            Duration::from_secs(60),
        );

        let subscribe_topics = &[DISCONNECT_TOPIC.to_string()];
        subscribe(&mut client, subscribe_topics).await?;

        let (termination_handle, termination_receiver) = mpsc::channel(5);

        Ok(CommandHandler {
            broker_handle,
            client,
            termination_handle,
            termination_receiver,
        })
    }

    pub fn shutdown_handle(&self) -> Result<ShutdownHandle, ShutdownError> {
        Ok(ShutdownHandle {
            command_handler_shutdown: self.termination_handle.clone(),
            client_shutdown: self.client.shutdown_handle()?,
        })
    }

    pub async fn run(mut self) {
        debug!("starting command handler");

        loop {
            match self.client.try_next().await {
                Ok(Some(event)) => {
                    if let Err(e) = self.handle_event(event).await {
                        error!(message = "error processing command handler event", error = %e);
                    }
                }
                Ok(None) => {
                    error!("command handler client disconnected");

                    if let Ok(()) = self.termination_receiver.try_recv() {
                        break;
                    }
                }
                Err(e) => error!("failure polling command handler client {}", error = e),
            }
        }

        debug!("command handler stopped");
    }

    async fn handle_event(&mut self, event: Event) -> Result<(), HandleDisconnectError> {
        if let Event::Publication(publication) = event {
            let client_id: ClientId = serde_json::from_slice(&publication.payload)
                .map_err(HandleDisconnectError::ParseClientId)?;

            info!("received disconnection request for client {}", client_id);

            if let Err(e) =
                self.broker_handle
                    .send(Message::System(SystemEvent::ForceClientDisconnect(
                        client_id.clone(),
                    )))
            {
                return Err(HandleDisconnectError::SignalError(e));
            }

            info!(
                "succeeded sending broker signal to disconnect client{}",
                client_id
            );
        }

        Ok(())
    }
}

async fn subscribe(
    client: &mut mqtt3::Client<BrokerConnection>,
    topics: &[String],
) -> Result<(), CommandHandlerError> {
    debug!("command handler subscribing to disconnect topic");
    let subscriptions = topics.iter().map(|topic| proto::SubscribeTo {
        topic_filter: topic.to_string(),
        qos: proto::QoS::AtLeastOnce,
    });

    for subscription in subscriptions {
        client
            .subscribe(subscription)
            .map_err(CommandHandlerError::SubscribeFailure)?;
    }

    let mut subacks: HashSet<_> = topics.iter().map(Clone::clone).collect();

    while let Some(event) = client
        .try_next()
        .await
        .map_err(CommandHandlerError::PollClientFailure)?
    {
        if let Event::SubscriptionUpdates(subscriptions) = event {
            for subscription in subscriptions {
                if let SubscriptionUpdateEvent::Subscribe(sub) = subscription {
                    subacks.remove(&sub.topic_filter);
                }
            }

            if subacks.is_empty() {
                debug!("command handler successfully subscribed to disconnect topic");
                return Ok(());
            }
        }
    }

    error!("command handler failed to subscribe to disconnect topic");
    Err(CommandHandlerError::MissingSubacks(
        subacks.into_iter().collect::<Vec<_>>(),
    ))
}

#[derive(Debug, thiserror::Error)]
pub enum CommandHandlerError {
    #[error("failed to receive expected subacks for command topics: {0:?}")]
    MissingSubacks(Vec<String>),

    #[error("failed to subscribe command handler to command topic")]
    SubscribeFailure(#[from] UpdateSubscriptionError),

    #[error("failed to poll client when validating command handler subscriptions")]
    PollClientFailure(#[from] mqtt3::Error),

    #[error("failed to signal shutdown for command handler")]
    ShutdownClient(#[from] mqtt3::ShutdownError),

    #[error("failed to signal shutdown for command handler")]
    ShutdownError(),
}

#[derive(Debug, thiserror::Error)]
enum HandleDisconnectError {
    #[error("failed to parse client id from message payload")]
    ParseClientId(#[from] SerdeError),

    #[error("failed sending broker signal to disconnect client")]
    SignalError(#[from] Error),
}
