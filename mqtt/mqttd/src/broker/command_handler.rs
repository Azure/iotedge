use futures::future::select;
use futures_util::pin_mut;
use futures_util::StreamExt;
use mqtt3::proto;
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

// TODO: get device id from env
const CLIENT_ID: &str = "deviceid/$edgeHub/$broker/$control";
const TOPIC_FILTER: &str = "$edgehub/{}/disconnect";
const CLIENT_EXTRACTION_REGEX: &str = r"(?<=\$edgehub\/)(.*)(?=\/disconnect)";

enum Event {
    Shutdown,
}

#[derive(Debug)]
pub struct ShutdownHandle(Sender<Event>);

impl ShutdownHandle {
    pub async fn shutdown(&mut self) -> Result<(), Error> {
        self.0
            .send(Event::Shutdown)
            .await
            .map_err(|_| Error::SendSnapshotMessage)?; // TODO: new error type
        Ok(())
    }
}

// TODO: should it be pub
pub struct CommandHandler {
    broker_handle: BrokerHandle,
    sender: Sender<Event>,
    events: Receiver<Event>,
}

impl CommandHandler {
    pub fn new(broker_handle: BrokerHandle) -> Self {
        let (sender, events) = mpsc::channel(5);
        CommandHandler {
            broker_handle,
            sender,
            events,
        }
    }

    pub fn shutdown_handle(&self) -> ShutdownHandle {
        ShutdownHandle(self.sender.clone())
    }

    pub fn run(mut self) -> JoinHandle<()> {
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
        }
        // let event_loop = async {
        //     while let Some(event) = client.next().await {
        //         let event = event.expect("event expected");
        //         match event {
        //             Event::NewConnection { .. } => conn_sender
        //                 .send(event)
        //                 .expect("can't send an event to a conn channel"),
        //             Event::Publication(publication) => pub_sender
        //                 .send(publication)
        //                 .expect("can't send an event to a pub channel"),
        //             Event::SubscriptionUpdates(_) => sub_sender
        //                 .send(event)
        //                 .expect("can't send an event to a sub channel"),
        //         };
        //     }
        // };
        // pin_mut!(event_loop);
        // select(event_loop, tx).await;
        let event_loop_handle = tokio::spawn(async move {
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
            pin_mut!(event_loop);
            select(event_loop, self.events).await;
        });

        event_loop_handle
    }

    fn parse_client_id(topic_name: String) -> Option<String> {
        // TODO: eliminate unwrap
        let re = Regex::new(CLIENT_EXTRACTION_REGEX);

        // TODO: clean up
        // TODO: "disconnect client topic"?
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
