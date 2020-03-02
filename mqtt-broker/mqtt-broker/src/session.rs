use std::collections::{HashMap, HashSet, VecDeque};
use std::{cmp, fmt, mem};

use failure::ResultExt;
use mqtt::proto;
use tracing::{debug, warn};

use crate::subscription::Subscription;
use crate::{ClientEvent, ClientId, ConnReq, ConnectionHandle, Error, ErrorKind, Message, Publish};

const MAX_INFLIGHT_MESSAGES: usize = 16;

#[derive(Debug)]
pub struct ConnectedSession {
    state: SessionState,
    will: Option<proto::Publication>,
    handle: ConnectionHandle,
}

impl ConnectedSession {
    fn new(
        state: SessionState,
        will: Option<proto::Publication>,
        handle: ConnectionHandle,
    ) -> Self {
        Self {
            state,
            will,
            handle,
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.state.client_id
    }

    pub fn handle(&self) -> &ConnectionHandle {
        &self.handle
    }

    pub fn state(&self) -> &SessionState {
        &self.state
    }

    pub fn into_will(self) -> Option<proto::Publication> {
        self.will
    }

    pub fn into_parts(self) -> (SessionState, Option<proto::Publication>, ConnectionHandle) {
        (self.state, self.will, self.handle)
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

    pub fn subscribe(
        &mut self,
        subscribe: proto::Subscribe,
    ) -> Result<(proto::SubAck, Vec<Subscription>), Error> {
        let mut subscriptions = Vec::with_capacity(subscribe.subscribe_to.len());
        let mut acks = Vec::with_capacity(subscribe.subscribe_to.len());
        let packet_identifier = subscribe.packet_identifier;

        for subscribe_to in subscribe.subscribe_to {
            let ack_qos = match subscribe_to.topic_filter.parse() {
                Ok(filter) => {
                    let proto::SubscribeTo { topic_filter, qos } = subscribe_to;

                    let subscription = Subscription::new(filter, qos);
                    subscriptions.push(subscription.clone());
                    self.state.update_subscription(topic_filter, subscription);
                    proto::SubAckQos::Success(qos)
                }
                Err(e) => {
                    warn!("invalid topic filter {}: {}", subscribe_to.topic_filter, e);
                    proto::SubAckQos::Failure
                }
            };
            acks.push(ack_qos);
        }

        let suback = proto::SubAck {
            packet_identifier,
            qos: acks,
        };
        Ok((suback, subscriptions))
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

    async fn send(&mut self, event: ClientEvent) -> Result<(), Error> {
        let message = Message::Client(self.state.client_id.clone(), event);
        self.handle
            .send(message)
            .await
            .context(ErrorKind::SendConnectionMessage)?;
        Ok(())
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

    pub fn state(&self) -> &SessionState {
        &self.state
    }

    pub fn publish_to(
        &mut self,
        publication: proto::Publication,
    ) -> Result<Option<ClientEvent>, Error> {
        self.state.queue_publish(publication)?;
        Ok(None)
    }

    pub fn into_online(self) -> Result<(SessionState, Vec<ClientEvent>), Error> {
        let mut events = Vec::with_capacity(MAX_INFLIGHT_MESSAGES);
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
            match state.waiting_to_be_sent.pop_front() {
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
    client_id: ClientId,
    will: Option<proto::Publication>,
    handle: ConnectionHandle,
}

impl DisconnectingSession {
    fn new(
        client_id: ClientId,
        will: Option<proto::Publication>,
        handle: ConnectionHandle,
    ) -> Self {
        Self {
            client_id,
            will,
            handle,
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_id
    }

    pub fn into_will(self) -> Option<proto::Publication> {
        self.will
    }

    async fn send(&mut self, event: ClientEvent) -> Result<(), Error> {
        let message = Message::Client(self.client_id.clone(), event);
        self.handle
            .send(message)
            .await
            .context(ErrorKind::SendConnectionMessage)?;
        Ok(())
    }
}

#[derive(Clone, Debug)]
pub struct SessionState {
    client_id: ClientId,
    subscriptions: HashMap<String, Subscription>,
    packet_identifiers: PacketIdentifiers,
    packet_identifiers_qos0: PacketIdentifiers,

    waiting_to_be_sent: VecDeque<proto::Publication>,

    // for incoming messages - QoS2
    waiting_to_be_released: HashMap<proto::PacketIdentifier, proto::Publish>,

    // for outgoing messages - all QoS
    waiting_to_be_acked: HashMap<proto::PacketIdentifier, Publish>,
    waiting_to_be_acked_qos0: HashMap<proto::PacketIdentifier, Publish>,
    waiting_to_be_completed: HashSet<proto::PacketIdentifier>,
}

impl SessionState {
    pub fn new(client_id: ClientId) -> Self {
        Self {
            client_id,
            subscriptions: HashMap::new(),
            packet_identifiers: PacketIdentifiers::default(),
            packet_identifiers_qos0: PacketIdentifiers::default(),

            waiting_to_be_sent: VecDeque::new(),
            waiting_to_be_acked: HashMap::new(),
            waiting_to_be_acked_qos0: HashMap::new(),
            waiting_to_be_released: HashMap::new(),
            waiting_to_be_completed: HashSet::new(),
        }
    }

    pub fn client_id(&self) -> &ClientId {
        &self.client_id
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
            self.waiting_to_be_sent.push_back(publication);
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
                self.waiting_to_be_sent.push_back(publication);
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
            if let Some(publication) = self.waiting_to_be_sent.pop_front() {
                let event = self.prepare_to_send(&publication)?;
                return Ok(Some(event));
            }
        }
        Ok(None)
    }

    fn allowed_to_send(&self) -> bool {
        let num_inflight = self.waiting_to_be_acked.len()
            + self.waiting_to_be_acked_qos0.len()
            + self.waiting_to_be_completed.len();
        num_inflight < MAX_INFLIGHT_MESSAGES
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

#[derive(Debug)]
pub enum Session {
    Transient(ConnectedSession),
    Persistent(ConnectedSession),
    Disconnecting(DisconnectingSession),
    Offline(OfflineSession),
}

impl Session {
    pub fn new_transient(connreq: ConnReq) -> Self {
        let state = SessionState::new(connreq.client_id().clone());
        let (connect, handle) = connreq.into_parts();
        let connected = ConnectedSession::new(state, connect.will, handle);
        Session::Transient(connected)
    }

    pub fn new_persistent(connreq: ConnReq, state: SessionState) -> Self {
        let (connect, handle) = connreq.into_parts();
        let connected = ConnectedSession::new(state, connect.will, handle);
        Session::Persistent(connected)
    }

    pub fn new_offline(state: SessionState) -> Self {
        let offline = OfflineSession::new(state);
        Session::Offline(offline)
    }

    pub fn new_disconnecting(
        client_id: ClientId,
        will: Option<proto::Publication>,
        handle: ConnectionHandle,
    ) -> Self {
        let disconnecting = DisconnectingSession::new(client_id, will, handle);
        Session::Disconnecting(disconnecting)
    }

    pub fn client_id(&self) -> &ClientId {
        match self {
            Session::Transient(connected) => connected.client_id(),
            Session::Persistent(connected) => connected.client_id(),
            Session::Offline(offline) => offline.client_id(),
            Session::Disconnecting(disconnecting) => disconnecting.client_id(),
        }
    }

    pub fn into_will(self) -> Option<proto::Publication> {
        match self {
            Session::Transient(connected) => connected.into_will(),
            Session::Persistent(connected) => connected.into_will(),
            Session::Offline(_offline) => None,
            Session::Disconnecting(disconnecting) => disconnecting.into_will(),
        }
    }

    pub fn handle_publish(
        &mut self,
        publish: proto::Publish,
    ) -> Result<(Option<proto::Publication>, Option<ClientEvent>), Error> {
        match self {
            Session::Transient(connected) => connected.handle_publish(publish),
            Session::Persistent(connected) => connected.handle_publish(publish),
            Session::Offline(_offline) => Err(Error::from(ErrorKind::SessionOffline)),
            Session::Disconnecting(_) => Err(Error::from(ErrorKind::SessionOffline)),
        }
    }

    pub fn handle_puback(&mut self, puback: &proto::PubAck) -> Result<Option<ClientEvent>, Error> {
        match self {
            Session::Transient(connected) => connected.handle_puback(puback),
            Session::Persistent(connected) => connected.handle_puback(puback),
            Session::Offline(_offline) => Err(Error::from(ErrorKind::SessionOffline)),
            Session::Disconnecting(_) => Err(Error::from(ErrorKind::SessionOffline)),
        }
    }

    pub fn handle_puback0(
        &mut self,
        id: proto::PacketIdentifier,
    ) -> Result<Option<ClientEvent>, Error> {
        match self {
            Session::Transient(connected) => connected.handle_puback0(id),
            Session::Persistent(connected) => connected.handle_puback0(id),
            Session::Offline(_offline) => Err(Error::from(ErrorKind::SessionOffline)),
            Session::Disconnecting(_) => Err(Error::from(ErrorKind::SessionOffline)),
        }
    }

    pub fn handle_pubrec(&mut self, pubrec: &proto::PubRec) -> Result<Option<ClientEvent>, Error> {
        match self {
            Session::Transient(connected) => connected.handle_pubrec(pubrec),
            Session::Persistent(connected) => connected.handle_pubrec(pubrec),
            Session::Offline(_offline) => Err(Error::from(ErrorKind::SessionOffline)),
            Session::Disconnecting(_) => Err(Error::from(ErrorKind::SessionOffline)),
        }
    }

    pub fn handle_pubrel(
        &mut self,
        pubrel: &proto::PubRel,
    ) -> Result<Option<proto::Publication>, Error> {
        match self {
            Session::Transient(connected) => connected.handle_pubrel(pubrel),
            Session::Persistent(connected) => connected.handle_pubrel(pubrel),
            Session::Offline(_offline) => Err(Error::from(ErrorKind::SessionOffline)),
            Session::Disconnecting(_) => Err(Error::from(ErrorKind::SessionOffline)),
        }
    }

    pub fn handle_pubcomp(
        &mut self,
        pubcomp: &proto::PubComp,
    ) -> Result<Option<ClientEvent>, Error> {
        match self {
            Session::Transient(connected) => connected.handle_pubcomp(pubcomp),
            Session::Persistent(connected) => connected.handle_pubcomp(pubcomp),
            Session::Offline(_offline) => Err(Error::from(ErrorKind::SessionOffline)),
            Session::Disconnecting(_) => Err(Error::from(ErrorKind::SessionOffline)),
        }
    }

    pub fn publish_to(
        &mut self,
        publication: &proto::Publication,
    ) -> Result<Option<ClientEvent>, Error> {
        match self {
            Session::Transient(connected) => connected.publish_to(publication.to_owned()),
            Session::Persistent(connected) => connected.publish_to(publication.to_owned()),
            Session::Offline(offline) => offline.publish_to(publication.to_owned()),
            Session::Disconnecting(_) => Err(Error::from(ErrorKind::SessionOffline)),
        }
    }

    pub fn subscribe(
        &mut self,
        subscribe: proto::Subscribe,
    ) -> Result<(proto::SubAck, Vec<Subscription>), Error> {
        match self {
            Session::Transient(connected) => connected.subscribe(subscribe),
            Session::Persistent(connected) => connected.subscribe(subscribe),
            Session::Offline(_) => Err(Error::from(ErrorKind::SessionOffline)),
            Session::Disconnecting(_) => Err(Error::from(ErrorKind::SessionOffline)),
        }
    }

    pub fn unsubscribe(
        &mut self,
        unsubscribe: &proto::Unsubscribe,
    ) -> Result<proto::UnsubAck, Error> {
        match self {
            Session::Transient(connected) => connected.unsubscribe(unsubscribe),
            Session::Persistent(connected) => connected.unsubscribe(unsubscribe),
            Session::Offline(_) => Err(Error::from(ErrorKind::SessionOffline)),
            Session::Disconnecting(_) => Err(Error::from(ErrorKind::SessionOffline)),
        }
    }

    pub async fn send(&mut self, event: ClientEvent) -> Result<(), Error> {
        match self {
            Session::Transient(ref mut connected) => connected.send(event).await,
            Session::Persistent(ref mut connected) => connected.send(event).await,
            Session::Disconnecting(ref mut disconnecting) => disconnecting.send(event).await,
            _ => Err(ErrorKind::SessionOffline.into()),
        }
    }
}

#[derive(Clone)]
struct PacketIdentifiers {
    in_use: Box<[usize; PacketIdentifiers::SIZE]>,
    previous: proto::PacketIdentifier,
}

impl PacketIdentifiers {
    /// Size of a bitset for every packet identifier
    ///
    /// Packet identifiers are u16's, so the number of usize's required
    /// = number of u16's / number of bits in a usize
    /// = pow(2, number of bits in a u16) / number of bits in a usize
    /// = pow(2, 16) / (size_of::<usize>() * 8)
    ///
    /// We use a bitshift instead of usize::pow because the latter is not a const fn
    const SIZE: usize = (1 << 16) / (mem::size_of::<usize>() * 8);

    fn reserve(&mut self) -> Result<proto::PacketIdentifier, Error> {
        let start = self.previous;
        let mut current = start;

        current += 1;

        let (block, mask) = self.entry(current);
        if (*block & mask) != 0 {
            return Err(Error::from(ErrorKind::PacketIdentifiersExhausted));
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
        (&mut self.in_use[block], 1 << offset)
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
            in_use: Box::new([0; PacketIdentifiers::SIZE]),
            previous: proto::PacketIdentifier::max_value(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    use tokio::sync::mpsc;
    use uuid::Uuid;

    use crate::ConnectionHandle;

    fn connection_handle() -> ConnectionHandle {
        let id = Uuid::new_v4();
        let (tx1, _rx1) = mpsc::channel(128);
        ConnectionHandle::new(id, tx1)
    }

    fn transient_connect(id: String) -> proto::Connect {
        proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession(id),
            keep_alive: Default::default(),
            protocol_name: "MQTT".to_string(),
            protocol_level: 0x4,
        }
    }

    #[test]
    fn test_subscribe() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id.clone());
        let handle1 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);
        let mut session = Session::new_transient(req1);

        let subscribe = proto::Subscribe {
            packet_identifier: proto::PacketIdentifier::new(23).unwrap(),
            subscribe_to: vec![proto::SubscribeTo {
                topic_filter: "topic/new".to_string(),
                qos: proto::QoS::AtMostOnce,
            }],
        };
        let (suback, subscriptions) = session.subscribe(subscribe).unwrap();
        assert_eq!(
            proto::PacketIdentifier::new(23).unwrap(),
            suback.packet_identifier
        );
        match session {
            Session::Transient(ref connected) => {
                assert_eq!(1, connected.state.subscriptions.len());
                assert_eq!(
                    proto::QoS::AtMostOnce,
                    *connected.state.subscriptions["topic/new"].max_qos()
                );
            }
            _ => panic!("not transient"),
        }
        assert_eq!(1, subscriptions.len());

        let subscribe = proto::Subscribe {
            packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![proto::SubscribeTo {
                topic_filter: "topic/new".to_string(),
                qos: proto::QoS::AtLeastOnce,
            }],
        };
        session.subscribe(subscribe).unwrap();

        match session {
            Session::Transient(ref connected) => {
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
    fn test_unsubscribe() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id.clone());
        let handle1 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);
        let mut session = Session::new_transient(req1);

        let subscribe = proto::Subscribe {
            packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![proto::SubscribeTo {
                topic_filter: "topic/new".to_string(),
                qos: proto::QoS::AtMostOnce,
            }],
        };
        session.subscribe(subscribe).unwrap();
        match session {
            Session::Transient(ref connected) => {
                assert_eq!(1, connected.state.subscriptions.len());
                assert_eq!(
                    proto::QoS::AtMostOnce,
                    *connected.state.subscriptions["topic/new"].max_qos()
                );
            }
            _ => panic!("not transient"),
        }

        let unsubscribe = proto::Unsubscribe {
            packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
            unsubscribe_from: vec!["topic/different".to_string()],
        };
        session.unsubscribe(&unsubscribe).unwrap();

        match session {
            Session::Transient(ref connected) => {
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

        match session {
            Session::Transient(ref connected) => {
                assert_eq!(0, connected.state.subscriptions.len());
            }
            _ => panic!("not transient"),
        }
    }

    #[test]
    fn test_offline_subscribe() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let mut session = Session::new_offline(SessionState::new(client_id));

        let subscribe = proto::Subscribe {
            packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![proto::SubscribeTo {
                topic_filter: "topic/new".to_string(),
                qos: proto::QoS::AtMostOnce,
            }],
        };
        let err = session.subscribe(subscribe).unwrap_err();
        assert_eq!(ErrorKind::SessionOffline, *err.kind());
    }

    #[test]
    fn test_offline_unsubscribe() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let mut session = Session::new_offline(SessionState::new(client_id));

        let unsubscribe = proto::Unsubscribe {
            packet_identifier: proto::PacketIdentifier::new(24).unwrap(),
            unsubscribe_from: vec!["topic/new".to_string()],
        };
        let err = session.unsubscribe(&unsubscribe).unwrap_err();
        assert_eq!(ErrorKind::SessionOffline, *err.kind());
    }

    #[test]
    fn packet_identifiers() {
        #[cfg(target_pointer_width = "32")]
        assert_eq!(PacketIdentifiers::SIZE, 2048);
        #[cfg(target_pointer_width = "64")]
        assert_eq!(PacketIdentifiers::SIZE, 1024);

        let mut packet_identifiers: PacketIdentifiers = Default::default();
        assert_eq!(
            packet_identifiers.in_use[..],
            Box::new([0; PacketIdentifiers::SIZE])[..]
        );

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 1);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = 1 << 1;
        assert_eq!(packet_identifiers.in_use[..], expected[..]);

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 2);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 1) | (1 << 2);
        assert_eq!(packet_identifiers.in_use[..], expected[..]);

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 3);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 1) | (1 << 2) | (1 << 3);
        assert_eq!(packet_identifiers.in_use[..], expected[..]);

        packet_identifiers.discard(crate::proto::PacketIdentifier::new(2).unwrap());
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 1) | (1 << 3);
        assert_eq!(packet_identifiers.in_use[..], expected[..]);

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 4);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 1) | (1 << 3) | (1 << 4);
        assert_eq!(packet_identifiers.in_use[..], expected[..]);

        packet_identifiers.discard(crate::proto::PacketIdentifier::new(1).unwrap());
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = (1 << 3) | (1 << 4);
        assert_eq!(packet_identifiers.in_use[..], expected[..]);

        packet_identifiers.discard(crate::proto::PacketIdentifier::new(3).unwrap());
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = 1 << 4;
        assert_eq!(packet_identifiers.in_use[..], expected[..]);

        packet_identifiers.discard(crate::proto::PacketIdentifier::new(4).unwrap());
        assert_eq!(
            packet_identifiers.in_use[..],
            Box::new([0; PacketIdentifiers::SIZE])[..]
        );

        assert_eq!(packet_identifiers.reserve().unwrap().get(), 5);
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        expected[0] = 1 << 5;
        assert_eq!(packet_identifiers.in_use[..], expected[..]);

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
        assert_eq!(packet_identifiers.in_use[..], expected[..]);

        #[allow(clippy::cast_possible_truncation, clippy::range_minus_one)]
        for i in 5..=(goes_in_next_block - 1) {
            packet_identifiers.discard(crate::proto::PacketIdentifier::new(i as u16).unwrap());
        }
        let mut expected = Box::new([0; PacketIdentifiers::SIZE]);
        #[allow(clippy::identity_op)]
        {
            expected[1] |= 1 << 0;
        }
        assert_eq!(packet_identifiers.in_use[..], expected[..]);
    }
}
