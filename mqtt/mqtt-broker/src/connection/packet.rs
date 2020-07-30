use std::sync::Arc;

use async_trait::async_trait;
use tokio::sync::Semaphore;
use tracing::{debug, warn};

use mqtt3::proto::Packet;

use crate::{ClientEvent, ClientId, Error, Message, Publish};

#[derive(Debug, Clone, Copy, PartialEq)]
pub enum PacketAction<C, S> {
    Continue(C),
    Stop(S),
}

#[async_trait]
pub trait IncomingPacketProcessor {
    async fn process(&mut self, packet: Packet) -> Result<PacketAction<Message, Message>, Error>;
}

#[async_trait]
pub trait OutgoingPacketProcessor {
    async fn process(
        &mut self,
        message: Message,
    ) -> PacketAction<Option<(Packet, Option<Message>)>, ()>;
}

pub struct MqttIncomingPacketProcessor {
    client_id: ClientId,
    incoming_pub_limit: Arc<Semaphore>,
}

pub trait MakeIncomingPacketProcessor {
    type Processor: IncomingPacketProcessor + Send + Sync;

    fn make_incoming(&self, client_id: &ClientId) -> Self::Processor;
}

pub trait MakeOutgoingPacketProcessor {
    type Processor: OutgoingPacketProcessor + Send + Sync;

    fn make_outgoing(&self, client_id: &ClientId) -> Self::Processor;
}

#[derive(Debug, Clone)]
pub struct MakeMqttPacketProcessor;

impl MakeIncomingPacketProcessor for MakeMqttPacketProcessor {
    type Processor = MqttIncomingPacketProcessor;

    fn make_incoming(&self, client_id: &ClientId) -> Self::Processor {
        // We limit the number of incoming publications (PublishFrom) per client
        // in order to avoid (a single) publisher to occupy whole BrokerHandle queue.
        // This helps with QoS 0 messages throughput, due to the fact that outgoing_task
        // also uses sends PubAck0 for QoS 0 messages to BrokerHandle queue.
        let incoming_pub_limit = Arc::new(Semaphore::new(10));

        Self::Processor {
            client_id: client_id.clone(),
            incoming_pub_limit,
        }
    }
}

impl MakeOutgoingPacketProcessor for MakeMqttPacketProcessor {
    type Processor = MqttOutgoingPacketProcessor;

    fn make_outgoing(&self, client_id: &ClientId) -> Self::Processor {
        Self::Processor {
            client_id: client_id.clone(),
        }
    }
}

#[async_trait]
impl IncomingPacketProcessor for MqttIncomingPacketProcessor {
    async fn process(&mut self, packet: Packet) -> Result<PacketAction<Message, Message>, Error> {
        let event = match packet {
            Packet::Connect(_) => {
                // [MQTT-3.1.0-2] - The Server MUST process a second CONNECT Packet
                // sent from a Client as a protocol violation and disconnect the Client.
                warn!("CONNECT packet received on an already established connection, dropping connection due to protocol violation");
                return Err(Error::ProtocolViolation);
            }
            Packet::ConnAck(connack) => ClientEvent::ConnAck(connack),
            Packet::Disconnect(disconnect) => {
                let event = ClientEvent::Disconnect(disconnect);
                let message = Message::Client(self.client_id.clone(), event);
                debug!("disconnect received. shutting down receive side of connection");
                return Ok(PacketAction::Stop(message));
            }
            Packet::PingReq(ping) => ClientEvent::PingReq(ping),
            Packet::PingResp(pingresp) => ClientEvent::PingResp(pingresp),
            Packet::PubAck(puback) => ClientEvent::PubAck(puback),
            Packet::PubComp(pubcomp) => ClientEvent::PubComp(pubcomp),
            Packet::Publish(publish) => {
                let perm = self.incoming_pub_limit.clone().acquire_owned().await;
                ClientEvent::PublishFrom(publish, Some(perm))
            }
            Packet::PubRec(pubrec) => ClientEvent::PubRec(pubrec),
            Packet::PubRel(pubrel) => ClientEvent::PubRel(pubrel),
            Packet::Subscribe(subscribe) => ClientEvent::Subscribe(subscribe),
            Packet::SubAck(suback) => ClientEvent::SubAck(suback),
            Packet::Unsubscribe(unsubscribe) => ClientEvent::Unsubscribe(unsubscribe),
            Packet::UnsubAck(unsuback) => ClientEvent::UnsubAck(unsuback),
        };

        let message = Message::Client(self.client_id.clone(), event);
        Ok(PacketAction::Continue(message))
    }
}

pub struct MqttOutgoingPacketProcessor {
    client_id: ClientId,
}

#[async_trait]
impl OutgoingPacketProcessor for MqttOutgoingPacketProcessor {
    async fn process(
        &mut self,
        message: Message,
    ) -> PacketAction<Option<(Packet, Option<Message>)>, ()> {
        match message {
            Message::Client(_client_id, event) => match event {
                ClientEvent::ConnReq(_) => PacketAction::Continue(None),
                ClientEvent::ConnAck(connack) => {
                    PacketAction::Continue(Some((Packet::ConnAck(connack), None)))
                }
                ClientEvent::Disconnect(_) => {
                    debug!("asked to disconnect. outgoing_task completing...");
                    PacketAction::Stop(())
                }
                ClientEvent::DropConnection => {
                    debug!("asked to drop connection. outgoing_task completing...");
                    PacketAction::Stop(())
                }
                ClientEvent::PingReq(req) => {
                    PacketAction::Continue(Some((Packet::PingReq(req), None)))
                }
                ClientEvent::PingResp(response) => {
                    PacketAction::Continue(Some((Packet::PingResp(response), None)))
                }
                ClientEvent::Subscribe(sub) => {
                    PacketAction::Continue(Some((Packet::Subscribe(sub), None)))
                }
                ClientEvent::SubAck(suback) => {
                    PacketAction::Continue(Some((Packet::SubAck(suback), None)))
                }
                ClientEvent::Unsubscribe(unsub) => {
                    PacketAction::Continue(Some((Packet::Unsubscribe(unsub), None)))
                }
                ClientEvent::UnsubAck(unsuback) => {
                    PacketAction::Continue(Some((Packet::UnsubAck(unsuback), None)))
                }
                ClientEvent::PublishTo(Publish::QoS12(_id, publish)) => {
                    PacketAction::Continue(Some((Packet::Publish(publish), None)))
                }
                ClientEvent::PublishTo(Publish::QoS0(id, publish)) => {
                    let message = Message::Client(self.client_id.clone(), ClientEvent::PubAck0(id));
                    PacketAction::Continue(Some((Packet::Publish(publish), Some(message))))
                }
                ClientEvent::PubAck(puback) => {
                    PacketAction::Continue(Some((Packet::PubAck(puback), None)))
                }
                ClientEvent::PubRec(pubrec) => {
                    PacketAction::Continue(Some((Packet::PubRec(pubrec), None)))
                }
                ClientEvent::PubRel(pubrel) => {
                    PacketAction::Continue(Some((Packet::PubRel(pubrel), None)))
                }
                ClientEvent::PubComp(pubcomp) => {
                    PacketAction::Continue(Some((Packet::PubComp(pubcomp), None)))
                }
                event => {
                    warn!("ignoring event for outgoing_task: {:?}", event);
                    PacketAction::Continue(None)
                }
            },
            Message::System(_event) => PacketAction::Continue(None),
        }
    }
}
