use std::{collections::HashMap, collections::HashSet, error::Error as StdError, time::Duration};

use async_trait::async_trait;
use futures_util::{future::BoxFuture, TryStreamExt};
use tokio::net::TcpStream;
use tracing::{debug, error, info};

use mqtt3::{
    proto, Client, Event, IoSource, ShutdownError, SubscriptionUpdateEvent, UpdateSubscriptionError,
};
use mqtt_broker::sidecar::{Sidecar, SidecarShutdownHandle, SidecarShutdownHandleError};

use crate::command::{Command, DynCommand};

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

/// Shutdown handle for `CommandHandler`
#[derive(Clone, Debug)]
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

/// `CommandHandler` is a, so called, "sidecar" that runs alongside
/// the broker (in a separate task) and subscribes to a certain system
/// topics to receive and dispatch commands.
///
/// For example, see `DisconnectCommand`.
pub struct CommandHandler {
    client: Client<BrokerConnection>,
    commands: HashMap<String, Box<dyn Command<Error = Box<dyn StdError>> + Send>>,
}

impl CommandHandler {
    pub fn add_command<C, E>(&mut self, command: C)
    where
        C: Command<Error = E> + Send + 'static,
        E: StdError + 'static,
    {
        let topic = command.topic().to_string();
        let command = Box::new(DynCommand::from(command));

        self.commands.insert(topic, command);
    }

    pub fn new(address: String, device_id: &str) -> Self {
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
            client,
            commands: HashMap::new(),
        }
    }

    pub fn shutdown_handle(&self) -> Result<ShutdownHandle, ShutdownError> {
        Ok(ShutdownHandle {
            client_shutdown: self.client.shutdown_handle()?,
        })
    }

    async fn handle_event(&mut self, event: Event) -> Result<(), Box<dyn StdError>> {
        if let Event::Publication(publication) = event {
            if let Some(command) = self.commands.get_mut(&publication.topic_name) {
                command.handle(&publication)?;
            }
        }
        Ok(())
    }
}

#[async_trait]
impl Sidecar for CommandHandler {
    fn shutdown_handle(&self) -> Result<SidecarShutdownHandle, SidecarShutdownHandleError> {
        let mut handle = self
            .client
            .shutdown_handle()
            .map_err(|e| SidecarShutdownHandleError(Box::new(e)))?;

        let shutdown = async move {
            if let Err(e) = handle.shutdown().await {
                error!(error = %e, "unable to request shutdown for command handler");
            }
        };

        Ok(SidecarShutdownHandle::new(shutdown))
    }

    async fn run(mut self: Box<Self>) {
        let topics = self.commands.keys().map(Clone::clone).collect();
        // TODO percolate error instead
        if let Err(e) = subscribe(&mut self.client, topics).await {
            error!(error = %e, "unable to subscribe to all required topics");
        }

        info!("starting command handler...");

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
}

async fn subscribe(
    client: &mut Client<BrokerConnection>,
    topics: Vec<String>,
) -> Result<(), CommandHandlerError> {
    debug!("command handler subscribing to topics: {:?}", topics);

    let mut subacks: HashSet<_> = topics.iter().map(ToString::to_string).collect();

    for topic in topics {
        let subscription = proto::SubscribeTo {
            topic_filter: topic,
            qos: proto::QoS::AtLeastOnce,
        };

        client
            .subscribe(subscription)
            .map_err(CommandHandlerError::SubscribeFailure)?;
    }

    while let Some(event) = client
        .try_next()
        .await
        .map_err(CommandHandlerError::PollClientFailure)?
    {
        if let Event::SubscriptionUpdates(subscriptions) = event {
            for subscription in subscriptions {
                match subscription {
                    SubscriptionUpdateEvent::Subscribe(sub) => {
                        subacks.remove(&sub.topic_filter);
                    }
                    SubscriptionUpdateEvent::RejectedByServer(sub) => {
                        return Err(CommandHandlerError::SubscriptionRejectedByServer(
                            sub.topic_filter,
                        ));
                    }
                    SubscriptionUpdateEvent::Unsubscribe(_) => {}
                }
            }

            if subacks.is_empty() {
                debug!("command handler successfully subscribed to disconnect topic");
                return Ok(());
            }
        }
    }

    error!(
        "command handler failed to subscribe to the following topics {:?}",
        subacks
    );
    Err(CommandHandlerError::MissingSubacks(
        subacks.into_iter().collect::<Vec<_>>(),
    ))
}

#[derive(Debug, thiserror::Error)]
pub enum CommandHandlerError {
    #[error("failed to receive expected subacks for command topics: {0:?}")]
    MissingSubacks(Vec<String>),

    #[error("subscription rejected by server: {0:?}")]
    SubscriptionRejectedByServer(String),

    #[error("failed to subscribe command handler to command topic: {0}")]
    SubscribeFailure(#[from] UpdateSubscriptionError),

    #[error("failed to poll client when validating command handler subscriptions: {0}")]
    PollClientFailure(#[from] mqtt3::Error),

    #[error("failed to signal shutdown for command handler: {0}")]
    ShutdownClient(#[from] mqtt3::ShutdownError),
}
