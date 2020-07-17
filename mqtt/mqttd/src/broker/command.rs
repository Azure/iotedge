use core::future::Future;
use futures::future::select;
use futures_util::pin_mut;
use futures_util::StreamExt;
use lazy_static::lazy_static;
use mqtt3::proto;
use mqtt3::IoSource;
// use mqtt3::ShutdownHandle;
use mqtt3::UpdateSubscriptionHandle;
use mqtt_broker::BrokerHandle;
use mqtt_broker::Error;
use mqtt_broker::Message;
use mqtt_broker::SystemEvent;
use regex::Regex;
use std::pin::Pin;
use std::time::Duration;
use tokio::net::TcpStream;
use tokio::sync::mpsc::{self, Receiver, Sender};
use tokio::sync::oneshot;
use tokio::task::JoinHandle;
use tracing::error;
use tracing::info;

// TODO: rename to command
// TODO: get device id from env
const CLIENT_ID: &str = "deviceid/$edgeHub/$broker/$control";
const TOPIC_FILTER: &str = "$edgehub/{}/disconnect";
const CLIENT_EXTRACTION_REGEX: &str = r"(?<=\$edgehub\/)(.*)(?=\/disconnect)";

enum Event {
    Shutdown,
}

#[derive(Debug)]
pub struct ShutdownHandle(Sender<()>);

// TODO: return self.shutdown_handle which is oneshot
impl ShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), Error> {
        self.0
            .send(())
            .await
            .map_err(|_| Error::SendSnapshotMessage)?; // TODO: new error type
        Ok(())
    }
}

pub struct BrokerConnection {}
impl IoSource for BrokerConnection {
    type Io = TcpStream;
    type Error = std::io::Error;
    type Future =
        Pin<Box<dyn Future<Output = Result<(TcpStream, Option<String>), std::io::Error>>>>;

    fn connect(
        &mut self,
    ) -> Pin<Box<dyn Future<Output = Result<(TcpStream, Option<String>), std::io::Error>>>> {
        Box::pin(async move {
            let io = tokio::net::TcpStream::connect("127.0.0.1:1883").await; // TODO: read from config or broker
            io.map(|io| (io, None))
        })
    }
}

pub struct CommandHandler {
    broker_handle: BrokerHandle,
    // client: mqtt3::Client<BrokerConnection>,
    shutdown_handle: Sender<()>,
    shutdown_listen: Receiver<()>,
}

impl CommandHandler {
    pub fn new(broker_handle: BrokerHandle) -> Self {
        let (shutdown_handle, shutdown_listen) = oneshot::channel::<()>();

        CommandHandler {
            broker_handle,
            shutdown_handle,
            shutdown_listen,
        }
    }

    pub fn shutdown_handle(&self) -> ShutdownHandle {
        ShutdownHandle(self.shutdown_handle.clone())
    }

    pub async fn run(mut self) {
        let broker_connection = BrokerConnection {};

        // TODO: move to broker connect
        // TODO: read associated types (implementation of trait determines what types used)
        // TODO: read generics
        let mut client = mqtt3::Client::new(
            Some(CLIENT_ID.to_string()),
            None,
            None,
            broker_connection,
            Duration::from_secs(1),
            Duration::from_secs(60),
        );

        let qos = proto::QoS::AtLeastOnce;
        // TODO: log error with client and topic
        if let Err(_e) = client.subscribe(proto::SubscribeTo {
            topic_filter: TOPIC_FILTER.to_string(),
            qos,
        }) {
            // TODO: better message
            error!("could not subscribe to command topic")
        } else {
            // TODO: better message
            info!("successfully subscribed to command topic")
        };

        let event_loop = async {
            while let Some(event) = client.next().await {
                info!("received data");

                // client.next() produces option of a result
                // TODO: safely handle
                let event = event.unwrap();

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
        };
        // TODO: we don't need to wait on shutdown handle because the client.next() call will exit when the client is shutdown
        pin_mut!(event_loop); // TODO: Do we need
        select(event_loop, self.shutdown_listen).await;

        client.shutdown_handle().unwrap().shutdown(); // TODO: safeley handle
        event_loop.await;
    }

    fn parse_client_id(topic_name: String) -> Option<String> {
        // TODO: eliminate unwrap
        // TODO: create static var for regex
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
