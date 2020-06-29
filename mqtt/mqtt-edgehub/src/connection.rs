use async_trait::async_trait;

use mqtt3::proto::Packet;
use mqtt_broker::{
    ClientEvent, ClientId, Error, IncomingPacketProcessor, MakeIncomingPacketProcessor,
    MakeMqttPacketProcessor, MakeOutgoingPacketProcessor, Message, OutgoingPacketProcessor,
    PacketAction, Publish, PublishPermit,
};

use crate::topic::translation::{
    translate_incoming_publish, translate_incoming_subscribe, translate_incoming_unsubscribe,
    translate_outgoing_publish,
};

/// MQTT packet processor wrapper. It identifies `IoTHub` topics and converts
/// them into a format internal for Broker-EdgeHub communication.
pub struct EdgeHubPacketProcessor<P> {
    client_id: ClientId,
    inner: P,
}

impl<P> EdgeHubPacketProcessor<P> {
    pub fn new(client_id: impl Into<ClientId>, inner: P) -> Self {
        Self {
            client_id: client_id.into(),
            inner,
        }
    }
}

#[async_trait]
impl<P> IncomingPacketProcessor for EdgeHubPacketProcessor<P>
where
    P: IncomingPacketProcessor + Send,
{
    async fn process(
        &mut self,
        mut packet: Packet,
    ) -> Result<PacketAction<Message, Message>, Error> {
        match &mut packet {
            Packet::Publish(ref mut publish) => {
                translate_incoming_publish(&self.client_id, publish);
            }
            Packet::Subscribe(subscribe) => {
                translate_incoming_subscribe(&self.client_id, subscribe);
            }
            Packet::Unsubscribe(unsubscribe) => {
                translate_incoming_unsubscribe(&self.client_id, unsubscribe);
            }
            _ => (),
        }
        self.inner.process(packet).await
    }
}

#[async_trait]
impl<P> OutgoingPacketProcessor for EdgeHubPacketProcessor<P>
where
    P: OutgoingPacketProcessor + Send,
{
    async fn process(
        &mut self,
        mut message: Message,
    ) -> PacketAction<Option<(Packet, Option<PublishPermit>)>, ()> {
        match &mut message {
            Message::Client(_, ClientEvent::PublishTo(Publish::QoS12(_, publish), _))
            | Message::Client(_, ClientEvent::PublishTo(Publish::QoS0(publish), _)) => {
                translate_outgoing_publish(publish);
            }
            _ => (),
        }
        self.inner.process(message).await
    }
}

/// Creates a wrapper around default MQTT packet processor.
#[derive(Debug, Clone)]
pub struct MakeEdgeHubPacketProcessor<P>(P);

impl Default for MakeEdgeHubPacketProcessor<MakeMqttPacketProcessor> {
    fn default() -> Self {
        Self::new(MakeMqttPacketProcessor)
    }
}

impl<P> MakeEdgeHubPacketProcessor<P> {
    pub fn new(make_processor: P) -> Self {
        Self(make_processor)
    }
}

impl<P> MakeIncomingPacketProcessor for MakeEdgeHubPacketProcessor<P>
where
    P: MakeIncomingPacketProcessor,
{
    type Processor = EdgeHubPacketProcessor<P::Processor>;

    fn make_incoming(&self, client_id: &ClientId) -> Self::Processor {
        let inner = self.0.make_incoming(client_id);
        Self::Processor::new(client_id.clone(), inner)
    }
}

impl<P> MakeOutgoingPacketProcessor for MakeEdgeHubPacketProcessor<P>
where
    P: MakeOutgoingPacketProcessor,
{
    type Processor = EdgeHubPacketProcessor<P::Processor>;

    fn make_outgoing(&self, client_id: &ClientId) -> Self::Processor {
        let inner = self.0.make_outgoing(client_id);
        Self::Processor::new(client_id.clone(), inner)
    }
}
