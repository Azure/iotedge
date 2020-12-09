use std::collections::HashMap;

use chrono::{DateTime, Utc};
use tracing::debug;

use mqtt3::proto;

use crate::{
    snapshot::SessionSnapshot, subscription::Subscription, ClientEvent, ClientId, ClientInfo,
    Error, Publish, SessionState,
};

#[derive(Debug)]
pub struct OfflineSession {
    state: SessionState,
    last_active: DateTime<Utc>,
}

impl OfflineSession {
    pub fn new(state: SessionState, last_active: DateTime<Utc>) -> Self {
        Self { state, last_active }
    }

    pub fn client_id(&self) -> &ClientId {
        self.state.client_id()
    }

    pub fn last_client_info(&self) -> &ClientInfo {
        self.state.client_info()
    }

    pub fn snapshot(&self) -> SessionSnapshot {
        self.state.clone().into_snapshot(self.last_active)
    }

    pub fn into_snapshot(self) -> SessionSnapshot {
        self.state.into_snapshot(self.last_active)
    }

    pub fn subscriptions(&self) -> &HashMap<String, Subscription> {
        self.state.subscriptions()
    }

    pub fn last_active(&self) -> DateTime<Utc> {
        self.last_active
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
        let OfflineSession { mut state, .. } = self;

        // Drop all outstanding QoS 0 packets
        if !state.waiting_to_be_acked_qos0_mut().is_empty() {
            debug!("dropping all QoS0 packet");
            state.waiting_to_be_acked_qos0_mut().clear();
        }

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

            debug!("resending QoS12 packet {}", id);
            events.push(ClientEvent::PublishTo(to_publish));
        }

        // Handle the outstanding QoS 2 packets in the second stage of transmission
        for completed in state.waiting_to_be_completed() {
            events.push(ClientEvent::PubRel(proto::PubRel {
                packet_identifier: *completed,
            }));
        }

        // Dequeue any queued messages - up to the max inflight count
        while state.allowed_to_send() {
            match state.waiting_to_be_sent_mut().dequeue() {
                Some(publication) => {
                    debug!("dequeueing a message for {}", state.client_id());
                    let event = state.prepare_to_send(&publication)?;
                    events.push(event);
                }
                None => break,
            }
        }

        Ok((state, events))
    }
}
