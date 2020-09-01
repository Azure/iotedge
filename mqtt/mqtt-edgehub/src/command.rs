#![allow(dead_code, unused_variables)]
use std::collections::HashMap;
use std::{collections::HashSet, time::Duration};

use futures_util::future::BoxFuture;
use serde_json::error::Error as SerdeError;
use tokio::{net::TcpStream, stream::StreamExt};
use tracing::{debug, error, info};

use mqtt3::{
    proto, Client, Event, IoSource, ReceivedPublication, ShutdownError, SubscriptionUpdateEvent,
    UpdateSubscriptionError,
};
use mqtt_broker::{BrokerHandle, ClientId, Error, Message, ServiceIdentity, SystemEvent};

const DISCONNECT_TOPIC: &str = "$edgehub/disconnect";
const AUTHORIZED_IDENTITIES_TOPIC: &str = "$internal/identities";

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

struct Command {
    topic: String,
    handle: fn(&mut BrokerHandle, &ReceivedPublication) -> Result<(), HandleEventError>,
}

pub struct CommandHandler {
    broker_handle: BrokerHandle,
    client: Client<BrokerConnection>,
    commands: HashMap<String, Command>,
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

        let commands = Self::init_commands();

        let subscribe_topics = &[
            DISCONNECT_TOPIC.to_string(),
            AUTHORIZED_IDENTITIES_TOPIC.to_string(),
        ];
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
                Some(command) => (command.handle)(&mut self.broker_handle, &publication),
                None => Ok(()),
            };
        }
        Ok(())
    }

    fn init_commands() -> HashMap<String, Command> {
        let mut commands = HashMap::new();
        commands.insert(
            DISCONNECT_TOPIC.to_string(),
            Command {
                topic: DISCONNECT_TOPIC.to_string(),
                handle: handle_disconnect,
            },
        );
        commands.insert(
            AUTHORIZED_IDENTITIES_TOPIC.to_string(),
            Command {
                topic: AUTHORIZED_IDENTITIES_TOPIC.to_string(),
                handle: handle_authorized_identities,
            },
        );
        commands
    }
}

fn handle_disconnect(
    broker_handle: &mut BrokerHandle,
    publication: &ReceivedPublication,
) -> Result<(), HandleEventError> {
    let client_id: ClientId =
        serde_json::from_slice(&publication.payload).map_err(HandleEventError::ParseClientId)?;

    info!("received disconnection request for client {}", client_id);

    if let Err(e) = broker_handle.send(Message::System(SystemEvent::ForceClientDisconnect(
        client_id.clone(),
    ))) {
        return Err(HandleEventError::DisconnectSignal(e));
    }

    info!(
        "succeeded sending broker signal to disconnect client{}",
        client_id
    );
    Ok(())
}

fn handle_authorized_identities(
    broker_handle: &mut BrokerHandle,
    publication: &ReceivedPublication,
) -> Result<(), HandleEventError> {
    let array: Vec<ServiceIdentity> =
        serde_json::from_slice(&publication.payload).map_err(HandleEventError::ParseClientId)?;
    if let Err(e) = broker_handle.send(Message::System(SystemEvent::IdentityScopesUpdate(array))) {
        return Err(HandleEventError::SendAuthorizedIdentitiesToBroker(e));
    }

    info!("succeeded sending authorized identity scopes to broker",);
    Ok(())
}

async fn subscribe(
    client: &mut mqtt3::Client<BrokerConnection>,
    topics: &[String],
) -> Result<(), CommandHandlerError> {
    let subscriptions = topics.iter().map(|topic| proto::SubscribeTo {
        topic_filter: topic.to_string(),
        qos: proto::QoS::AtLeastOnce,
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

#[derive(Debug, thiserror::Error)]
enum HandleEventError {
    #[error("failed to parse client id from message payload")]
    ParseClientId(#[from] SerdeError),

    #[error("failed while sending authorized identities to broker")]
    SendAuthorizedIdentitiesToBroker(Error),

    #[error("failed sending broker signal to disconnect client")]
    DisconnectSignal(Error),
}
