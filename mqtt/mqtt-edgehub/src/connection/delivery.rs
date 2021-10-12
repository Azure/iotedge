use std::{collections::HashMap, sync::Arc};

use async_trait::async_trait;
use lazy_static::lazy_static;
use parking_lot::Mutex;
use regex::Regex;

use mqtt3::proto::{self, Packet, PacketIdentifier, Publication};
use mqtt_broker::{
    BrokerHandle, Error, IncomingPacketProcessor, Message, OutgoingPacketProcessor, PacketAction,
    SystemEvent,
};

pub struct PublicationDelivery<P> {
    inner: P,
    broker_handle: BrokerHandle,
    waited_to_be_acked: Arc<Mutex<HashMap<PacketIdentifier, String>>>,
}

/// MQTT packet processor wrapper. It identifies `IoTHub` M2M outgoing publishes,
/// saves PACKETID to send a confirmation packet back to sender on special topic
/// "$edgehub/delivery".
///
/// It is used as a back pressure mechanism to avoid M2M message loss.
impl<P> PublicationDelivery<P> {
    pub fn new(
        broker_handle: BrokerHandle,
        inner: P,
        waited_to_be_acked: Arc<Mutex<HashMap<PacketIdentifier, String>>>,
    ) -> Self {
        Self {
            inner,
            broker_handle,
            waited_to_be_acked,
        }
    }

    fn prepare_confirmation(
        &mut self,
        packet: &Packet,
    ) -> Result<Option<Publication>, serde_json::Error> {
        const DELIVERY_TOPIC: &str = "$edgehub/delivered";

        let packet_identifier = match &packet {
            Packet::PubAck(puback) => puback.packet_identifier,
            _ => return Ok(None),
        };

        let publication = match self.waited_to_be_acked.lock().remove(&packet_identifier) {
            Some(topic_name) => Some(Publication {
                topic_name: DELIVERY_TOPIC.into(),
                qos: proto::QoS::AtLeastOnce,
                retain: false,
                payload: serde_json::to_string(&topic_name)?.into(),
            }),
            None => None,
        };

        Ok(publication)
    }

    fn store_packet_info(&mut self, packet: &Packet) {
        if let Some((packet_identifier, topic_name)) = match_m2m_publish(packet) {
            self.waited_to_be_acked
                .lock()
                .insert(packet_identifier, topic_name);
        }
    }
}

fn match_m2m_publish(packet: &Packet) -> Option<(proto::PacketIdentifier, String)> {
    const ANYTHING_BUT_SLASH: &str = r"[^/]+";
    lazy_static! {
        static ref M2M_PUBLISH_PATTERN: Regex = Regex::new(&format!(
            "\\$edgehub/{}/{}/{}/inputs/.+",
            ANYTHING_BUT_SLASH, ANYTHING_BUT_SLASH, ANYTHING_BUT_SLASH
        ))
        .expect("failed to create new Regex from pattern");
    }

    let (packet_identifier_dup_qos, topic_name) = match packet {
        Packet::Publish(proto::Publish {
            packet_identifier_dup_qos,
            topic_name,
            ..
        }) => (packet_identifier_dup_qos, topic_name),
        _ => return None,
    };

    let packet_identifier = match packet_identifier_dup_qos {
        proto::PacketIdentifierDupQoS::AtLeastOnce(packet_identifier, _) => packet_identifier,
        _ => return None,
    };

    if M2M_PUBLISH_PATTERN.is_match(topic_name) {
        Some((*packet_identifier, topic_name.clone()))
    } else {
        None
    }
}

#[async_trait]
impl<P> IncomingPacketProcessor for PublicationDelivery<P>
where
    P: IncomingPacketProcessor + Send,
{
    async fn process(&mut self, packet: Packet) -> Result<PacketAction<Message, Message>, Error> {
        if let Some(confirmation) = self
            .prepare_confirmation(&packet)
            .map_err(|e| Error::PacketProcessing(e.into()))?
        {
            let message = Message::System(SystemEvent::Publish(confirmation));
            self.broker_handle.send(message)?;
        }

        self.inner.process(packet).await
    }
}

#[async_trait]
impl<P> OutgoingPacketProcessor for PublicationDelivery<P>
where
    P: OutgoingPacketProcessor + Send,
{
    async fn process(
        &mut self,
        message: Message,
    ) -> PacketAction<Option<(Packet, Option<Message>)>, ()> {
        let action = self.inner.process(message).await;

        if let PacketAction::Continue(Some((packet, _))) = &action {
            self.store_packet_info(packet);
        }

        action
    }
}
