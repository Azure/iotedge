use tracing::error;
use tracing::info;
use tracing::warn;
// use mqtt_broker_core::ClientId;
use futures_util::StreamExt;
use mqtt3::proto;
use mqtt_broker::BrokerHandle;
use mqtt_broker::Message;
use mqtt_broker::SystemEvent;
use std::time::Duration;

// TODO: should it be pub
pub struct CommandHandler {
    broker_handle: BrokerHandle,
}

impl CommandHandler {
    pub fn new(broker_handle: BrokerHandle) -> Self {
        CommandHandler { broker_handle }
    }

    pub async fn run(mut self) {
        // TODO: get device id from env
        let client_id = "deviceid/$edgeHub/$broker/$control";
        let username = "";

        let mut client = mqtt3::Client::new(
            Some(client_id.to_string()),
            Some(username.to_string()),
            None,
            move || {
                let password = "";
                Box::pin(async move {
                    let io = tokio::net::TcpStream::connect("127.0.0.1:1883").await; // TODO: read from config or broker
                    io.map(|io| (io, Some(password.to_string())))
                })
            },
            Duration::from_secs(1),
            Duration::from_secs(60),
        );

        let topic_filter = "$edgehub/{}/disconnect".to_string();
        let qos = proto::QoS::AtLeastOnce;
        // TODO: log error with client and topic
        if let Err(_e) = client.subscribe(proto::SubscribeTo { topic_filter, qos }) {
            // TODO: better message
            error!("could not subscribe to command topic")
        } else {
            // TODO: better message
            info!("successfully subscribed to command topic")
        }

        while let Some(event) = client.next().await {
            info!("received data");

            // client.next() produces option of a result
            // TODO: safely handle
            let event = event.unwrap();

            if let mqtt3::Event::Publication(publication) = event {
                let client_id = Self::parse_client_id(publication.topic_name);

                if let Err(e) = self
                    .broker_handle
                    .send(Message::System(SystemEvent::ForceClientDisconnect(
                        client_id.into(),
                    )))
                    .await
                {
                    warn!(message = "failed to signal broker to disconnect client", error=%e);
                }
            }
        }
    }

    // TODO: implement parse
    fn parse_client_id(topic_name: String) -> String {
        topic_name
    }
}
