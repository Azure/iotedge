use std::env;
use std::time::Duration;

use futures_util::{future::BoxFuture, StreamExt};
use lazy_static::lazy_static;
use regex::Regex;
use tokio::net::TcpStream;
use tracing::{error, info, warn};

use mqtt3::{proto, Client, Event, IoSource, ShutdownError, UpdateSubscriptionError};
use mqtt_broker::{BrokerHandle, Error, Message, SystemEvent};

const TOPIC_FILTER: &str = "$edgehub/+/disconnect";
const CLIENT_EXTRACTION_REGEX: &str = r"\$edgehub/(.*)/disconnect";
const DEVICE_ID_ENV: &str = "IOTEDGE_DEVICEID";

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
    pub fn new(broker_handle: BrokerHandle, address: String) -> Result<Self, InitializationError> {
        let device_id =
            env::var(DEVICE_ID_ENV).map_err(|_| InitializationError::DeviceIdNotFound())?;
        let client_id = format!("{}/$edgeHub/$broker/$control", device_id);

        let broker_connection = BrokerConnection { address };

        let mut client = Client::new(
            Some(client_id),
            None,
            None,
            broker_connection,
            Duration::from_secs(1),
            Duration::from_secs(60),
        );

        let qos = proto::QoS::AtLeastOnce;
        client
            .subscribe(proto::SubscribeTo {
                topic_filter: TOPIC_FILTER.to_string(),
                qos,
            })
            .map_err(InitializationError::SubscribeFailure)?;

        println!("successfully subscribed");

        Ok(CommandHandler {
            broker_handle,
            client,
        })
    }

    pub fn shutdown_handle(&self) -> Result<ShutdownHandle, ShutdownError> {
        self.client
            .shutdown_handle()
            .map_or(Err(ShutdownError::ClientDoesNotExist), |shutdown_handle| {
                Ok(ShutdownHandle(shutdown_handle))
            })
    }

    pub async fn run(mut self) {
        while let Some(Ok(event)) = self.client.next().await {
            if let Err(e) = self.handle_event(event).await {
                warn!(message = "failed to disconnect client", error = %e);
            }
        }
    }

    async fn handle_event(&mut self, event: Event) -> Result<(), DisconnectClientError> {
        if let Event::Publication(publication) = event {
            let topic_name = publication.topic_name.as_ref();
            let client_id = parse_client_id(topic_name)?;

            info!("received disconnection request for client {}", client_id);

            if let Err(e) = self
                .broker_handle
                .send(Message::System(SystemEvent::ForceClientDisconnect(
                    client_id.into(),
                )))
                .await
            {
                return Err(DisconnectClientError::SignalError(e));
            }

            info!("succeeded sending broker signal to disconnect client");
        }

        Ok(())
    }
}

fn parse_client_id(topic_name: &str) -> Result<String, DisconnectClientError> {
    lazy_static! {
        static ref REGEX: Regex =
            Regex::new(CLIENT_EXTRACTION_REGEX).expect("failed to create new Regex from pattern");
    }

    let captures = REGEX
        .captures(topic_name.as_ref())
        .ok_or_else(DisconnectClientError::RegexFailure)?;

    let value = captures
        .get(1)
        .ok_or_else(DisconnectClientError::RegexFailure)?;

    let client_id = value.as_str();
    match client_id {
        "" => Err(DisconnectClientError::NoClientId()),
        id => Ok(id.to_string()),
    }
}

#[derive(Debug, thiserror::Error)]
pub enum InitializationError {
    #[error("failed to find expected device id environment variable")]
    DeviceIdNotFound(),

    #[error("failed to subscribe command handler to command topic")]
    SubscribeFailure(#[from] UpdateSubscriptionError),
}

#[derive(Debug, thiserror::Error)]
enum DisconnectClientError {
    #[error("regex does not match disconnect topic")]
    RegexFailure(),

    #[error("client id not found for client disconnect topic")]
    NoClientId(),

    #[error("failed sending broker signal to disconnect client")]
    SignalError(#[from] Error),
}

#[cfg(test)]
mod tests {
    use crate::command::parse_client_id;
    use crate::command::DisconnectClientError;
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

        assert_matches!(output, Err(DisconnectClientError::NoClientId()));
    }

    #[test]
    fn it_parses_bad_topic_returning_none() {
        let topic = "$edgehub/disconnect/client-id";

        let output = parse_client_id(topic);

        assert_matches!(output, Err(DisconnectClientError::RegexFailure()));
    }

    #[test]
    fn it_parses_empty_topic_returning_none() {
        let topic = "";

        let output = parse_client_id(topic);

        assert_matches!(output, Err(DisconnectClientError::RegexFailure()));
    }
}
