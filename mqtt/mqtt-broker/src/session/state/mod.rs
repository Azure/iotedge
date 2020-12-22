mod map;
mod queue;
mod set;

use map::SmallIndexMap;
use queue::BoundedQueue;
use set::SmallIndexSet;

use std::{cmp, collections::HashMap};

use chrono::{DateTime, Utc};
use tracing::{debug, info};

use mqtt3::proto;

use crate::{
    session::identifiers::PacketIdentifiers, snapshot::SessionSnapshot, subscription::Subscription,
    ClientEvent, ClientId, ClientInfo, Error, Publish, SessionConfig,
};

/// Common data and functions for broker sessions.
#[derive(Clone, Debug, PartialEq)]
pub struct SessionState {
    client_info: ClientInfo,
    subscriptions: HashMap<String, Subscription>,
    packet_identifiers: PacketIdentifiers,
    packet_identifiers_qos0: PacketIdentifiers,

    waiting_to_be_sent: BoundedQueue,

    // for incoming messages - QoS2
    waiting_to_be_released: SmallIndexMap<proto::PacketIdentifier, proto::Publish>,

    // for outgoing messages - all QoS
    waiting_to_be_acked: SmallIndexMap<proto::PacketIdentifier, Publish>,
    waiting_to_be_acked_qos0: SmallIndexMap<proto::PacketIdentifier, Publish>,
    waiting_to_be_completed: SmallIndexSet<proto::PacketIdentifier>,
    config: SessionConfig,
}

impl SessionState {
    pub fn new(client_info: ClientInfo, config: SessionConfig) -> Self {
        Self {
            client_info,
            subscriptions: HashMap::new(),
            packet_identifiers: PacketIdentifiers::default(),
            packet_identifiers_qos0: PacketIdentifiers::default(),

            waiting_to_be_sent: BoundedQueue::new(
                config.max_queued_messages(),
                config.max_queued_size(),
                config.when_full(),
            ),
            waiting_to_be_acked: SmallIndexMap::new(),
            waiting_to_be_acked_qos0: SmallIndexMap::new(),
            waiting_to_be_released: SmallIndexMap::new(),
            waiting_to_be_completed: SmallIndexSet::new(),
            config,
        }
    }

    pub fn from_snapshot(
        snapshot: SessionSnapshot,
        config: SessionConfig,
    ) -> (Self, DateTime<Utc>) {
        let (client_info, subscriptions, queued_publications, last_active) = snapshot.into_parts();

        let mut waiting_to_be_sent = BoundedQueue::new(
            config.max_queued_messages(),
            config.max_queued_size(),
            config.when_full(),
        );
        waiting_to_be_sent.extend(queued_publications);

        (
            Self {
                client_info,
                subscriptions,
                packet_identifiers: PacketIdentifiers::default(),
                waiting_to_be_sent,
                waiting_to_be_acked: SmallIndexMap::new(),
                waiting_to_be_released: SmallIndexMap::new(),
                waiting_to_be_completed: SmallIndexSet::new(),
                waiting_to_be_acked_qos0: SmallIndexMap::new(),
                packet_identifiers_qos0: PacketIdentifiers::default(),
                config,
            },
            last_active,
        )
    }

    pub fn into_snapshot(self, last_active: DateTime<Utc>) -> SessionSnapshot {
        SessionSnapshot::from_parts(
            self.client_info,
            self.subscriptions,
            self.waiting_to_be_sent.into_inner(),
            last_active,
        )
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_info.client_id
    }

    pub fn client_info(&self) -> &ClientInfo {
        &self.client_info
    }

    pub fn set_client_info(&mut self, client_info: ClientInfo) {
        self.client_info = client_info;
    }

    pub fn subscriptions(&self) -> &HashMap<String, Subscription> {
        &self.subscriptions
    }

    pub(super) fn waiting_to_be_acked(&self) -> &SmallIndexMap<proto::PacketIdentifier, Publish> {
        &self.waiting_to_be_acked
    }

    pub(super) fn waiting_to_be_acked_qos0_mut(
        &mut self,
    ) -> &mut SmallIndexMap<proto::PacketIdentifier, Publish> {
        &mut self.waiting_to_be_acked_qos0
    }

    pub(super) fn waiting_to_be_completed(&self) -> &SmallIndexSet<proto::PacketIdentifier> {
        &self.waiting_to_be_completed
    }

    pub(super) fn waiting_to_be_sent_mut(&mut self) -> &mut BoundedQueue {
        &mut self.waiting_to_be_sent
    }

    pub fn update_subscription(
        &mut self,
        topic_filter: String,
        subscription: Subscription,
    ) -> Option<Subscription> {
        self.subscriptions.insert(topic_filter, subscription)
    }

    pub fn remove_subscription(&mut self, topic_filter: &str) -> Option<Subscription> {
        self.subscriptions.remove(topic_filter)
    }

    pub fn queue_publish(&mut self, publication: proto::Publication) -> Result<(), Error> {
        if let Some(publication) = self.filter(publication) {
            if let Some(limit) = self.waiting_to_be_sent.enqueue(publication) {
                let dropped = limit.publication();
                info!("{}. drop publication {}", limit, dropped.topic_name);
                debug!("dropped publication {:?}", dropped)
            }
        }
        Ok(())
    }

    /// Takes a publication and returns an optional Publish packet if sending is allowed.
    /// This can return None if the current outstanding messages is at its limit.
    pub fn publish_to(
        &mut self,
        publication: proto::Publication,
    ) -> Result<Option<ClientEvent>, Error> {
        if let Some(publication) = self.filter(publication) {
            if self.allowed_to_send() {
                let event = self.prepare_to_send(&publication)?;
                Ok(Some(event))
            } else {
                if let Some(limit) = self.waiting_to_be_sent.enqueue(publication) {
                    let dropped = limit.publication();
                    info!("{}. drop publication {}", limit, dropped.topic_name);
                    debug!("dropped publication {:?}", dropped)
                }
                Ok(None)
            }
        } else {
            Ok(None)
        }
    }

    pub fn handle_publish(
        &mut self,
        publish: proto::Publish,
    ) -> Result<(Option<proto::Publication>, Option<ClientEvent>), Error> {
        let result = match publish.packet_identifier_dup_qos {
            proto::PacketIdentifierDupQoS::AtMostOnce => {
                let publication = proto::Publication {
                    topic_name: publish.topic_name,
                    qos: proto::QoS::AtMostOnce,
                    retain: publish.retain,
                    payload: publish.payload,
                };
                (Some(publication), None)
            }
            proto::PacketIdentifierDupQoS::AtLeastOnce(packet_identifier, _dup) => {
                let publication = proto::Publication {
                    topic_name: publish.topic_name,
                    qos: proto::QoS::AtLeastOnce,
                    retain: publish.retain,
                    payload: publish.payload,
                };
                let puback = proto::PubAck { packet_identifier };
                let event = ClientEvent::PubAck(puback);
                (Some(publication), Some(event))
            }
            proto::PacketIdentifierDupQoS::ExactlyOnce(packet_identifier, _dup) => {
                self.waiting_to_be_released
                    .insert(packet_identifier, publish);
                let pubrec = proto::PubRec { packet_identifier };
                let event = ClientEvent::PubRec(pubrec);
                (None, Some(event))
            }
        };
        Ok(result)
    }

    pub fn handle_pubrec(&mut self, pubrec: &proto::PubRec) -> Result<Option<ClientEvent>, Error> {
        self.waiting_to_be_acked.remove(&pubrec.packet_identifier);
        self.waiting_to_be_completed
            .insert(pubrec.packet_identifier);
        let pubrel = proto::PubRel {
            packet_identifier: pubrec.packet_identifier,
        };
        Ok(Some(ClientEvent::PubRel(pubrel)))
    }

    pub fn handle_pubrel(
        &mut self,
        pubrel: &proto::PubRel,
    ) -> Result<Option<proto::Publication>, Error> {
        let publication = self
            .waiting_to_be_released
            .remove(&pubrel.packet_identifier)
            .map(|publish| proto::Publication {
                topic_name: publish.topic_name,
                qos: proto::QoS::ExactlyOnce,
                retain: publish.retain,
                payload: publish.payload,
            });
        Ok(publication)
    }

    pub fn handle_pubcomp(
        &mut self,
        pubcomp: &proto::PubComp,
    ) -> Result<Option<ClientEvent>, Error> {
        self.waiting_to_be_completed
            .remove(&pubcomp.packet_identifier);
        self.packet_identifiers.discard(pubcomp.packet_identifier);
        self.try_publish()
    }

    pub fn handle_puback(&mut self, puback: &proto::PubAck) -> Result<Option<ClientEvent>, Error> {
        debug!("discarding packet identifier {}", puback.packet_identifier);
        self.waiting_to_be_acked.remove(&puback.packet_identifier);
        self.packet_identifiers.discard(puback.packet_identifier);
        self.try_publish()
    }

    pub fn handle_puback0(
        &mut self,
        id: proto::PacketIdentifier,
    ) -> Result<Option<ClientEvent>, Error> {
        debug!("discarding QoS 0 packet identifier {}", id);
        self.waiting_to_be_acked_qos0.remove(&id);
        self.packet_identifiers_qos0.discard(id);
        self.try_publish()
    }

    fn try_publish(&mut self) -> Result<Option<ClientEvent>, Error> {
        if self.allowed_to_send() {
            if let Some(publication) = self.waiting_to_be_sent.dequeue() {
                let event = self.prepare_to_send(&publication)?;
                return Ok(Some(event));
            }
        }
        Ok(None)
    }

    pub(super) fn allowed_to_send(&self) -> bool {
        match self.config.max_inflight_messages() {
            Some(limit) => {
                let num_inflight = self.waiting_to_be_acked.len()
                    + self.waiting_to_be_acked_qos0.len()
                    + self.waiting_to_be_completed.len();
                num_inflight < limit.get()
            }
            None => true,
        }
    }

    fn filter(&self, mut publication: proto::Publication) -> Option<proto::Publication> {
        self.subscriptions
            .values()
            .filter(|sub| sub.filter().matches(&publication.topic_name))
            .fold(None, |acc, sub| {
                acc.map(|qos| cmp::max(qos, cmp::min(*sub.max_qos(), publication.qos)))
                    .or_else(|| Some(cmp::min(*sub.max_qos(), publication.qos)))
            })
            .map(move |qos| {
                publication.qos = qos;
                publication
            })
    }

    pub fn prepare_to_send(
        &mut self,
        publication: &proto::Publication,
    ) -> Result<ClientEvent, Error> {
        let publish = match publication.qos {
            proto::QoS::AtMostOnce => {
                let id = self.packet_identifiers_qos0.reserve()?;
                let packet = proto::Publish {
                    packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
                    retain: publication.retain,
                    topic_name: publication.topic_name.to_owned(),
                    payload: publication.payload.to_owned(),
                };
                Publish::QoS0(id, packet)
            }
            proto::QoS::AtLeastOnce => {
                let id = self.packet_identifiers.reserve()?;
                let packet = proto::Publish {
                    packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtLeastOnce(
                        id, false,
                    ),
                    retain: publication.retain,
                    topic_name: publication.topic_name.to_owned(),
                    payload: publication.payload.to_owned(),
                };
                Publish::QoS12(id, packet)
            }
            proto::QoS::ExactlyOnce => {
                let id = self.packet_identifiers.reserve()?;
                let packet = proto::Publish {
                    packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::ExactlyOnce(
                        id, false,
                    ),
                    retain: publication.retain,
                    topic_name: publication.topic_name.to_owned(),
                    payload: publication.payload.to_owned(),
                };
                Publish::QoS12(id, packet)
            }
        };

        let event = match publish {
            Publish::QoS0(id, publish) => {
                self.waiting_to_be_acked_qos0
                    .insert(id, Publish::QoS0(id, publish.clone()));
                ClientEvent::PublishTo(Publish::QoS0(id, publish))
            }
            Publish::QoS12(id, publish) => {
                self.waiting_to_be_acked
                    .insert(id, Publish::QoS12(id, publish.clone()));
                ClientEvent::PublishTo(Publish::QoS12(id, publish))
            }
        };
        Ok(event)
    }
}

#[cfg(test)]
mod tests {
    use std::{net::IpAddr, net::Ipv4Addr, net::SocketAddr, time::Duration};

    use bytes::Bytes;
    use matches::assert_matches;

    use mqtt3::proto;

    use crate::{
        settings::{HumanSize, QueueFullAction},
        AuthId, ClientId, ClientInfo, SessionConfig, SessionState, Subscription,
    };

    #[test]
    fn test_publish_to_inflight() {
        let client_id = ClientId::from("id1");
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let max_inflight = 3;
        let max_queued = 10;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_info, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication), Ok(Some(_)));

        assert_eq!(session.waiting_to_be_acked().len(), 3);
    }

    #[test]
    fn test_publish_to_queues_when_inflight_full() {
        let client_id = ClientId::from("id1");
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let max_inflight = 2;
        let max_queued = 10;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_info, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));

        assert_matches!(session.publish_to(publication), Ok(None)); //inflight is full.

        assert_eq!(session.waiting_to_be_acked.len(), 2);
        assert_eq!(session.waiting_to_be_sent.len(), 1);
    }
    #[test]
    fn test_publish_to_drops_new_message_when_queue_count_limit_reached() {
        let client_id = ClientId::from("id1");
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let max_inflight = 2;
        let max_queued = 2;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_info, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));

        assert_matches!(session.publish_to(publication.clone()), Ok(None));
        assert_matches!(session.publish_to(publication), Ok(None));

        let publication = new_publication(topic, "last message");
        assert_matches!(session.publish_to(publication), Ok(None));

        assert_eq!(session.waiting_to_be_acked.len(), 2);
        assert_eq!(session.waiting_to_be_sent.len(), 2);

        assert_matches!(
            session
                .waiting_to_be_sent
                .iter()
                .find(|p| p.payload == Bytes::from("last message")),
            None
        );
    }

    #[test]
    fn test_publish_to_drops_new_message_when_queue_size_limit_reached() {
        let client_id = ClientId::from("id1");
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let max_inflight = 2;
        let max_size = HumanSize::new_bytes(10);
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            max_inflight,
            0,
            Some(max_size),
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_info, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));

        assert_matches!(session.publish_to(publication), Ok(None));

        let publication = new_publication(topic, "last message");
        assert_matches!(session.publish_to(publication), Ok(None));

        assert_eq!(session.waiting_to_be_acked.len(), 2);
        assert_eq!(session.waiting_to_be_sent.len(), 1);

        assert_matches!(
            session
                .waiting_to_be_sent
                .iter()
                .find(|p| p.payload == Bytes::from("last message")),
            None
        );
    }

    #[test]
    fn test_publish_to_drops_old_message_when_queue_count_limit_reached() {
        let client_id = ClientId::from("id1");
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let max_inflight = 2;
        let max_queued = 2;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropOld,
        );

        let mut session = SessionState::new(client_info, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));

        let first_queued = new_publication(topic, "first message");

        assert_matches!(session.publish_to(first_queued), Ok(None));
        assert_matches!(session.publish_to(publication.clone()), Ok(None));
        assert_matches!(session.publish_to(publication), Ok(None));

        assert_eq!(session.waiting_to_be_acked.len(), 2);
        assert_eq!(session.waiting_to_be_sent.len(), 2);

        assert_matches!(
            session
                .waiting_to_be_sent
                .iter()
                .find(|p| p.payload == Bytes::from("first message")),
            None
        );
    }

    #[test]
    fn test_publish_to_drops_old_message_when_queue_size_limit_reached() {
        let client_id = ClientId::from("id1");
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let max_inflight = 2;
        let max_size = HumanSize::new_bytes(10);
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            max_inflight,
            0,
            Some(max_size),
            QueueFullAction::DropOld,
        );

        let mut session = SessionState::new(client_info, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));

        let first_queued = new_publication(topic, "first message");

        assert_matches!(session.publish_to(first_queued), Ok(None));
        assert_matches!(session.publish_to(publication.clone()), Ok(None));
        assert_matches!(session.publish_to(publication), Ok(None));

        assert_eq!(session.waiting_to_be_acked.len(), 2);
        assert_eq!(session.waiting_to_be_sent.len(), 1);

        assert_matches!(
            session
                .waiting_to_be_sent
                .iter()
                .find(|p| p.payload == Bytes::from("first message")),
            None
        );
    }

    #[test]
    fn test_publish_to_drops_old_message_when_queue_full() {
        let client_id = ClientId::from("id1");
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let max_inflight = 2;
        let max_queued = 2;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropOld,
        );

        let mut session = SessionState::new(client_info, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));

        let first_queued = new_publication(topic, "first message");

        assert_matches!(session.publish_to(first_queued), Ok(None));
        assert_matches!(session.publish_to(publication.clone()), Ok(None));
        assert_matches!(session.publish_to(publication), Ok(None));

        assert_eq!(session.waiting_to_be_acked.len(), 2);
        assert_eq!(session.waiting_to_be_sent.len(), 2);

        assert_matches!(
            session
                .waiting_to_be_sent
                .iter()
                .find(|p| p.payload == Bytes::from("first message")),
            None
        );
    }

    #[test]
    fn test_publish_to_ignores_zero_max_inflight_messages() {
        let client_id = ClientId::from("id1");
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let max_inflight = 0;
        let max_queued = 0;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_info, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication), Ok(Some(_)));

        assert_eq!(session.waiting_to_be_acked.len(), 3);
        assert_eq!(session.waiting_to_be_sent.len(), 0);
    }

    #[test]
    fn test_publish_to_ignores_zero_max_queued_messages() {
        let client_id = ClientId::from("id1");
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let max_inflight = 2;
        let max_queued = 0;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_info, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));

        assert_matches!(session.publish_to(publication.clone()), Ok(None));
        assert_matches!(session.publish_to(publication), Ok(None));

        assert_eq!(session.waiting_to_be_acked.len(), 2);
        assert_eq!(session.waiting_to_be_sent.len(), 2);
    }

    fn new_publication(topic: impl Into<String>, payload: impl Into<Bytes>) -> proto::Publication {
        proto::Publication {
            topic_name: topic.into(),
            qos: proto::QoS::AtLeastOnce,
            retain: false,
            payload: payload.into(),
        }
    }

    fn subscribe_to(topic: &str, session: &mut SessionState) {
        session.update_subscription(
            topic.into(),
            Subscription::new(
                topic.parse().expect("unable to parse topic filter"),
                proto::QoS::AtLeastOnce,
            ),
        );
    }
}
