use std::{
    cmp,
    collections::{HashMap, HashSet, VecDeque},
    num::NonZeroUsize,
};

use tracing::debug;

use mqtt3::proto;

use super::identifiers::PacketIdentifiers;
use crate::{
    configuration::QueueFullAction, snapshot::SessionSnapshot, subscription::Subscription,
    ClientEvent, ClientId, Error, Publish, SessionConfig,
};

/// Common data and functions for broker sessions.
#[derive(Clone, Debug, PartialEq)]
pub struct SessionState {
    client_id: ClientId,
    subscriptions: HashMap<String, Subscription>,
    packet_identifiers: PacketIdentifiers,
    packet_identifiers_qos0: PacketIdentifiers,

    waiting_to_be_sent: BoundedQueue,

    // for incoming messages - QoS2
    waiting_to_be_released: HashMap<proto::PacketIdentifier, proto::Publish>,

    // for outgoing messages - all QoS
    waiting_to_be_acked: HashMap<proto::PacketIdentifier, Publish>,
    waiting_to_be_acked_qos0: HashMap<proto::PacketIdentifier, Publish>,
    waiting_to_be_completed: HashSet<proto::PacketIdentifier>,
    config: SessionConfig,
}

impl SessionState {
    pub fn new(client_id: ClientId, config: SessionConfig) -> Self {
        Self {
            client_id,
            subscriptions: HashMap::new(),
            packet_identifiers: PacketIdentifiers::default(),
            packet_identifiers_qos0: PacketIdentifiers::default(),

            waiting_to_be_sent: BoundedQueue::new(
                config.max_queued_messages(),
                config.max_queued_size(),
                config.when_full(),
            ),
            waiting_to_be_acked: HashMap::new(),
            waiting_to_be_acked_qos0: HashMap::new(),
            waiting_to_be_released: HashMap::new(),
            waiting_to_be_completed: HashSet::new(),
            config,
        }
    }

    pub fn from_snapshot(snapshot: SessionSnapshot, config: SessionConfig) -> Self {
        let (client_id, subscriptions, queued_publications) = snapshot.into_parts();

        let mut waiting_to_be_sent = BoundedQueue::new(
            config.max_queued_messages(),
            config.max_queued_size(),
            config.when_full(),
        );
        waiting_to_be_sent.extend(queued_publications);

        Self {
            client_id,
            subscriptions,
            packet_identifiers: PacketIdentifiers::default(),
            waiting_to_be_sent,
            waiting_to_be_acked: HashMap::new(),
            waiting_to_be_released: HashMap::new(),
            waiting_to_be_completed: HashSet::new(),
            waiting_to_be_acked_qos0: HashMap::new(),
            packet_identifiers_qos0: PacketIdentifiers::default(),
            config,
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_id
    }

    pub fn subscriptions(&self) -> &HashMap<String, Subscription> {
        &self.subscriptions
    }

    pub(super) fn waiting_to_be_acked(&self) -> &HashMap<proto::PacketIdentifier, Publish> {
        &self.waiting_to_be_acked
    }

    pub(super) fn waiting_to_be_acked_qos0(&self) -> &HashMap<proto::PacketIdentifier, Publish> {
        &self.waiting_to_be_acked_qos0
    }

    pub(super) fn waiting_to_be_completed(&self) -> &HashSet<proto::PacketIdentifier> {
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
            self.waiting_to_be_sent.enqueue(publication);
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
                self.waiting_to_be_sent.enqueue(publication);
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

impl From<SessionState> for SessionSnapshot {
    fn from(state: SessionState) -> Self {
        SessionSnapshot::from_parts(
            state.client_id,
            state.subscriptions,
            state.waiting_to_be_sent.into_inner(),
        )
    }
}

/// `BoundedQueue` is a queue of publications with bounds by count and total payload size in bytes.
///
/// Packets will be queued until either `max_len` (max number of publications)
/// or `max_size` (max total payload size of publications)
/// is reached, and then `when_full` strategy is applied.
///
/// None for `max_len` or `max_size` means "unbounded".
#[derive(Clone, Debug, PartialEq)]
pub(super) struct BoundedQueue {
    inner: VecDeque<proto::Publication>,
    max_len: Option<NonZeroUsize>,
    max_size: Option<NonZeroUsize>,
    when_full: QueueFullAction,
    current_size: usize,
}

impl BoundedQueue {
    pub fn new(
        max_len: Option<NonZeroUsize>,
        max_size: Option<NonZeroUsize>,
        when_full: QueueFullAction,
    ) -> Self {
        Self {
            inner: VecDeque::new(),
            max_len,
            max_size,
            when_full,
            current_size: 0,
        }
    }

    pub fn into_inner(self) -> VecDeque<proto::Publication> {
        self.inner
    }

    pub fn dequeue(&mut self) -> Option<proto::Publication> {
        match self.inner.pop_front() {
            Some(publication) => {
                self.current_size -= publication.payload.len();
                Some(publication)
            }
            None => None,
        }
    }

    pub fn enqueue(&mut self, publication: proto::Publication) {
        if let Some(max_len) = self.max_len {
            if self.inner.len() >= max_len.get() {
                return self.handle_queue_limit(publication);
            }
        }

        if let Some(max_size) = self.max_size {
            let pub_len = publication.payload.len();
            if self.current_size + pub_len > max_size.get() {
                return self.handle_queue_limit(publication);
            }
        }

        self.current_size += publication.payload.len();
        self.inner.push_back(publication);
    }

    #[cfg(test)]
    pub fn len(&self) -> usize {
        self.inner.len()
    }

    #[cfg(test)]
    pub fn iter(&self) -> std::collections::vec_deque::Iter<'_, proto::Publication> {
        self.inner.iter()
    }

    fn handle_queue_limit(&mut self, publication: proto::Publication) {
        match self.when_full {
            QueueFullAction::DropNew => {
                // do nothing
            }
            QueueFullAction::DropOld => {
                let _ = self.dequeue();
                self.current_size += publication.payload.len();
                self.inner.push_back(publication);
            }
        };
    }
}

impl Extend<proto::Publication> for BoundedQueue {
    fn extend<T: IntoIterator<Item = proto::Publication>>(&mut self, iter: T) {
        iter.into_iter().for_each(|item| self.enqueue(item));
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use bytes::Bytes;
    use matches::assert_matches;

    use mqtt3::proto;

    use super::SessionState;
    use crate::{
        configuration::{HumanSize, QueueFullAction},
        ClientId, SessionConfig, Subscription,
    };

    #[test]
    fn test_publish_to_inflight() {
        let client_id = ClientId::from("id1");
        let max_inflight = 3;
        let max_queued = 10;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_id, config);

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
        let max_inflight = 2;
        let max_queued = 10;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_id, config);

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
        let max_inflight = 2;
        let max_queued = 2;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_id, config);

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
        let max_inflight = 2;
        let max_size = HumanSize::new_bytes(10);
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            None,
            max_inflight,
            0,
            Some(max_size),
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_id, config);

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
        let max_inflight = 2;
        let max_queued = 2;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropOld,
        );

        let mut session = SessionState::new(client_id, config);

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
        let max_inflight = 2;
        let max_size = HumanSize::new_bytes(10);
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            None,
            max_inflight,
            0,
            Some(max_size),
            QueueFullAction::DropOld,
        );

        let mut session = SessionState::new(client_id, config);

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
        let max_inflight = 2;
        let max_queued = 2;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropOld,
        );

        let mut session = SessionState::new(client_id, config);

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
        let max_inflight = 0;
        let max_queued = 0;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_id, config);

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
        let max_inflight = 2;
        let max_queued = 0;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            None,
            max_inflight,
            max_queued,
            None,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_id, config);

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
