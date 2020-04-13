use crate::broker::BrokerHandle;
use crate::{ClientEvent, Message};
use bytes::Bytes;
use mqtt3::proto;

pub async fn notify_incoming(event: &ClientEvent, handle: &mut BrokerHandle, client_id: &str) {
    match event {
        ClientEvent::Subscribe(s) => subscribe(s, handle, client_id).await,
        ClientEvent::Disconnect(_) => (), // TODO add
        _ => (),
    }
}

async fn subscribe(subscribe: &proto::Subscribe, handle: &mut BrokerHandle, client_id: &str) {
    let topics: String = subscribe
        .subscribe_to
        .iter()
        .map(|s| s.topic_filter.clone())
        .collect::<Vec<String>>()
        .join(",");

    let publish = proto::Publish {
        packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce, //no ack
        retain: false,
        topic_name: format!("$sys/subscribe/{}", client_id),
        payload: Bytes::from(topics),
    };

    let message = Message::Client("system".into(), ClientEvent::PublishFrom(publish));

    handle.send(message).await.unwrap(); // just for poc
}
