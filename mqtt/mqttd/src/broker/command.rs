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
const TOPIC_FILTER: &str = "$edgehub/+/disconnect";
// const CLIENT_EXTRACTION_REGEX: &str = r"(?<=\$edgehub\/)(.*)(?=\/disconnect)";
const CLIENT_EXTRACTION_REGEX: &str = r"\$edgehub\/(.*?)\/disconnect";

#[derive(Debug)]
pub struct ShutdownHandle(mqtt3::ShutdownHandle);

// TODO REVIEW: We need this to map the err in a structured way?
impl ShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), Error> {
        self.0
            .shutdown()
            .await
            .map_err(|e| Error::ShutdownClient(e))?;
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
            let io = TcpStream::connect("127.0.0.1:1882").await; // TODO: read from config or broker
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

    // TODO: move out so we can test
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

// TODO: create integration tests with PacketStream rather than mqtt3

#[cfg(test)]
mod tests {
    // use std::{
    //     env,
    //     time::{Duration as StdDuration, Instant},
    // };

    // use chrono::{Duration, Utc};
    // use mockito::mock;
    // use serde_json::json;

    #[tokio::test]
    async fn it_does_basic_thing() {
        // mock broker handle
        // mock client

        // create command handler

        // test case 1: create
        // test case 2: call run and verify
        //              a) subscribed to a given topic
        //              b) subscribed with a given qos
        // test case 3: connect client to broker, simulate a message on the subscribed topic, and verify
        //              a) ForceClientDisconnection
    }

    // #[tokio::test]
    // async fn it_downloads_server_cert() {
    //     let expiration = Utc::now() + Duration::days(90);
    //     let res = json!(
    //         {
    //             "privateKey": { "type": "key", "bytes": PRIVATE_KEY },
    //             "certificate": CERTIFICATE,
    //             "expiration": expiration.to_rfc3339()
    //         }
    //     );

    //     let _m = mock(
    //         "POST",
    //         "/modules/$edgeHub/genid/12345678/certificate/server?api-version=2019-01-30",
    //     )
    //     .with_status(201)
    //     .with_body(serde_json::to_string(&res).unwrap())
    //     .create();

    //     env::set_var(WORKLOAD_URI, mockito::server_url());
    //     env::set_var(EDGE_DEVICE_HOST_NAME, "localhost");
    //     env::set_var(MODULE_ID, "$edgeHub");
    //     env::set_var(MODULE_GENERATION_ID, "12345678");

    //     let res = download_server_certificate().await;
    //     assert!(res.is_ok());
    // }

    // #[tokio::test]
    // async fn it_schedules_cert_renewal_in_future() {
    //     let now = Instant::now();

    //     let renew_at = Utc::now() + Duration::milliseconds(100);
    //     server_certificate_renewal(renew_at).await;

    //     let elapsed = now.elapsed();
    //     assert!(elapsed > StdDuration::from_millis(100));
    //     assert!(elapsed < StdDuration::from_millis(500));
    // }

    // #[tokio::test]
    // async fn it_does_not_schedule_cert_renewal_in_past() {
    //     let now = Instant::now();

    //     let renew_at = Utc::now();
    //     server_certificate_renewal(renew_at).await;

    //     assert!(now.elapsed() < StdDuration::from_millis(100));
    // }
}
