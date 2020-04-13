use crate::{broker::BrokerHandle, ClientEvent, Message};
use bytes::Bytes;
use mqtt3::proto;

pub struct Notifier {
    handle: BrokerHandle,
}

impl Notifier {
    pub fn new(handle: BrokerHandle) -> Self {
        Self { handle }
    }

    pub async fn subscription(&mut self, client_id: &str, subscription: &proto::Subscribe) {
        let topics: String = subscription
            .subscribe_to
            .iter()
            .map(|s| s.topic_filter.clone())
            .collect::<Vec<String>>()
            .join(r"\u{0000}");

        let publish = proto::Publish {
            packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce, //no ack
            retain: false,
            topic_name: format!("$sys/subscribe/{}", client_id),
            payload: Bytes::from(topics),
        };

        let message = Message::Client("system".into(), ClientEvent::PublishFrom(publish));

        self.handle.send(message).await.unwrap(); // just for poc
    }
}
