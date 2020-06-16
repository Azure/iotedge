use std::{
    cmp,
    collections::{HashMap, HashSet, VecDeque},
    fmt, mem,
    num::{NonZeroU64, NonZeroUsize},
};

use serde::{
    de::{SeqAccess, Visitor},
    ser::SerializeTuple,
    Deserialize, Deserializer, Serialize, Serializer,
};
use tracing::{debug, warn};

use mqtt3::proto;
use mqtt_broker_core::{auth::AuthId, ClientInfo};

use crate::{
    configuration::QueueFullAction, snapshot::SessionSnapshot, subscription::Subscription,
    ClientEvent, ClientId, ConnReq, ConnectionHandle, Error, Message, Publish, SessionConfig,
};

#[derive(Debug)]
pub struct ConnectedSession {
    state: SessionState,
    client_info: ClientInfo,
    will: Option<proto::Publication>,
    handle: ConnectionHandle,
}

impl ConnectedSession {
    fn new(
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
        &self.state.client_id
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
        &self.state.subscriptions
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

    fn send(&mut self, event: ClientEvent) -> Result<(), Error> {
        let message = Message::Client(self.state.client_id.clone(), event);
        self.handle.send(message)
    }
}

#[derive(Debug)]
pub struct OfflineSession {
    state: SessionState,
}

impl OfflineSession {
    fn new(state: SessionState) -> Self {
        Self { state }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.state.client_id
    }

    pub fn snapshot(&self) -> SessionSnapshot {
        self.state.clone().into()
    }

    pub fn subscriptions(&self) -> &HashMap<String, Subscription> {
        &self.state.subscriptions
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
        for (id, publish) in &state.waiting_to_be_acked {
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

        // Handle the outstanding QoS 0 packets
        for (id, publish) in &state.waiting_to_be_acked_qos0 {
            debug!("resending QoS0 packet {}", id);
            events.push(ClientEvent::PublishTo(publish.clone()));
        }

        // Handle the outstanding QoS 2 packets in the second stage of transmission
        for completed in &state.waiting_to_be_completed {
            events.push(ClientEvent::PubRel(proto::PubRel {
                packet_identifier: *completed,
            }));
        }

        // Dequeue any queued messages - up to the max inflight count
        while state.allowed_to_send() {
            match state.waiting_to_be_sent.dequeue() {
                Some(publication) => {
                    debug!("dequeueing a message for {}", state.client_id);
                    let event = state.prepare_to_send(&publication)?;
                    events.push(event);
                }
                None => break,
            }
        }

        Ok((state, events))
    }
}

#[derive(Debug)]
pub struct DisconnectingSession {
    client_info: ClientInfo,
    client_id: ClientId,
    will: Option<proto::Publication>,
    handle: ConnectionHandle,
}

impl DisconnectingSession {
    fn new(
        client_id: ClientId,
        client_info: ClientInfo,
        will: Option<proto::Publication>,
        handle: ConnectionHandle,
    ) -> Self {
        Self {
            client_id,
            client_info,
            will,
            handle,
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_id
    }

    pub fn client_info(&self) -> &ClientInfo {
        &self.client_info
    }

    pub fn into_will(self) -> Option<proto::Publication> {
        self.will
    }

    fn send(&mut self, event: ClientEvent) -> Result<(), Error> {
        let message = Message::Client(self.client_id.clone(), event);
        self.handle.send(message)
    }
}

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

    fn allowed_to_send(&self) -> bool {
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

    fn prepare_to_send(&mut self, publication: &proto::Publication) -> Result<ClientEvent, Error> {
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

#[derive(Debug)]
pub enum Session {
    Transient(ConnectedSession),
    Persistent(ConnectedSession),
    Disconnecting(DisconnectingSession),
    Offline(OfflineSession),
}

impl Session {
    pub fn new_transient(auth_id: AuthId, connreq: ConnReq, state: SessionState) -> Self {
        let (peer_addr, connect, handle) = connreq.into_parts();
        let client_info = ClientInfo::new(peer_addr, auth_id);
        let connected = ConnectedSession::new(state, client_info, connect.will, handle);
        Self::Transient(connected)
    }

    pub fn new_persistent(auth_id: AuthId, connreq: ConnReq, state: SessionState) -> Self {
        let (peer_addr, connect, handle) = connreq.into_parts();
        let client_info = ClientInfo::new(peer_addr, auth_id);
        let connected = ConnectedSession::new(state, client_info, connect.will, handle);
        Self::Persistent(connected)
    }

    pub fn new_offline(state: SessionState) -> Self {
        let offline = OfflineSession::new(state);
        Self::Offline(offline)
    }

    pub fn new_disconnecting(
        client_id: ClientId,
        client_info: ClientInfo,
        will: Option<proto::Publication>,
        handle: ConnectionHandle,
    ) -> Self {
        let disconnecting = DisconnectingSession::new(client_id, client_info, will, handle);
        Self::Disconnecting(disconnecting)
    }

    pub fn client_id(&self) -> &ClientId {
        match self {
            Self::Transient(connected) => connected.client_id(),
            Self::Persistent(connected) => connected.client_id(),
            Self::Offline(offline) => offline.client_id(),
            Self::Disconnecting(disconnecting) => disconnecting.client_id(),
        }
    }

    pub fn client_info(&self) -> Result<&ClientInfo, Error> {
        match self {
            Self::Transient(connected) => Ok(connected.client_info()),
            Self::Persistent(connected) => Ok(connected.client_info()),
            Self::Offline(_offline) => Err(Error::SessionOffline),
            Self::Disconnecting(disconnecting) => Ok(disconnecting.client_info()),
        }
    }

    pub fn into_will(self) -> Option<proto::Publication> {
        match self {
            Self::Transient(connected) => connected.into_will(),
            Self::Persistent(connected) => connected.into_will(),
            Self::Offline(_offline) => None,
            Self::Disconnecting(disconnecting) => disconnecting.into_will(),
        }
    }

    pub fn subscriptions(&self) -> Option<&HashMap<String, Subscription>> {
        match self {
            Self::Transient(connected) => Some(connected.subscriptions()),
            Self::Persistent(connected) => Some(connected.subscriptions()),
            Self::Offline(offline) => Some(offline.subscriptions()),
            Self::Disconnecting(_) => None,
        }
    }

    pub fn handle_publish(
        &mut self,
        publish: proto::Publish,
    ) -> Result<(Option<proto::Publication>, Option<ClientEvent>), Error> {
        match self {
            Self::Transient(connected) => connected.handle_publish(publish),
            Self::Persistent(connected) => connected.handle_publish(publish),
            Self::Offline(_offline) => Err(Error::SessionOffline),
            Self::Disconnecting(_) => Err(Error::SessionOffline),
        }
    }

    pub fn handle_puback(&mut self, puback: &proto::PubAck) -> Result<Option<ClientEvent>, Error> {
        match self {
            Self::Transient(connected) => connected.handle_puback(puback),
            Self::Persistent(connected) => connected.handle_puback(puback),
            Self::Offline(_offline) => Err(Error::SessionOffline),
            Self::Disconnecting(_) => Err(Error::SessionOffline),
        }
    }

    pub fn handle_puback0(
        &mut self,
        id: proto::PacketIdentifier,
    ) -> Result<Option<ClientEvent>, Error> {
        match self {
            Self::Transient(connected) => connected.handle_puback0(id),
            Self::Persistent(connected) => connected.handle_puback0(id),
            Self::Offline(_offline) => Err(Error::SessionOffline),
            Self::Disconnecting(_) => Err(Error::SessionOffline),
        }
    }

    pub fn handle_pubrec(&mut self, pubrec: &proto::PubRec) -> Result<Option<ClientEvent>, Error> {
        match self {
            Self::Transient(connected) => connected.handle_pubrec(pubrec),
            Self::Persistent(connected) => connected.handle_pubrec(pubrec),
            Self::Offline(_offline) => Err(Error::SessionOffline),
            Self::Disconnecting(_) => Err(Error::SessionOffline),
        }
    }

    pub fn handle_pubrel(
        &mut self,
        pubrel: &proto::PubRel,
    ) -> Result<Option<proto::Publication>, Error> {
        match self {
            Self::Transient(connected) => connected.handle_pubrel(pubrel),
            Self::Persistent(connected) => connected.handle_pubrel(pubrel),
            Self::Offline(_offline) => Err(Error::SessionOffline),
            Self::Disconnecting(_) => Err(Error::SessionOffline),
        }
    }

    pub fn handle_pubcomp(
        &mut self,
        pubcomp: &proto::PubComp,
    ) -> Result<Option<ClientEvent>, Error> {
        match self {
            Self::Transient(connected) => connected.handle_pubcomp(pubcomp),
            Self::Persistent(connected) => connected.handle_pubcomp(pubcomp),
            Self::Offline(_offline) => Err(Error::SessionOffline),
            Self::Disconnecting(_) => Err(Error::SessionOffline),
        }
    }

    pub fn publish_to(
        &mut self,
        publication: &proto::Publication,
    ) -> Result<Option<ClientEvent>, Error> {
        match self {
            Self::Transient(connected) => connected.publish_to(publication.to_owned()),
            Self::Persistent(connected) => connected.publish_to(publication.to_owned()),
            Self::Offline(offline) => offline.publish_to(publication.to_owned()),
            Self::Disconnecting(_) => Err(Error::SessionOffline),
        }
    }

    pub fn subscribe_to(
        &mut self,
        subscribe_to: proto::SubscribeTo,
    ) -> Result<(proto::SubAckQos, Option<Subscription>), Error> {
        match self {
            Self::Transient(connected) => connected.subscribe_to(subscribe_to),
            Self::Persistent(connected) => connected.subscribe_to(subscribe_to),
            Self::Offline(_) => Err(Error::SessionOffline),
            Self::Disconnecting(_) => Err(Error::SessionOffline),
        }
    }

    pub fn unsubscribe(
        &mut self,
        unsubscribe: &proto::Unsubscribe,
    ) -> Result<proto::UnsubAck, Error> {
        match self {
            Self::Transient(connected) => connected.unsubscribe(unsubscribe),
            Self::Persistent(connected) => connected.unsubscribe(unsubscribe),
            Self::Offline(_) => Err(Error::SessionOffline),
            Self::Disconnecting(_) => Err(Error::SessionOffline),
        }
    }

    pub fn send(&mut self, event: ClientEvent) -> Result<(), Error> {
        match self {
            Self::Transient(ref mut connected) => connected.send(event),
            Self::Persistent(ref mut connected) => connected.send(event),
            Self::Disconnecting(ref mut disconnecting) => disconnecting.send(event),
            _ => Err(Error::SessionOffline),
        }
    }
}

#[derive(Clone)]
pub(crate) struct IdentifiersInUse(pub(crate) Box<[usize; PacketIdentifiers::SIZE]>);

impl fmt::Debug for IdentifiersInUse {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("IdentifiersInUse").finish()
    }
}

impl cmp::PartialEq for IdentifiersInUse {
    fn eq(&self, other: &Self) -> bool {
        self.0.iter().zip(other.0.iter()).all(|(a, b)| a.eq(b))
    }
}

struct IdentifiersInUseVisitor;

impl<'de> Visitor<'de> for IdentifiersInUseVisitor {
    type Value = IdentifiersInUse;

    fn expecting(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "an array of length {}", PacketIdentifiers::SIZE)
    }

    #[inline]
    fn visit_seq<A>(self, mut seq: A) -> Result<Self::Value, A::Error>
    where
        A: SeqAccess<'de>,
    {
        let mut ids = Box::new([0; PacketIdentifiers::SIZE]);
        for i in 0..PacketIdentifiers::SIZE {
            ids[i] = match seq.next_element()? {
                Some(val) => val,
                None => return Err(serde::de::Error::invalid_length(i, &self)),
            };
        }
        Ok(IdentifiersInUse(ids))
    }
}

impl Serialize for IdentifiersInUse {
    #[inline]
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {
        let mut seq = serializer.serialize_tuple(PacketIdentifiers::SIZE)?;
        for e in self.0.iter() {
            seq.serialize_element(e)?;
        }
        seq.end()
    }
}

impl<'de> Deserialize<'de> for IdentifiersInUse {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {
        deserializer.deserialize_tuple(PacketIdentifiers::SIZE, IdentifiersInUseVisitor)
    }
}

#[derive(Clone, PartialEq, Serialize, Deserialize)]
pub(crate) struct PacketIdentifiers {
    in_use: IdentifiersInUse,
    previous: proto::PacketIdentifier,
}

impl PacketIdentifiers {
    /// Size of a bitset for every packet identifier
    ///
    /// Packet identifiers are u16's, so the number of usize's required
    /// = number of u16's / number of bits in a usize
    /// = pow(2, number of bits in a u16) / number of bits in a usize
    /// = pow(2, 16) / (`size_of::<usize>()` * 8)
    ///
    /// We use a bitshift instead of `usize::pow` because the latter is not a const fn
    pub(crate) const SIZE: usize = (1 << 16) / (mem::size_of::<usize>() * 8);

    #[cfg(any(test, feature = "proptest"))]
    pub(crate) fn new(in_use: IdentifiersInUse, previous: proto::PacketIdentifier) -> Self {
        Self { in_use, previous }
    }

    fn reserve(&mut self) -> Result<proto::PacketIdentifier, Error> {
        let start = self.previous;
        let mut current = start;

        current += 1;

        let (block, mask) = self.entry(current);
        if (*block & mask) != 0 {
            return Err(Error::PacketIdentifiersExhausted);
        }

        *block |= mask;
        self.previous = current;
        Ok(current)
    }

    fn discard(&mut self, packet_identifier: proto::PacketIdentifier) {
        let (block, mask) = self.entry(packet_identifier);
        *block &= !mask;
    }

    fn entry(&mut self, packet_identifier: proto::PacketIdentifier) -> (&mut usize, usize) {
        let packet_identifier = usize::from(packet_identifier.get());
        let (block, offset) = (
            packet_identifier / (mem::size_of::<usize>() * 8),
            packet_identifier % (mem::size_of::<usize>() * 8),
        );
        (&mut self.in_use.0[block], 1 << offset)
    }
}

impl fmt::Debug for PacketIdentifiers {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("PacketIdentifiers")
            .field("previous", &self.previous)
            .finish()
    }
}

impl Default for PacketIdentifiers {
    fn default() -> Self {
        PacketIdentifiers {
            in_use: IdentifiersInUse(Box::new([0; PacketIdentifiers::SIZE])),
            previous: proto::PacketIdentifier::max_value(),
        }
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
struct BoundedQueue {
    inner: VecDeque<proto::Publication>,
    max_len: Option<NonZeroUsize>,
    max_size: Option<NonZeroU64>,
    when_full: QueueFullAction,
    current_size: u64,
}

impl BoundedQueue {
    pub fn new(
        max_len: Option<NonZeroUsize>,
        max_size: Option<NonZeroU64>,
        when_full: QueueFullAction,
    ) -> Self {
        BoundedQueue {
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
                self.current_size -= publication.payload.len() as u64;
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
            let pub_len = publication.payload.len() as u64;
            if self.current_size + pub_len > max_size.get() {
                return self.handle_queue_limit(publication);
            }
        }

        self.current_size += publication.payload.len() as u64;
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
                self.current_size += publication.payload.len() as u64;
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
    use tokio::sync::mpsc;
    use uuid::Uuid;

    use mqtt3::{proto, PROTOCOL_LEVEL, PROTOCOL_NAME};
    use mqtt_broker_core::auth::AuthId;

    use crate::{
        configuration::QueueFullAction,
        session::{PacketIdentifiers, Session, SessionState},
        tests::peer_addr,
        Auth, ClientId, ConnReq, ConnectionHandle, Error, SessionConfig, Subscription,
    };

    fn connection_handle() -> ConnectionHandle {
        let id = Uuid::new_v4();
        let (tx1, _rx1) = mpsc::unbounded_channel();
        ConnectionHandle::new(id, tx1)
    }

    fn transient_connect(id: String) -> proto::Connect {
        proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession(id),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
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

    fn new_publication(topic: impl Into<String>, payload: impl Into<Bytes>) -> proto::Publication {
        proto::Publication {
            topic_name: topic.into(),
            qos: proto::QoS::AtLeastOnce,
            retain: false,
            payload: payload.into(),
        }
    }

    fn default_config() -> SessionConfig {
        SessionConfig::new(
            Duration::default(),
            0,
            16,
            1000,
            0,
            QueueFullAction::DropNew,
        )
    }

    #[test]
    fn test_subscribe_to() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id);
        let handle1 = connection_handle();
        let req1 = ConnReq::new(
            client_id.clone(),
            peer_addr(),
            connect1,
            Auth::Unknown,
            handle1,
        );
        let auth_id = "auth-id1".into();
        let state = SessionState::new(client_id, default_config());
        let mut session = Session::new_transient(auth_id, req1, state);
        let subscribe_to = proto::SubscribeTo {
            topic_filter: "topic/new".to_string(),
            qos: proto::QoS::AtMostOnce,
        };

        let (ack, subscription) = session.subscribe_to(subscribe_to).unwrap();

        assert_eq!(ack, proto::SubAckQos::Success(proto::QoS::AtMostOnce));
        assert_matches!(subscription, Some(_));
        match &session {
            Session::Transient(connected) => {
                assert_eq!(1, connected.state.subscriptions.len());
                assert_eq!(
                    proto::QoS::AtMostOnce,
                    *connected.state.subscriptions["topic/new"].max_qos()
                );
            }
            _ => panic!("not transient"),
        }

        let subscribe_to = proto::SubscribeTo {
            topic_filter: "topic/new".to_string(),
            qos: proto::QoS::AtLeastOnce,
        };

        let (ack, subscription) = session.subscribe_to(subscribe_to).unwrap();

        assert_eq!(ack, proto::SubAckQos::Success(proto::QoS::AtLeastOnce));
        assert_matches!(subscription, Some(_));
        match &session {
            Session::Transient(connected) => {
                assert_eq!(1, connected.state.subscriptions.len());
                assert_eq!(
                    proto::QoS::AtLeastOnce,
                    *connected.state.subscriptions["topic/new"].max_qos()
                );
            }
            _ => panic!("not transient"),
        }
    }

    #[test]
    fn test_subscribe_to_with_invalid_topic() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id);
        let handle1 = connection_handle();
        let req1 = ConnReq::new(
            client_id.clone(),
            peer_addr(),
            connect1,
            Auth::Unknown,
            handle1,
        );
        let auth_id = "auth-id1".into();
        let state = SessionState::new(client_id, default_config());
        let mut session = Session::new_transient(auth_id, req1, state);
        let subscribe_to = proto::SubscribeTo {
            topic_filter: "topic/#/#".to_string(),
            qos: proto::QoS::AtMostOnce,
        };

        let (ack, subscription) = session.subscribe_to(subscribe_to).unwrap();

        assert_eq!(ack, proto::SubAckQos::Failure);
        assert_eq!(subscription, None);
    }

    #[test]
    fn test_unsubscribe() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id);
        let handle1 = connection_handle();
        let req1 = ConnReq::new(
            client_id.clone(),
            peer_addr(),
            connect1,
            Auth::Unknown,
            handle1,
        );
        let auth_id = AuthId::Anonymous;
        let state = SessionState::new(client_id, default_config());
        let mut session = Session::new_transient(auth_id, req1, state);

        let subscribe_to = proto::SubscribeTo {
            topic_filter: "topic/new".to_string(),
            qos: proto::QoS::AtMostOnce,
        };

        let (ack, subscription) = session.subscribe_to(subscribe_to).unwrap();
        assert_eq!(ack, proto::SubAckQos::Success(proto::QoS::AtMostOnce));
        assert_matches!(subscription, Some(_));

        let unsubscribe = proto::Unsubscribe {
            packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
            unsubscribe_from: vec!["topic/different".to_string()],
        };
        session.unsubscribe(&unsubscribe).unwrap();

        match &session {
            Session::Transient(connected) => {
                assert_eq!(1, connected.state.subscriptions.len());
                assert_eq!(
                    proto::QoS::AtMostOnce,
                    *connected.state.subscriptions["topic/new"].max_qos()
                );
            }
            _ => panic!("not transient"),
        }

        let unsubscribe = proto::Unsubscribe {
            packet_identifier: proto::PacketIdentifier::new(24).unwrap(),
            unsubscribe_from: vec!["topic/new".to_string()],
        };
        let unsuback = session.unsubscribe(&unsubscribe).unwrap();
        assert_eq!(
            proto::PacketIdentifier::new(24).unwrap(),
            unsuback.packet_identifier
        );

        match &session {
            Session::Transient(connected) => {
                assert_eq!(0, connected.state.subscriptions.len());
            }
            _ => panic!("not transient"),
        }
    }

    #[test]
    fn test_offline_subscribe_to() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id);
        let mut session = Session::new_offline(SessionState::new(client_id, default_config()));

        let subscribe_to = proto::SubscribeTo {
            topic_filter: "topic/new".to_string(),
            qos: proto::QoS::AtMostOnce,
        };
        let result = session.subscribe_to(subscribe_to);
        assert_matches!(result, Err(Error::SessionOffline));
    }

    #[test]
    fn test_offline_unsubscribe() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id);
        let mut session = Session::new_offline(SessionState::new(client_id, default_config()));

        let unsubscribe = proto::Unsubscribe {
            packet_identifier: proto::PacketIdentifier::new(24).unwrap(),
            unsubscribe_from: vec!["topic/new".to_string()],
        };
        let result = session.unsubscribe(&unsubscribe);
        assert_matches!(result, Err(Error::SessionOffline));
    }

    #[test]
    fn test_publish_to_inflight() {
        let client_id = ClientId::from("id1");
        let max_inflight = 3;
        let max_queued = 10;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            0,
            max_inflight,
            max_queued,
            0,
            QueueFullAction::DropNew,
        );

        let mut session = SessionState::new(client_id, config);

        subscribe_to(topic, &mut session);

        let publication = new_publication(topic, "payload");

        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication.clone()), Ok(Some(_)));
        assert_matches!(session.publish_to(publication), Ok(Some(_)));

        assert_eq!(session.waiting_to_be_acked.len(), 3);
    }

    #[test]
    fn test_publish_to_queues_when_inflight_full() {
        let client_id = ClientId::from("id1");
        let max_inflight = 2;
        let max_queued = 10;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            0,
            max_inflight,
            max_queued,
            0,
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
            0,
            max_inflight,
            max_queued,
            0,
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
        let max_size = 10;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            0,
            max_inflight,
            0,
            max_size,
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
            0,
            max_inflight,
            max_queued,
            0,
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
        let max_size = 10;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            0,
            max_inflight,
            0,
            max_size,
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
    fn test_publish_to_ignores_zero_max_inflight_messages() {
        let client_id = ClientId::from("id1");
        let max_inflight = 0;
        let max_queued = 0;
        let topic = "topic/new";

        let config = SessionConfig::new(
            Duration::default(),
            0,
            max_inflight,
            max_queued,
            0,
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
            0,
            max_inflight,
            max_queued,
            0,
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

    #[test]
    fn packet_identifiers() {
        #[cfg(target_pointer_width = "32")]
        assert_eq!(PacketIdentifiers::SIZE, 2048);
        #[cfg(target_pointer_width = "64")]
        assert_eq!(PacketIdentifiers::SIZE, 1024);

        let mut packet_identifiers = PacketIdentifiers::default();
        assert_eq!(
            packet_identifiers.in_use.0[..],
            Box::new([0; PacketIdentifiers::SIZE])[..]
        );

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 1);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = 1 << 1;
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 2);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 1) | (1 << 2);
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 3);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 1) | (1 << 2) | (1 << 3);
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);

        packet_identifiers.discard(crate::proto::PacketIdentifier::new(2).unwrap());
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 1) | (1 << 3);
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 4);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 1) | (1 << 3) | (1 << 4);
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);

        packet_identifiers.discard(crate::proto::PacketIdentifier::new(1).unwrap());
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 3) | (1 << 4);
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);

        packet_identifiers.discard(crate::proto::PacketIdentifier::new(3).unwrap());
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = 1 << 4;
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);

        packet_identifiers.discard(crate::proto::PacketIdentifier::new(4).unwrap());
        assert_eq!(
            packet_identifiers.in_use.0[..],
            Box::new([0; PacketIdentifiers::SIZE])[..]
        );

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 5);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = 1 << 5;
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);

        let goes_in_next_block = std::mem::size_of::<usize>() * 8;
        #[allow(clippy::cast_possible_truncation)]
        for i in 6..=goes_in_next_block {
            assert_eq!(packet_identifiers.reserve().unwrap().get(), i as u16);
        }
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        #[allow(clippy::identity_op)]
        {
            expected[0] = usize::max_value() - (1 << 0) - (1 << 1) - (1 << 2) - (1 << 3) - (1 << 4);
            expected[1] |= 1 << 0;
        }
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);

        #[allow(clippy::cast_possible_truncation, clippy::range_minus_one)]
        for i in 5..=(goes_in_next_block - 1) {
            packet_identifiers.discard(crate::proto::PacketIdentifier::new(i as u16).unwrap());
        }
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        #[allow(clippy::identity_op)]
        {
            expected[1] |= 1 << 0;
        }
        assert_eq!(packet_identifiers.in_use.0[..], expected[..]);
    }
}
