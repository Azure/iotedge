use std::collections::HashSet;
use std::iter::FromIterator;
use std::time::Duration;

use futures_util::future::BoxFuture;
use lazy_static::lazy_static;
use regex::Regex;
use tokio::net::TcpStream;
use tokio::stream::StreamExt;
use tracing::{error, info, warn};

use mqtt3::{
    proto, Client, Event, IoSource, ShutdownError, SubscriptionUpdateEvent, UpdateSubscriptionError,
};
use mqtt_broker::{BrokerHandle, Error, Message, SystemEvent};

const TOPIC_FILTER: &str = "$edgehub/+/disconnect";
const CLIENT_EXTRACTION_REGEX: &str = r"\$edgehub/(.*)/disconnect";

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
        let subscribe_topics = &[TOPIC_FILTER.to_string()];

        if !self.subscribe(subscribe_topics).await? {
            return Err(CommandHandlerError::MissingSubacks(
                subscribe_topics.concat(),
            ));
        }

        while let Some(event) = self
            .client
            .try_next()
            .await
            .map_err(|e| CommandHandlerError::PollClientFailure(e))?
        {
            if let Err(e) = self.handle_event(event).await {
                warn!(message = "failed to disconnect client", error = %e);
            }
        }

        Ok(())
    }

    async fn subscribe(&mut self, topics: &[String]) -> Result<bool, CommandHandlerError> {
        let subscriptions = topics.into_iter().map(|topic| proto::SubscribeTo {
            topic_filter: topic.to_string(),
            qos: proto::QoS::AtLeastOnce,
        });

        for subscription in subscriptions {
            self.client
                .subscribe(subscription)
                .map_err(CommandHandlerError::SubscribeFailure)?;
        }

        let mut subacks: HashSet<String> = HashSet::from_iter(topics.to_vec());

        while let Some(event_or_err) = self
            .client
            .try_next()
            .await
            .map_err(|e| CommandHandlerError::PollClientFailure(e))?
        {
            if let Event::SubscriptionUpdates(subscriptions) = event_or_err {
                for subscription in subscriptions {
                    if let SubscriptionUpdateEvent::Subscribe(sub) = subscription {
                        subacks.remove(&sub.topic_filter);
                    }
                }

                if subacks.is_empty() {
                    return Ok(true);
                }
            }
        }

        Ok(false)
    }

    async fn handle_event(&mut self, event: Event) -> Result<(), HandleDisconnectError> {
        if let Event::Publication(publication) = event {
            let client_id = parse_client_id(&publication.topic_name)?;

            info!("received disconnection request for client {}", client_id);

            if let Err(e) = self
                .broker_handle
                .send(Message::System(SystemEvent::ForceClientDisconnect(
                    client_id.into(),
                )))
                .await
            {
                return Err(HandleDisconnectError::SignalError(e));
            }

            info!("succeeded sending broker signal to disconnect client");
        }

        Ok(())
    }
}

fn parse_client_id(topic_name: &str) -> Result<String, HandleDisconnectError> {
    lazy_static! {
        static ref REGEX: Regex =
            Regex::new(CLIENT_EXTRACTION_REGEX).expect("failed to create new Regex from pattern");
    }

    let captures = REGEX
        .captures(topic_name.as_ref())
        .ok_or_else(|| HandleDisconnectError::RegexFailure)?;

    let value = captures
        .get(1)
        .ok_or_else(|| HandleDisconnectError::RegexFailure)?;

    let client_id = value.as_str();
    match client_id {
        "" => Err(HandleDisconnectError::NoClientId),
        id => Ok(id.to_string()),
    }
}

#[derive(Debug, thiserror::Error)]
pub enum CommandHandlerError {
    #[error("failed to receive expected subacks for command topics: {0}")]
    MissingSubacks(String),

    #[error("failed to subscribe command handler to command topic")]
    SubscribeFailure(#[from] UpdateSubscriptionError),

    #[error("failed to poll client when validating command handler subscriptions")]
    PollClientFailure(#[from] mqtt3::Error),
}

#[derive(Debug, thiserror::Error)]
enum HandleDisconnectError {
    #[error("regex does not match disconnect topic")]
    RegexFailure,

    #[error("client id not found for client disconnect topic")]
    NoClientId,

    #[error("failed sending broker signal to disconnect client")]
    SignalError(#[from] Error),
}

#[cfg(test)]
mod tests {
    use crate::command::parse_client_id;
    use crate::command::HandleDisconnectError;
    use assert_matches::assert_matches;

    #[test]
    fn it_parses_client_id() {
        let client_id = "test-client";
        let topic = "$edgehub/test-client/disconnect";

        let output = parse_client_id(topic).unwrap();

        assert_eq!(output, client_id);
    }

    #[test]
    fn it_parses_client_id_with_slash() {
        let client_id = "test/client";
        let topic = "$edgehub/test/client/disconnect";

        let output = parse_client_id(topic).unwrap();

        assert_eq!(output, client_id);
    }

    #[test]
    fn it_parses_client_id_with_slash_disconnect_suffix() {
        let client_id = "test/disconnect";
        let topic = "$edgehub/test/disconnect/disconnect";

        let output = parse_client_id(topic).unwrap();

        assert_eq!(output, client_id);
    }

    #[test]
    fn it_parses_empty_client_id_returning_none() {
        let topic = "$edgehub//disconnect";

        let output = parse_client_id(topic);

        assert_matches!(output, Err(HandleDisconnectError::NoClientId));
    }

    #[test]
    fn it_parses_bad_topic_returning_none() {
        let topic = "$edgehub/disconnect/client-id";

        let output = parse_client_id(topic);

        assert_matches!(output, Err(HandleDisconnectError::RegexFailure));
    }

    #[test]
    fn it_parses_empty_topic_returning_none() {
        let topic = "";

        let output = parse_client_id(topic);

        assert_matches!(output, Err(HandleDisconnectError::RegexFailure));
    }
}
