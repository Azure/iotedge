use mqtt3::proto::QoS::AtLeastOnce;
use std::collections::HashMap;
use std::{collections::HashSet, time::Duration};

use futures_util::future::BoxFuture;
use tokio::{net::TcpStream, stream::StreamExt};
use tracing::{debug, error};

use crate::command::{Command, HandleEventError};
use mqtt3::{
    proto, Client, Event, IoSource, ShutdownError, SubscriptionUpdateEvent, UpdateSubscriptionError,
};
use mqtt_broker::BrokerHandle;

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

pub struct ShutdownHandle {
    client_shutdown: mqtt3::ShutdownHandle,
}

impl ShutdownHandle {
    pub async fn shutdown(mut self) -> Result<(), CommandHandlerError> {
        debug!("signaling command handler shutdown");
        self.client_shutdown
            .shutdown()
            .await
            .map_err(CommandHandlerError::ShutdownClient)?;

        Ok(())
    }
}

pub struct CommandHandler {
    broker_handle: BrokerHandle,
    client: Client<BrokerConnection>,
    commands: HashMap<String, Box<dyn Command + Send>>,
}

impl CommandHandler {
    pub async fn new(
        broker_handle: BrokerHandle,
        address: String,
        device_id: &str,
        commands: HashMap<String, Box<dyn Command + Send>>,
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

        let subscribe_topics = commands.keys().cloned().collect::<Vec<_>>();
        subscribe(&mut client, subscribe_topics).await?;

        Ok(CommandHandler {
            broker_handle,
            client,
            commands,
        })
    }

    pub fn shutdown_handle(&self) -> Result<ShutdownHandle, ShutdownError> {
        Ok(ShutdownHandle {
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
                    debug!("command handler mqtt client disconnected");
                    break;
                }
                Err(e) => {
                    error!("failure polling command handler client {}", error = e);
                }
            }
        }

        debug!("command handler stopped");
    }

    async fn handle_event(&mut self, event: Event) -> Result<(), HandleEventError> {
        if let Event::Publication(publication) = event {
            return match self.commands.get(&publication.topic_name) {
                Some(command) => (*command).handle(&mut self.broker_handle, &publication),
                None => Ok(()),
            };
        }
        Ok(())
    }
}

async fn subscribe(
    client: &mut mqtt3::Client<BrokerConnection>,
    topics: Vec<String>,
) -> Result<(), CommandHandlerError> {
    let subscriptions = topics.iter().map(|topic| proto::SubscribeTo {
        topic_filter: topic.to_string(),
        qos: AtLeastOnce,
    });
    debug!(
        "command handler subscribing to topics: {}",
        topics.join(", ")
    );

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
}
