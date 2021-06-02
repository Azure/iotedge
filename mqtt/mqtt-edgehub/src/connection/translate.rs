use async_trait::async_trait;

use mqtt3::proto::Packet;
use mqtt_broker::{
    ClientId, Error, IncomingPacketProcessor, Message, OutgoingPacketProcessor, PacketAction,
};

use crate::topic::translation::{
    translate_incoming_publish, translate_incoming_subscribe, translate_incoming_unsubscribe,
    translate_outgoing_publish,
};

/// MQTT packet processor wrapper. It identifies `IoTHub` topics and converts
/// them into a format internal for Broker-EdgeHub communication.
pub struct TranslateTopic<P> {
    client_id: ClientId,
    inner: P,
}

impl<P> TranslateTopic<P> {
    pub fn new(client_id: impl Into<ClientId>, inner: P) -> Self {
        Self {
            client_id: client_id.into(),
            inner,
        }
    }
}

#[async_trait]
impl<P> IncomingPacketProcessor for TranslateTopic<P>
where
    P: IncomingPacketProcessor + Send,
{
    async fn process(
        &mut self,
        mut packet: Packet,
    ) -> Result<PacketAction<Message, Message>, Error> {
        translate_incoming(&self.client_id, &mut packet);
        self.inner.process(packet).await
    }
}

#[async_trait]
impl<P> OutgoingPacketProcessor for TranslateTopic<P>
where
    P: OutgoingPacketProcessor + Send,
{
    async fn process(
        &mut self,
        message: Message,
    ) -> PacketAction<Option<(Packet, Option<Message>)>, ()> {
        let mut action = self.inner.process(message).await;

        if let PacketAction::Continue(Some((packet, _))) = &mut action {
            translate_outgoing(packet);
        }

        action
    }
}

fn translate_incoming(client_id: &ClientId, packet: &mut Packet) {
    match packet {
        Packet::Publish(ref mut publish) => {
            translate_incoming_publish(client_id, publish);
        }
        Packet::Subscribe(subscribe) => {
            translate_incoming_subscribe(client_id, subscribe);
        }
        Packet::Unsubscribe(unsubscribe) => {
            translate_incoming_unsubscribe(client_id, unsubscribe);
        }
        _ => (),
    }
}

fn translate_outgoing(packet: &mut Packet) {
    if let Packet::Publish(publish) = packet {
        translate_outgoing_publish(publish);
    }
}
