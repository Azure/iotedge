use std::{collections::HashMap, sync::Arc};

use async_trait::async_trait;

use lazy_static::lazy_static;
use mqtt3::proto::{self, Packet};
use mqtt_broker::{
    BrokerHandle, ClientId, Error, IncomingPacketProcessor, MakePacketProcessor, Message,
    OutgoingPacketProcessor, PacketAction, SystemEvent,
};
use parking_lot::Mutex;
use proto::{PacketIdentifier, Publication};
use regex::Regex;

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
impl<P> OutgoingPacketProcessor for TranslateTopic<P>
where
    P: OutgoingPacketProcessor + Send,
{
    async fn process(
        &mut self,
        message: Message,
    ) -> PacketAction<Option<(Packet, Option<Message>)>, ()> {
        // match &mut message {
        //     Message::Client(_, ClientEvent::PublishTo(Publish::QoS12(_id, publish)))
        //     | Message::Client(_, ClientEvent::PublishTo(Publish::QoS0(_id, publish))) => {
        //         translate_outgoing_publish(publish);
        //     }
        //     _ => (),
        // }
        let mut action = self.inner.process(message).await;

        if let PacketAction::Continue(Some((Packet::Publish(publish), _))) = &mut action {
            translate_outgoing_publish(publish);
        }

        action
    }
}

// struct PublicationDeliveryInner<O, I> {
//     outgoing_inner: O,
//     incoming_inner: I,
//     broker_handle: BrokerHandle,
//     waited_to_be_acked: HashMap<mqtt3::proto::PacketIdentifier, String>,
// }

// pub struct PublicationDelivery<O, I>(Arc<RefCell<PublicationDeliveryInner<O, I>>>);

// impl<O, I> PublicationDelivery<O, I> {
//     pub fn new(broker_handle: BrokerHandle, outgoing_inner: O, incoming_inner: I) -> Self {
//         let inner = PublicationDeliveryInner {
//             outgoing_inner,
//             incoming_inner,
//             broker_handle,
//             waited_to_be_acked: HashMap::new(),
//         };
//         Self(Arc::new(RefCell::new(inner)))
//     }

pub struct PublicationDelivery<P> {
    inner: P,
    broker_handle: BrokerHandle,
    waited_to_be_acked: Arc<Mutex<HashMap<PacketIdentifier, String>>>,
}

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
            Packet::PubRel(pubrel) => pubrel.packet_identifier,
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
    const DEVICE_ID: &str = r"(?P<device_id>[^/]+)";
    const MODULE_ID: &str = r"(?P<module_id>[^/]+)";
    lazy_static! {
        static ref M2M_PUBLISH_PATTERN: Regex = Regex::new(&format!(
            "\\$edgehub/{}/{}/inputs/(?P<path>.+)",
            DEVICE_ID, MODULE_ID
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

    if M2M_PUBLISH_PATTERN.is_match(&topic_name) {
        Some((*packet_identifier, topic_name.clone()))
    } else {
        None
    }

    // dbg!(topic_name);

    // match packet {
    //     Packet::Publish(proto::Publish { packet_identifier_dup_qos, topic_name, .. })
    //         if M2M_PUBLISH_PATTERN.is_match(&topic_name) =>
    //     {
    //         let p =
    //         Some((*packet_identifier, topic_name.clone()))
    //     }
    //     _ => None,
    // }
}

// fn match_m2m_publish(message: &Message) -> Option<(proto::PacketIdentifier, String)> {
//     const DEVICE_ID: &str = r"(?P<device_id>[^/]+)";
//     const MODULE_ID: &str = r"(?P<module_id>[^/]+)";
//     lazy_static! {
//         static ref M2M_PUBLISH_PATTERN: Regex = Regex::new(&format!(
//             "$edgehub/{}/{}/inputs/(?P<path>.*)",
//             DEVICE_ID, MODULE_ID
//         ))
//         .expect("failed to create new Regex from pattern");
//     }

//     dbg!(&message);

//     match message {
//         Message::Client(_, ClientEvent::PublishTo(Publish::QoS12(packet_identifier, publish)))
//             if M2M_PUBLISH_PATTERN.is_match(&publish.topic_name) =>
//         {
//             Some((*packet_identifier, publish.topic_name.clone()))
//         }
//         _ => None,
//     }
// }

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
            self.broker_handle.send(message)?
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
            self.store_packet_info(&packet);
        }

        action
    }
}

/// Creates a wrapper around default MQTT packet processor.
#[derive(Debug, Clone)]
pub struct MakeEdgeHubPacketProcessor<P> {
    broker_handle: BrokerHandle,
    inner: P,
}

impl<P> MakeEdgeHubPacketProcessor<P> {
    pub fn new(broker_handle: BrokerHandle, inner: P) -> Self {
        Self {
            broker_handle,
            inner,
        }
    }
}

impl<P> MakePacketProcessor for MakeEdgeHubPacketProcessor<P>
where
    P: MakePacketProcessor,
{
    type OutgoingProcessor = TranslateTopic<PublicationDelivery<P::OutgoingProcessor>>;

    type IncomingProcessor = TranslateTopic<PublicationDelivery<P::IncomingProcessor>>;

    fn make(&self, client_id: &ClientId) -> (Self::OutgoingProcessor, Self::IncomingProcessor) {
        let waited_to_be_acked = Arc::new(Mutex::new(HashMap::new()));

        let (outgoing_inner, incoming_inner) = self.inner.make(client_id);

        let inner = PublicationDelivery::new(
            self.broker_handle.clone(),
            outgoing_inner,
            waited_to_be_acked.clone(),
        );
        let outgoing = Self::OutgoingProcessor::new(client_id.clone(), inner);

        let inner = PublicationDelivery::new(
            self.broker_handle.clone(),
            incoming_inner,
            waited_to_be_acked,
        );
        let incoming = Self::IncomingProcessor::new(client_id.clone(), inner);

        (outgoing, incoming)
    }
}

// impl<P> MakeIncomingPacketProcessor for MakeEdgeHubPacketProcessor<P>
// where
//     P: MakeIncomingPacketProcessor,
// {
//     type Processor = TranslateTopicProcessor<PublishDeliveryConfirmationProcessor<P::Processor>>;
//     // type Processor = TranslateTopicProcessor<P::Processor>;

//     fn make_incoming(&self, client_id: &ClientId) -> Self::Processor {
//         let inner = self.inner.make_incoming(client_id);
//         let inner = PublishDeliveryConfirmationProcessor::new(self.broker_handle.clone(), inner);
//         Self::Processor::new(client_id.clone(), inner)
//     }
// }

// impl<P> MakeOutgoingPacketProcessor for MakeEdgeHubPacketProcessor<P>
// where
//     P: MakeOutgoingPacketProcessor,
// {
//     type Processor = TranslateTopicProcessor<PublishDeliveryConfirmationProcessor<P::Processor>>;
//     // type Processor = TranslateTopicProcessor<P::Processor>;

//     fn make_outgoing(&self, client_id: &ClientId) -> Self::Processor {
//         let inner = self.inner.make_outgoing(client_id);
//         let inner = PublishDeliveryConfirmationProcessor::new(self.broker_handle.clone(), inner);
//         Self::Processor::new(client_id.clone(), inner)
//     }
// }
