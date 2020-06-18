use std::collections::HashMap;

use tracing::warn;

use mqtt3::proto;
use mqtt_broker_core::ClientInfo;

use crate::{
    snapshot::SessionSnapshot, subscription::Subscription, ClientEvent, ClientId, ConnectionHandle,
    Error, Message, SessionState,
};

#[derive(Debug)]
pub struct ConnectedSession {
    state: SessionState,
    client_info: ClientInfo,
    will: Option<proto::Publication>,
    handle: ConnectionHandle,
}

impl ConnectedSession {
    pub fn new(
        state: SessionState,
        client_info: ClientInfo,
        will: Option<proto::Publication>,
        handle: ConnectionHandle,
    ) -> Self {
        Self {
            state,
            client_info,
            will,
            handle,
        }
    }

    pub fn client_id(&self) -> &ClientId {
        self.state.client_id()
    }

    pub fn client_info(&self) -> &ClientInfo {
        &self.client_info
    }

    pub fn handle(&self) -> &ConnectionHandle {
        &self.handle
    }

    pub fn snapshot(&self) -> SessionSnapshot {
        self.state.clone().into()
    }

    pub fn subscriptions(&self) -> &HashMap<String, Subscription> {
        self.state.subscriptions()
    }

    pub fn into_will(self) -> Option<proto::Publication> {
        self.will
    }

    pub fn into_parts(
        self,
    ) -> (
        SessionState,
        ClientInfo,
        Option<proto::Publication>,
        ConnectionHandle,
    ) {
        (self.state, self.client_info, self.will, self.handle)
    }

    pub fn handle_publish(
        &mut self,
        publish: proto::Publish,
    ) -> Result<(Option<proto::Publication>, Option<ClientEvent>), Error> {
        self.state.handle_publish(publish)
    }

    pub fn handle_puback(&mut self, puback: &proto::PubAck) -> Result<Option<ClientEvent>, Error> {
        self.state.handle_puback(puback)
    }

    pub fn handle_puback0(
        &mut self,
        id: proto::PacketIdentifier,
    ) -> Result<Option<ClientEvent>, Error> {
        self.state.handle_puback0(id)
    }

    pub fn handle_pubrec(&mut self, pubrec: &proto::PubRec) -> Result<Option<ClientEvent>, Error> {
        self.state.handle_pubrec(pubrec)
    }

    pub fn handle_pubrel(
        &mut self,
        pubrel: &proto::PubRel,
    ) -> Result<Option<proto::Publication>, Error> {
        self.state.handle_pubrel(pubrel)
    }

    pub fn handle_pubcomp(
        &mut self,
        pubcomp: &proto::PubComp,
    ) -> Result<Option<ClientEvent>, Error> {
        self.state.handle_pubcomp(pubcomp)
    }

    pub fn publish_to(
        &mut self,
        publication: proto::Publication,
    ) -> Result<Option<ClientEvent>, Error> {
        self.state.publish_to(publication)
    }

    pub fn subscribe_to(
        &mut self,
        subscribe_to: proto::SubscribeTo,
    ) -> Result<(proto::SubAckQos, Option<Subscription>), Error> {
        match subscribe_to.topic_filter.parse() {
            Ok(filter) => {
                let proto::SubscribeTo { topic_filter, qos } = subscribe_to;

                let subscription = Subscription::new(filter, qos);
                self.state
                    .update_subscription(topic_filter, subscription.clone());
                Ok((proto::SubAckQos::Success(qos), Some(subscription)))
            }
            Err(e) => {
                warn!("invalid topic filter {}: {}", subscribe_to.topic_filter, e);
                Ok((proto::SubAckQos::Failure, None))
            }
        }
    }

    pub fn unsubscribe(
        &mut self,
        unsubscribe: &proto::Unsubscribe,
    ) -> Result<proto::UnsubAck, Error> {
        for filter in &unsubscribe.unsubscribe_from {
            self.state.remove_subscription(&filter);
        }

        let unsuback = proto::UnsubAck {
            packet_identifier: unsubscribe.packet_identifier,
        };
        Ok(unsuback)
    }

    pub fn send(&mut self, event: ClientEvent) -> Result<(), Error> {
        let message = Message::Client(self.state.client_id().clone(), event);
        self.handle.send(message)
    }
}
