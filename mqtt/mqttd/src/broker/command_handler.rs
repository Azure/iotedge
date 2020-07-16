use futures::future::select;
use futures_util::pin_mut;
use futures_util::StreamExt;
use mqtt3::proto;
use mqtt3::ShutdownHandle;
use mqtt3::UpdateSubscriptionHandle;
use mqtt_broker::BrokerHandle;
use mqtt_broker::Error;
use mqtt_broker::Message;
use mqtt_broker::SystemEvent;
use regex::Regex;
use std::time::Duration;
use tokio::sync::mpsc::{self, Receiver, Sender};
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

// #[derive(Debug)]
// pub struct ShutdownHandle(Sender<Event>);

// // TODO: change to hold mqtt client shutdown handle
// impl ShutdownHandle {
//     pub async fn shutdown(&mut self) -> Result<(), Error> {
//         self.0
//             .send(Event::Shutdown)
//             .await
//             .map_err(|_| Error::SendSnapshotMessage)?; // TODO: new error type
//         Ok(())
//     }
// }

pub struct CommandHandler {
    broker_handle: BrokerHandle,
    // subscription_handle: UpdateSubscriptionHandle,
    // shutdown_handle: ShutdownHandle,
    client: mqtt3::Client<IoS>,
}

impl CommandHandler {
    pub fn new(broker_handle: BrokerHandle) -> Self {
        let mut client = mqtt3::Client::new(
            Some(CLIENT_ID.to_string()),
            None,
            None,
            move || {
                Box::pin(async move {
                    let io = tokio::net::TcpStream::connect("127.0.0.1:1883").await; // TODO: read from config or broker
                    io.map(|io| (io, None))
                })
            },
            Duration::from_secs(1),
            Duration::from_secs(60),
        );

        // TODO: handle error
        // let subscription_handle = client.update_subscription_handle().unwrap();
        // let shutdown_handle = client.shutdown_handle().unwrap();

        CommandHandler {
            broker_handle,
            // subscription_handle,
            // shutdown_handle,
            client,
        }
    }

    pub fn shutdown_handle(&self) -> ShutdownHandle {
        self.shutdown_handle().clone()
    }

    pub async fn run(mut self) {
        let qos = proto::QoS::AtLeastOnce;
        // TODO: log error with client and topic
        if let Err(_e) = self.client.subscribe(proto::SubscribeTo {
            topic_filter: TOPIC_FILTER.to_string(),
            qos,
        }) {
            // TODO: better message
            error!("could not subscribe to command topic")
        } else {
            // TODO: better message
            info!("successfully subscribed to command topic")
        }

        let event_loop = async {
            while let Some(event) = self.client.next().await {
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
        pin_mut!(event_loop);
        select(event_loop, self.client.shutdown_handle()).await;
    }

    fn parse_client_id(topic_name: String) -> Option<String> {
        // TODO: eliminate unwrap
        // TODO: create static var for regex
        let re = Regex::new(CLIENT_EXTRACTION_REGEX);

        // TODO: clean up
        // TODO: look at configuration
        match re {
            Ok(re) => match re.captures(topic_name.as_ref()) {
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
            },
            Err(e) => {
                error!(
                    "could not create regex to parse client id from client disconnect topic. {}",
                    e
                );
                None
            }
        }
    }
}
