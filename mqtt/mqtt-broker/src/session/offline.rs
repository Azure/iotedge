use std::collections::HashMap;

use tracing::debug;

use mqtt3::proto;

use crate::{
    snapshot::SessionSnapshot, subscription::Subscription, ClientEvent, ClientId, Error, Publish,
    SessionState,
};

#[derive(Debug)]
pub struct OfflineSession {
    state: SessionState,
}

impl OfflineSession {
    pub fn new(state: SessionState) -> Self {
        Self { state }
    }

    pub fn client_id(&self) -> &ClientId {
        self.state.client_id()
    }

    pub fn snapshot(&self) -> SessionSnapshot {
        self.state.clone().into()
    }

    pub fn subscriptions(&self) -> &HashMap<String, Subscription> {
        self.state.subscriptions()
    }

    pub fn publish_to(
        &mut self,
        publication: proto::Publication,
    ) -> Result<Option<ClientEvent>, Error> {
        self.state.queue_publish(publication)?;
        Ok(None)
    }

    pub fn into_online(self) -> Result<(SessionState, Vec<ClientEvent>), Error> {
        let mut events = Vec::new();
        let OfflineSession { mut state } = self;

        // Handle the outstanding QoS 1 and QoS 2 packets
        for (id, publish) in state.waiting_to_be_acked() {
            let to_publish = match publish {
                Publish::QoS12(id, p) => {
                    let pidq = match p.packet_identifier_dup_qos {
                        proto::PacketIdentifierDupQoS::AtLeastOnce(id, _) => {
                            proto::PacketIdentifierDupQoS::AtLeastOnce(id, true)
                        }
                        proto::PacketIdentifierDupQoS::ExactlyOnce(id, _) => {
                            proto::PacketIdentifierDupQoS::ExactlyOnce(id, true)
                        }
                        proto::PacketIdentifierDupQoS::AtMostOnce => {
                            proto::PacketIdentifierDupQoS::AtMostOnce
                        }
                    };

                    let mut p1 = p.clone();
                    p1.packet_identifier_dup_qos = pidq;
                    Publish::QoS12(*id, p1)
                }
                _ => publish.clone(),
            };

            let permit = state
                .acquire_publish_permit()
                .expect("number of publish permits must not be less than max_inflight_messages");

            debug!("resending QoS12 packet {}", id);
            events.push(ClientEvent::PublishTo(to_publish, permit));
        }

        // Handle the outstanding QoS 2 packets in the second stage of transmission
        for completed in state.waiting_to_be_completed() {
            events.push(ClientEvent::PubRel(proto::PubRel {
                packet_identifier: *completed,
            }));
        }

        // Dequeue any queued messages - up to the max inflight count
        while let Some(permit) = state.allowed_to_send() {
            match state.waiting_to_be_sent_mut().dequeue() {
                Some(publication) => {
                    debug!("dequeueing a message for {}", state.client_id());
                    let event = state.prepare_to_send(&publication, permit)?;
                    events.push(event);
                }
                None => break,
            }
        }

        Ok((state, events))
    }
}
