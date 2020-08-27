use std::{collections::HashSet, time::Duration};

use futures_util::future::BoxFuture;
use serde_json::error::Error as SerdeError;
use tokio::{net::TcpStream, stream::StreamExt};
use tracing::{debug, error, info, warn};

use mqtt3::{
    proto, Client, Event, IoSource, ShutdownError, SubscriptionUpdateEvent, UpdateSubscriptionError,
};
use mqtt_broker::{BrokerHandle, ClientId, Error, Message, SystemEvent};

const DISCONNECT_TOPIC: &str = "$edgehub/disconnect";

#[derive(Debug)]
pub struct ShutdownHandle(mqtt3::ShutdownHandle);

impl ShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), Error> {
        self.0.shutdown().await.map_err(Error::ShutdownClient)?;
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
}

impl CommandHandler {
    pub fn new(broker_handle: BrokerHandle, address: String, device_id: &str) -> Self {
        let client_id = format!("{}/$edgeHub/$broker", device_id);

        let client = Client::new(
            Some(client_id),
            None,
            None,
            BrokerConnection { address },
            Duration::from_secs(1),
            Duration::from_secs(60),
        );

        CommandHandler {
            broker_handle,
            client,
        }
    }

    pub fn shutdown_handle(&self) -> Result<ShutdownHandle, ShutdownError> {
        self.client
            .shutdown_handle()
            .map_or(Err(ShutdownError::ClientDoesNotExist), |shutdown_handle| {
                Ok(ShutdownHandle(shutdown_handle))
            })
    }

    pub async fn run(mut self) -> Result<(), CommandHandlerError> {
        debug!("starting command handler");
        let subscribe_topics = &[DISCONNECT_TOPIC.to_string()];

        self.subscribe(subscribe_topics).await?;

        while let Some(event) = self
            .client
            .try_next()
            .await
            .map_err(CommandHandlerError::PollClientFailure)?
        {
            if let Err(e) = self.handle_event(event).await {
                warn!(message = "error processing command handler event", error = %e);
            }
        }

        debug!("command handler disconnected");

        Ok(())
    }

    async fn subscribe(&mut self, topics: &[String]) -> Result<(), CommandHandlerError> {
        debug!("command handler subscribing to disconnect topic");
        let subscriptions = topics.iter().map(|topic| proto::SubscribeTo {
            topic_filter: topic.to_string(),
            qos: proto::QoS::AtLeastOnce,
        });

        for subscription in subscriptions {
            self.client
                .subscribe(subscription)
                .map_err(CommandHandlerError::SubscribeFailure)?;
        }

        let mut subacks: HashSet<_> = topics.iter().map(Clone::clone).collect();

        while let Some(event) = self
            .client
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

#[derive(Debug, thiserror::Error)]
pub enum CommandHandlerError {
    #[error("failed to receive expected subacks for command topics: {0:?}")]
    MissingSubacks(Vec<String>),

    #[error("failed to subscribe command handler to command topic")]
    SubscribeFailure(#[from] UpdateSubscriptionError),

    #[error("failed to poll client when validating command handler subscriptions")]
    PollClientFailure(#[from] mqtt3::Error),
}

#[derive(Debug, thiserror::Error)]
enum HandleDisconnectError {
    #[error("failed to parse client id from message payload")]
    ParseClientId(#[from] SerdeError),

    #[error("failed sending broker signal to disconnect client")]
    SignalError(#[from] Error),
}
