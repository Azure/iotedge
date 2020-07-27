use crate::{BrokerHandle, ClientEvent, Error, Message};
use async_trait::async_trait;
use mqtt3::proto::Packet;
use mqtt_broker_core::ClientId;
#[cfg(feature = "edgehub")]
#[allow(unused_imports)]
use mqtt_edgehub::topic::translation::{
    translate_incoming_publish, translate_incoming_subscribe, translate_incoming_unsubscribe,
    translate_outgoing_publish,
};
use std::sync::Arc;
use tokio::sync::Semaphore;
use tracing::{debug, warn};

pub struct EdgeHubPacketProcessor<P> {
    inner: P,
    client_id: ClientId,
}

#[async_trait]
impl<P> InPacketProcessor for EdgeHubPacketProcessor<P>
where
    P: InPacketProcessor + Send,
{
    async fn process(
        &mut self,
        mut packet: Packet,
        limits: &Arc<Semaphore>,
    ) -> Result<Processed, Error> {
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
        self.inner.process(packet, limits).await
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub enum Processed {
    Continue,
    Stop,
}

#[async_trait]
pub trait InPacketProcessor {
    async fn process(
        &mut self,
        packet: Packet,
        limits: &Arc<Semaphore>,
    ) -> Result<Processed, Error>;
}

#[async_trait]
pub trait OutPacketProcessor {
    async fn process(&mut self, message: Message) -> Result<Option<ClientEvent>, Error>;
}

pub struct MqttPacketProcessor {
    client_id: ClientId,
    broker: BrokerHandle,
}

pub trait MakePacketProcessor {
    type Processor: InPacketProcessor + Send + Sync;

    fn make_incoming(&self, client_id: &ClientId, broker: &BrokerHandle) -> Self::Processor;
    // fn make_outgoing(client_id: impl Into<ClientId>, broker: BrokerHandle) -> O;
}

#[derive(Debug, Clone)]
pub struct MakeMqttPacketProcessor;

impl MakePacketProcessor for MakeMqttPacketProcessor {
    type Processor = MqttPacketProcessor;

    fn make_incoming(&self, client_id: &ClientId, broker_handle: &BrokerHandle) -> Self::Processor {
        Self::Processor {
            client_id: client_id.clone(),
            broker: broker_handle.clone(),
        }
    }
}

#[derive(Debug, Clone)]
pub struct MakeEdgeHubPacketProcessor;

impl MakePacketProcessor for MakeEdgeHubPacketProcessor {
    type Processor = EdgeHubPacketProcessor<MqttPacketProcessor>;

    fn make_incoming(&self, client_id: &ClientId, broker_handle: &BrokerHandle) -> Self::Processor {
        Self::Processor {
            client_id: client_id.clone(),
            inner: MqttPacketProcessor {
                client_id: client_id.clone(),
                broker: broker_handle.clone(),
            },
        }
    }
}

#[async_trait]
impl InPacketProcessor for MqttPacketProcessor {
    async fn process(
        &mut self,
        packet: Packet,
        limits: &Arc<Semaphore>,
    ) -> Result<Processed, Error> {
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
                self.broker.send(message).await?;
                debug!("disconnect received. shutting down receive side of connection");
                return Ok(Processed::Stop);
            }
            Packet::PingReq(ping) => ClientEvent::PingReq(ping),
            Packet::PingResp(pingresp) => ClientEvent::PingResp(pingresp),
            Packet::PubAck(puback) => ClientEvent::PubAck(puback),
            Packet::PubComp(pubcomp) => ClientEvent::PubComp(pubcomp),
            Packet::Publish(publish) => {
                let perm = limits.clone().acquire_owned().await;
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
        self.broker.send(message).await?;
        Ok(Processed::Continue)
    }
}

#[async_trait]
impl OutPacketProcessor for MqttPacketProcessor {
    async fn process(&mut self, _message: Message) -> Result<Option<ClientEvent>, Error> {
        todo!()
    }
}
