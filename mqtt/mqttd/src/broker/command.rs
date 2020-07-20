use std::time::Duration;

use futures_util::{future::BoxFuture, StreamExt};
use lazy_static::lazy_static;
use regex::Regex;
use tokio::net::TcpStream;
use tracing::{error, info};

use mqtt3::{proto, Client, IoSource, ShutdownError};
use mqtt_broker::{BrokerHandle, Error, Message, SystemEvent};

// TODO: get device id from env
const CLIENT_ID: &str = "deviceid/$edgeHub/$broker/$control";
const TOPIC_FILTER: &str = "$edgehub/{}/disconnect";
const CLIENT_EXTRACTION_REGEX: &str = r"(?<=\$edgehub\/)(.*)(?=\/disconnect)";

#[derive(Debug)]
pub struct ShutdownHandle(mqtt3::ShutdownHandle);

// TODO: return self.shutdown_handle which is oneshot
// TODO REVIEW: We need this to map the err in a structured way?
impl ShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), Error> {
        self.0
            .shutdown()
            .await
            .map_err(|_| Error::SendSnapshotMessage)?; // TODO: new error type
        Ok(())
    }
}

pub struct BrokerConnection;
impl IoSource for BrokerConnection {
    type Io = TcpStream;
    type Error = std::io::Error;
    type Future = BoxFuture<'static, Result<(TcpStream, Option<String>), std::io::Error>>;

    fn connect(&mut self) -> Self::Future {
        Box::pin(async move {
            let io = TcpStream::connect("127.0.0.1:1883").await; // TODO: read from config or broker
            io.map(|io| (io, None))
        })
    }
}

pub struct CommandHandler {
    broker_handle: BrokerHandle,
    client: Client<BrokerConnection>,
}

impl CommandHandler {
    pub fn new(broker_handle: BrokerHandle) -> Self {
        let client = mqtt3::Client::new(
            Some(CLIENT_ID.to_string()),
            None,
            None,
            BrokerConnection,
            Duration::from_secs(1),
            Duration::from_secs(60),
        );

        CommandHandler {
            broker_handle,
            client,
        }
    }

    pub fn shutdown_handle(&self) -> Result<ShutdownHandle, ShutdownError> {
        // TODO: handle unwrap
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
                "could not subscribe to command topic '{}' for command client '{}'",
                TOPIC_FILTER, CLIENT_ID
            )
        } else {
            info!(
                "successfully subscribed to command topic '{}' for command client '{}'",
                TOPIC_FILTER, CLIENT_ID
            )
        };

        while let Some(event) = self.client.next().await {
            info!("received data");

            match event {
                Ok(event) => {
                    if let mqtt3::Event::Publication(publication) = event {
                        let client_id = Self::parse_client_id(publication.topic_name);

                        match client_id {
                            Some(client_id) => {
                                if let Err(e) = self
                                    .broker_handle
                                    .send(Message::System(SystemEvent::ForceClientDisconnect(
                                        client_id.into(),
                                    )))
                                    .await
                                {
                                    error!(message = "failed to signal broker to disconnect client", error=%e);
                                }
                            }
                            None => {
                                error!("no client id in disconnect request");
                            }
                        }
                    }
                }
                Err(e) => error!(message = "client read bad event.", error = %e),
            }
        }
    }

    fn parse_client_id(topic_name: String) -> Option<String> {
        lazy_static! {
            static ref REGEX: Regex = Regex::new(CLIENT_EXTRACTION_REGEX)
                .expect("failed to create new Regex from pattern");
        }

        // TODO: clean up
        // TODO: look at configuration
        match REGEX.captures(topic_name.as_ref()) {
            Some(capture) => match capture.get(0) {
                Some(captured_match) => Some(captured_match.as_str().to_string()),
                None => {
                    error!("no client id found for client disconnect topic");
                    None
                }
            },
            None => {
                error!("could not parse client id from client disconnect topic");
                None
            }
        }
    }
}
