use std::env;
use std::time::Duration;

use futures_util::{future::BoxFuture, StreamExt};
use lazy_static::lazy_static;
use regex::Regex;
use tokio::net::TcpStream;
use tracing::{error, info, warn};

use mqtt3::{proto, Client, Event, IoSource, ShutdownError};
use mqtt_broker::{BrokerHandle, Error, Message, SystemEvent};

// TODO REVIEW: What if client connects with same id as the command handler client? I assume that the same problem exists for the eh-bridge client id, so its fine
// TODO REVIEW: do we want failures making the client to percolate all the way up and blow up broker?
const TOPIC_FILTER: &str = "$edgehub/+/disconnect";
const CLIENT_EXTRACTION_REGEX: &str = r"\$edgehub/(.*)/disconnect";
const DEVICE_ID_ENV: &str = "IOTEDGE_DEVICEID";

#[derive(Debug)]
pub struct ShutdownHandle(mqtt3::ShutdownHandle);

impl ShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), Error> {
        self.0
            .shutdown()
            .await
            .map_err(|e| Error::ShutdownClient(e))?;
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
    pub fn new(broker_handle: BrokerHandle, address: String) -> Self {
        let device_id = match env::var(DEVICE_ID_ENV) {
            Ok(id) => id,
            Err(e) => {
                error!(
                    message = "couldn't find expected device id environment variable",
                    error=%e
                );
                "generic-edge-device".to_string()
            }
        };
        let client_id = format!("{}/$edgeHub/$broker/$control", device_id).to_string();

        let broker_connection = BrokerConnection { address };

        let client = Client::new(
            Some(client_id),
            None,
            None,
            broker_connection,
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

    pub async fn run(mut self) {
        let qos = proto::QoS::AtLeastOnce;
        if let Err(_e) = self.client.subscribe(proto::SubscribeTo {
            topic_filter: TOPIC_FILTER.to_string(),
            qos,
        }) {
            error!(
                "could not subscribe command handler to command topic '{}'",
                TOPIC_FILTER
            )
        } else {
            info!(
                "command handler subscribed to command topic '{}'",
                TOPIC_FILTER
            )
        };

        while let Some(Ok(event)) = self.client.next().await {
            if let Err(e) = self.handle_event(event).await {
                warn!(message = "failed to disconnect client", error = %e);
            }
        }
    }

    async fn handle_event(&mut self, event: Event) -> Result<(), DisconnectClientError> {
        if let Event::Publication(publication) = event {
            let topic_name = publication.topic_name;
            let client_id = parse_client_id(topic_name)?;

            info!("received disconnection request for client {}", client_id);

            if let Err(e) = self
                .broker_handle
                .send(Message::System(SystemEvent::ForceClientDisconnect(
                    client_id.into(),
                )))
                .await
            {
                // TODO REVIEW: I tried to embed this error inside the SignalError then only log potential errors once in the calling function.
                //              This didn't work. If this behavior is desirable we can switch to doing it this way but I will need to fix the PartialEq derive error
                warn!(message = "failed to signal client disconnection", error=%e);
                return Err(DisconnectClientError::SignalError());
            }

            info!("succeeded sending broker signal to disconnect client");
        }

        Ok(())
    }
}

fn parse_client_id(topic_name: String) -> Result<String, DisconnectClientError> {
    lazy_static! {
        static ref REGEX: Regex =
            Regex::new(CLIENT_EXTRACTION_REGEX).expect("failed to create new Regex from pattern");
    }

    let captures = REGEX
        .captures(topic_name.as_ref())
        .ok_or_else(|| DisconnectClientError::RegexFailure())?;

    let value = captures
        .get(1)
        .ok_or_else(|| DisconnectClientError::RegexFailure())?;

    let client_id = value.as_str();
    match client_id {
        "" => Err(DisconnectClientError::NoClientId()),
        id => Ok(id.to_string()),
    }
}

// TODO REVIEW: Are these local errors the correct approach?
#[derive(Debug, PartialEq, thiserror::Error)]
pub enum DisconnectClientError {
    #[error("regex does not match disconnect topic")]
    RegexFailure(),

    #[error("client id not found for client disconnect topic")]
    NoClientId(),

    #[error("failed sending broker signal to disconnect client")]
    SignalError(),
}

#[cfg(test)]
mod tests {
    use crate::command::parse_client_id;
    use crate::command::DisconnectClientError;

    #[tokio::test]
    async fn it_parses_client_id() {
        let client_id: String = "test-client".to_string();
        let topic: String = "$edgehub/test-client/disconnect".to_string();

        let output = parse_client_id(topic).unwrap();

        assert_eq!(output, client_id);
    }

    #[tokio::test]
    async fn it_parses_client_id_with_slash() {
        let client_id: String = "test/client".to_string();
        let topic: String = "$edgehub/test/client/disconnect".to_string();

        let output = parse_client_id(topic).unwrap();

        assert_eq!(output, client_id);
    }

    #[tokio::test]
    async fn it_parses_client_id_with_slash_disconnect_suffix() {
        let client_id: String = "test/disconnect".to_string();
        let topic: String = "$edgehub/test/disconnect/disconnect".to_string();

        let output = parse_client_id(topic).unwrap();

        assert_eq!(output, client_id);
    }

    #[tokio::test]
    async fn it_parses_empty_client_id_returning_none() {
        let topic: String = "$edgehub//disconnect".to_string();

        let output = parse_client_id(topic);

        assert_eq!(output, Err(DisconnectClientError::NoClientId()));
    }

    #[tokio::test]
    async fn it_parses_bad_topic_returning_none() {
        let topic: String = "$edgehub/disconnect/client-id".to_string();

        let output = parse_client_id(topic);

        assert_eq!(output, Err(DisconnectClientError::RegexFailure()));
    }

    #[tokio::test]
    async fn it_parses_empty_topic_returning_none() {
        let topic: String = "".to_string();

        let output = parse_client_id(topic);

        assert_eq!(output, Err(DisconnectClientError::RegexFailure()));
    }
}
