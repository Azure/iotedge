use std::collections::HashMap;

use failure::ResultExt;
use mqtt::proto;
use tokio::sync::mpsc::{self, Receiver, Sender};
use tracing::{debug, info, span, warn, Level};
use tracing_futures::Instrument;

use crate::session::{ConnectedSession, Session, SessionState};
use crate::{ClientEvent, ClientId, ConnReq, Error, ErrorKind, Message, SystemEvent};

static EXPECTED_PROTOCOL_NAME: &str = "MQTT";
const EXPECTED_PROTOCOL_LEVEL: u8 = 0x4;

macro_rules! try_send {
    ($session:expr, $msg:expr) => {{
        if let Err(e) = $session.send($msg).await {
            warn!(message = "error processing message", error=%e);
        }
    }};
}

#[derive(Debug, Default)]
pub struct BrokerState {
    retained: HashMap<String, proto::Publication>,
    sessions: Vec<SessionState>,
}

pub struct Broker {
    sender: Sender<Message>,
    messages: Receiver<Message>,
    sessions: HashMap<ClientId, Session>,
    retained: HashMap<String, proto::Publication>,
}

impl Broker {
    pub fn new() -> Self {
        let (sender, messages) = mpsc::channel(1024);
        Self {
            sender,
            messages,
            sessions: HashMap::new(),
            retained: HashMap::new(),
        }
    }

    pub fn from_state(state: BrokerState) -> Self {
        let BrokerState { retained, sessions } = state;
        let sessions = sessions
            .into_iter()
            .map(|s| (s.client_id().clone(), Session::new_offline(s)))
            .collect::<HashMap<ClientId, Session>>();

        let (sender, messages) = mpsc::channel(1024);
        Self {
            sender,
            messages,
            sessions,
            retained,
        }
    }

    pub fn handle(&self) -> BrokerHandle {
        BrokerHandle(self.sender.clone())
    }

    pub async fn run(mut self) -> BrokerState {
        while let Some(message) = self.messages.recv().await {
            match message {
                Message::Client(client_id, event) => {
                    let span = span!(Level::INFO, "broker", client_id=%client_id);
                    if let Err(e) = self
                        .process_message(client_id, event)
                        .instrument(span)
                        .await
                    {
                        warn!(message = "an error occurred processing a message", error=%e);
                    }
                }
                Message::System(SystemEvent::Shutdown) => {
                    info!("gracefully shutting down the broker...");
                    debug!("closing sessions...");
                    if let Err(e) = self.process_shutdown().await {
                        warn!(message = "an error occurred shutting down the broker", error=%e);
                    }
                    break;
                }
                Message::System(SystemEvent::StateSnapshot(mut handle)) => {
                    let state = self.snapshot();
                    info!("asking snapshotter to persist state...");
                    if let Err(e) = handle.send(state).await {
                        warn!(message = "an error occurred communicating with the snapshotter", error=%e);
                    } else {
                        info!("sent state to snapshotter.");
                    }
                }
            }
        }

        info!("broker is shutdown.");
        self.snapshot()
    }

    fn snapshot(&self) -> BrokerState {
        let retained = self.retained.clone();
        let sessions = self
            .sessions
            .values()
            .filter_map(|session| match session {
                Session::Persistent(ref c) => Some(c.state().clone()),
                Session::Offline(ref o) => Some(o.state().clone()),
                _ => None,
            })
            .collect::<Vec<SessionState>>();

        BrokerState { retained, sessions }
    }

    async fn process_message(
        &mut self,
        client_id: ClientId,
        event: ClientEvent,
    ) -> Result<(), Error> {
        debug!("incoming: {:?}", event);
        let result = match event {
            ClientEvent::ConnReq(connreq) => self.process_connect(client_id, connreq).await,
            ClientEvent::ConnAck(_) => {
                info!("broker received CONNACK, ignoring");
                Ok(())
            }
            ClientEvent::Disconnect(_) => self.process_disconnect(client_id).await,
            ClientEvent::DropConnection => self.process_drop_connection(client_id).await,
            ClientEvent::CloseSession => self.process_close_session(client_id).await,
            ClientEvent::PingReq(ping) => self.process_ping_req(client_id, ping).await,
            ClientEvent::PingResp(_) => {
                info!("broker received PINGRESP, ignoring");
                Ok(())
            }
            ClientEvent::Subscribe(subscribe) => self.process_subscribe(client_id, subscribe).await,
            ClientEvent::SubAck(_) => {
                info!("broker received SUBACK, ignoring");
                Ok(())
            }
            ClientEvent::Unsubscribe(unsubscribe) => {
                self.process_unsubscribe(client_id, unsubscribe).await
            }
            ClientEvent::UnsubAck(_) => {
                info!("broker received UNSUBACK, ignoring");
                Ok(())
            }
            ClientEvent::PublishFrom(publish) => self.process_publish(client_id, publish).await,
            ClientEvent::PublishTo(_publish) => {
                info!("broker received a PublishTo, ignoring");
                Ok(())
            }
            ClientEvent::PubAck0(id) => self.process_puback0(client_id, id).await,
            ClientEvent::PubAck(puback) => self.process_puback(client_id, puback).await,
            ClientEvent::PubRec(pubrec) => self.process_pubrec(client_id, pubrec).await,
            ClientEvent::PubRel(pubrel) => self.process_pubrel(client_id, pubrel).await,
            ClientEvent::PubComp(pubcomp) => self.process_pubcomp(client_id, pubcomp).await,
        };

        if let Err(e) = result {
            warn!(message = "error processing message", %e);
        }

        Ok(())
    }

    async fn process_shutdown(&mut self) -> Result<(), Error> {
        let mut sessions = vec![];
        let client_ids = self.sessions.keys().cloned().collect::<Vec<ClientId>>();

        for client_id in client_ids {
            if let Some(session) = self.close_session(&client_id) {
                sessions.push(session)
            }
        }

        for mut session in sessions {
            if let Err(e) = session.send(ClientEvent::DropConnection).await {
                warn!(error=%e, message = "an error occurred closing the session", client_id = %session.client_id());
            }
        }
        Ok(())
    }

    async fn process_connect(
        &mut self,
        client_id: ClientId,
        mut connreq: ConnReq,
    ) -> Result<(), Error> {
        debug!("handling connect...");

        // [MQTT-3.1.2-1] - If the protocol name is incorrect the Server MAY
        // disconnect the Client, or it MAY continue processing the CONNECT
        // packet in accordance with some other specification.
        // In the latter case, the Server MUST NOT continue to process the
        // CONNECT packet in line with this specification.
        //
        // We will simply disconnect the client and return.
        if connreq.connect().protocol_name != EXPECTED_PROTOCOL_NAME {
            warn!(
                "invalid protocol name received from client: {}",
                connreq.connect().protocol_name
            );
            debug!("dropping connection due to invalid protocol name");
            let message = Message::Client(client_id, ClientEvent::DropConnection);
            try_send!(connreq.handle_mut(), message);
            return Ok(());
        }

        // [MQTT-3.1.2-2] - The Server MUST respond to the CONNECT Packet
        // with a CONNACK return code 0x01 (unacceptable protocol level)
        // and then disconnect the Client if the Protocol Level is not supported
        // by the Server.
        if connreq.connect().protocol_level != EXPECTED_PROTOCOL_LEVEL {
            warn!(
                "invalid protocol level received from client: {}",
                connreq.connect().protocol_level
            );
            let ack = proto::ConnAck {
                session_present: false,
                return_code: proto::ConnectReturnCode::Refused(
                    proto::ConnectionRefusedReason::UnacceptableProtocolVersion,
                ),
            };

            debug!("sending connack...");
            let event = ClientEvent::ConnAck(ack);
            let message = Message::Client(client_id.clone(), event);
            try_send!(connreq.handle_mut(), message);

            debug!("dropping connection due to invalid protocol level");
            let message = Message::Client(client_id, ClientEvent::DropConnection);
            try_send!(connreq.handle_mut(), message);
            return Ok(());
        }

        // Process the CONNECT packet after it has been validated
        // TODO - fix ConnAck return_code != accepted to not add session to sessions map
        match self.open_session(connreq) {
            Ok((ack, events)) => {
                // Send ConnAck on new session
                let session = self.get_session_mut(&client_id)?;
                session.send(ClientEvent::ConnAck(ack)).await?;

                for event in events {
                    session.send(event).await?;
                }
            }
            Err(SessionError::DuplicateSession(mut old_session, ack)) => {
                // Drop the old connection
                old_session.send(ClientEvent::DropConnection).await?;

                // Send ConnAck on new connection
                let should_drop = ack.return_code != proto::ConnectReturnCode::Accepted;
                let session = self.get_session_mut(&client_id)?;
                session.send(ClientEvent::ConnAck(ack)).await?;

                if should_drop {
                    session.send(ClientEvent::DropConnection).await?;
                }
            }
            Err(SessionError::ProtocolViolation(mut old_session)) => {
                old_session.send(ClientEvent::DropConnection).await?
            }
            Err(SessionError::PacketIdentifiersExhausted) => {
                panic!("Session identifiers exhausted, this can only be caused by a bug.");
            }
        }

        debug!("connect handled.");
        Ok(())
    }

    async fn process_disconnect(&mut self, client_id: ClientId) -> Result<(), Error> {
        debug!("handling disconnect...");
        if let Some(mut session) = self.close_session(&client_id) {
            session
                .send(ClientEvent::Disconnect(proto::Disconnect))
                .await?;
        } else {
            debug!("no session for {}", client_id);
        }
        debug!("disconnect handled.");
        Ok(())
    }

    async fn process_drop_connection(&mut self, client_id: ClientId) -> Result<(), Error> {
        debug!("handling drop connection...");
        if let Some(mut session) = self.close_session(&client_id) {
            session.send(ClientEvent::DropConnection).await?;

            // Ungraceful disconnect - send the will
            if let Some(will) = session.into_will() {
                self.publish_all(will).await?;
            }
        } else {
            debug!("no session for {}", client_id);
        }
        debug!("drop connection handled.");
        Ok(())
    }

    async fn process_close_session(&mut self, client_id: ClientId) -> Result<(), Error> {
        debug!("handling close session...");
        if let Some(session) = self.close_session(&client_id) {
            debug!("session removed");

            // Ungraceful disconnect - send the will
            if let Some(will) = session.into_will() {
                self.publish_all(will).await?;
            }
        } else {
            debug!("no session for {}", client_id);
        }
        debug!("close session handled.");
        Ok(())
    }

    async fn process_ping_req(
        &mut self,
        client_id: ClientId,
        _ping: proto::PingReq,
    ) -> Result<(), Error> {
        debug!("handling ping request...");
        match self.get_session_mut(&client_id) {
            Ok(session) => session.send(ClientEvent::PingResp(proto::PingResp)).await,
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                Ok(())
            }
            Err(e) => Err(e),
        }
    }

    async fn process_subscribe(
        &mut self,
        client_id: ClientId,
        subscribe: proto::Subscribe,
    ) -> Result<(), Error> {
        let subscriptions = match self.get_session_mut(&client_id) {
            Ok(session) => {
                let (suback, subscriptions) = session.subscribe(subscribe)?;
                session.send(ClientEvent::SubAck(suback)).await?;
                subscriptions
            }
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                return Ok(());
            }
            Err(e) => return Err(e),
        };

        // Handle retained messages
        let publications = self
            .retained
            .values()
            .filter(|p| {
                subscriptions
                    .iter()
                    .any(|sub| sub.filter().matches(&p.topic_name))
            })
            .cloned()
            .collect::<Vec<proto::Publication>>();

        match self.get_session_mut(&client_id) {
            Ok(session) => {
                for mut publication in publications {
                    publication.retain = true;
                    publish_to(session, &publication).await?;
                }
                Ok(())
            }
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                Ok(())
            }
            Err(e) => Err(e),
        }
    }

    async fn process_unsubscribe(
        &mut self,
        client_id: ClientId,
        unsubscribe: proto::Unsubscribe,
    ) -> Result<(), Error> {
        match self.get_session_mut(&client_id) {
            Ok(session) => {
                let unsuback = session.unsubscribe(&unsubscribe)?;
                session.send(ClientEvent::UnsubAck(unsuback)).await
            }
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                Ok(())
            }
            Err(e) => Err(e),
        }
    }

    async fn process_publish(
        &mut self,
        client_id: ClientId,
        publish: proto::Publish,
    ) -> Result<(), Error> {
        let maybe_publication = match self.get_session_mut(&client_id) {
            Ok(session) => {
                let (maybe_publication, maybe_event) = session.handle_publish(publish)?;
                if let Some(event) = maybe_event {
                    session.send(event).await?;
                }
                maybe_publication
            }
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                return Ok(());
            }
            Err(e) => return Err(e),
        };

        if let Some(publication) = maybe_publication {
            self.publish_all(publication).await?
        }
        Ok(())
    }

    async fn process_puback(
        &mut self,
        client_id: ClientId,
        puback: proto::PubAck,
    ) -> Result<(), Error> {
        match self.get_session_mut(&client_id) {
            Ok(session) => {
                if let Some(event) = session.handle_puback(&puback)? {
                    session.send(event).await?
                }
                Ok(())
            }
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                Ok(())
            }
            Err(e) => Err(e),
        }
    }

    async fn process_puback0(
        &mut self,
        client_id: ClientId,
        id: proto::PacketIdentifier,
    ) -> Result<(), Error> {
        match self.get_session_mut(&client_id) {
            Ok(session) => {
                if let Some(event) = session.handle_puback0(id)? {
                    session.send(event).await?
                }
                Ok(())
            }
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                Ok(())
            }
            Err(e) => Err(e),
        }
    }

    async fn process_pubrec(
        &mut self,
        client_id: ClientId,
        pubrec: proto::PubRec,
    ) -> Result<(), Error> {
        match self.get_session_mut(&client_id) {
            Ok(session) => {
                if let Some(event) = session.handle_pubrec(&pubrec)? {
                    session.send(event).await?
                }
                Ok(())
            }
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                Ok(())
            }
            Err(e) => Err(e),
        }
    }

    async fn process_pubrel(
        &mut self,
        client_id: ClientId,
        pubrel: proto::PubRel,
    ) -> Result<(), Error> {
        let maybe_publication = match self.get_session_mut(&client_id) {
            Ok(session) => {
                let packet_identifier = pubrel.packet_identifier;
                let maybe_publication = session.handle_pubrel(&pubrel)?;

                let pubcomp = proto::PubComp { packet_identifier };
                session.send(ClientEvent::PubComp(pubcomp)).await?;
                maybe_publication
            }
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                return Ok(());
            }
            Err(e) => return Err(e),
        };

        if let Some(publication) = maybe_publication {
            self.publish_all(publication).await?
        }
        Ok(())
    }

    async fn process_pubcomp(
        &mut self,
        client_id: ClientId,
        pubcomp: proto::PubComp,
    ) -> Result<(), Error> {
        match self.get_session_mut(&client_id) {
            Ok(session) => {
                if let Some(event) = session.handle_pubcomp(&pubcomp)? {
                    session.send(event).await?
                }
                Ok(())
            }
            Err(e) if *e.kind() == ErrorKind::NoSession => {
                debug!("no session for {}", client_id);
                Ok(())
            }
            Err(e) => Err(e),
        }
    }

    fn get_session_mut(&mut self, client_id: &ClientId) -> Result<&mut Session, Error> {
        self.sessions
            .get_mut(client_id)
            .ok_or_else(|| Error::new(ErrorKind::NoSession.into()))
    }

    fn open_session(
        &mut self,
        connreq: ConnReq,
    ) -> Result<(proto::ConnAck, Vec<ClientEvent>), SessionError> {
        let client_id = connreq.client_id().clone();

        match self.sessions.remove(&client_id) {
            Some(Session::Transient(current_connected)) => {
                self.open_session_connected(connreq, current_connected)
            }
            Some(Session::Persistent(current_connected)) => {
                self.open_session_connected(connreq, current_connected)
            }
            Some(Session::Offline(offline)) => {
                debug!("found an offline session for {}", client_id);

                let (new_session, events, session_present) =
                    if let proto::ClientId::IdWithExistingSession(_) = connreq.connect().client_id {
                        debug!("moving offline session to online for {}", client_id);
                        let (state, events) = offline
                            .into_online()
                            .map_err(|_e| SessionError::PacketIdentifiersExhausted)?;
                        let new_session = Session::new_persistent(connreq, state);
                        (new_session, events, true)
                    } else {
                        info!("cleaning offline session for {}", client_id);
                        let new_session = Session::new_transient(connreq);
                        (new_session, vec![], false)
                    };

                self.sessions.insert(client_id, new_session);

                let ack = proto::ConnAck {
                    session_present,
                    return_code: proto::ConnectReturnCode::Accepted,
                };

                Ok((ack, events))
            }
            Some(Session::Disconnecting(disconnecting)) => Err(SessionError::ProtocolViolation(
                Session::Disconnecting(disconnecting),
            )),
            None => {
                // No session present - create a new one.
                let new_session = if let proto::ClientId::IdWithExistingSession(_) =
                    connreq.connect().client_id
                {
                    info!("creating new persistent session for {}", client_id);
                    let state = SessionState::new(client_id.clone());
                    Session::new_persistent(connreq, state)
                } else {
                    info!("creating new transient session for {}", client_id);
                    Session::new_transient(connreq)
                };

                self.sessions.insert(client_id.clone(), new_session);

                let ack = proto::ConnAck {
                    session_present: false,
                    return_code: proto::ConnectReturnCode::Accepted,
                };
                let events = vec![];

                Ok((ack, events))
            }
        }
    }

    fn open_session_connected(
        &mut self,
        connreq: ConnReq,
        current_connected: ConnectedSession,
    ) -> Result<(proto::ConnAck, Vec<ClientEvent>), SessionError> {
        if current_connected.handle() == connreq.handle() {
            // [MQTT-3.1.0-2] - The Server MUST process a second CONNECT Packet
            // sent from a Client as a protocol violation and disconnect the Client.
            //
            // If the handles are equal, this is a second CONNECT packet on the
            // same physical connection. This condition is handled by the Connection
            // handling code. If this state gets to the broker, something is seriously broken
            // and we should just abort.

            panic!("Second CONNECT on same connection reached the broker. Connection handling logic should prevent this.");
        } else {
            // [MQTT-3.1.4-2] If the ClientId represents a Client already connected to the Server
            // then the Server MUST disconnect the existing Client.
            //
            // Send a DropConnection to the current handle.
            // Update the session to use the new handle.

            info!(
                "connection request for an in use client id ({}). closing previous connection",
                connreq.client_id()
            );

            let client_id = connreq.client_id().clone();
            let (state, _will, handle) = current_connected.into_parts();
            let old_session = Session::new_disconnecting(client_id.clone(), None, handle);
            let (new_session, session_present) =
                if let proto::ClientId::IdWithExistingSession(_) = connreq.connect().client_id {
                    debug!(
                        "moving persistent session to this connection for {}",
                        client_id
                    );
                    let new_session = Session::new_persistent(connreq, state);
                    (new_session, true)
                } else {
                    info!("cleaning session for {}", client_id);
                    let new_session = Session::new_transient(connreq);
                    (new_session, false)
                };

            self.sessions.insert(client_id, new_session);
            let ack = proto::ConnAck {
                session_present,
                return_code: proto::ConnectReturnCode::Accepted,
            };

            Err(SessionError::DuplicateSession(old_session, ack))
        }
    }

    fn close_session(&mut self, client_id: &ClientId) -> Option<Session> {
        match self.sessions.remove(client_id) {
            Some(Session::Transient(connected)) => {
                info!("closing transient session for {}", client_id);
                let (_state, will, handle) = connected.into_parts();
                Some(Session::new_disconnecting(client_id.clone(), will, handle))
            }
            Some(Session::Persistent(connected)) => {
                // Move a persistent session into the offline state
                // Return a disconnecting session to allow a disconnect
                // to be sent on the connection

                info!("moving persistent session to offline for {}", client_id);
                let (state, will, handle) = connected.into_parts();
                let new_session = Session::new_offline(state);
                self.sessions.insert(client_id.clone(), new_session);
                Some(Session::new_disconnecting(client_id.clone(), will, handle))
            }
            Some(Session::Offline(offline)) => {
                debug!("closing already offline session for {}", client_id);
                self.sessions
                    .insert(client_id.clone(), Session::Offline(offline));
                None
            }
            _ => None,
        }
    }

    async fn publish_all(&mut self, mut publication: proto::Publication) -> Result<(), Error> {
        if publication.retain {
            // [MQTT-3.3.1-6]. If the Server receives a QoS 0 message with the
            // RETAIN flag set to 1 it MUST discard any message previously
            // retained for that topic. It SHOULD store the new QoS 0 message
            // as the new retained message for that topic, but MAY choose to
            // discard it at any time - if this happens there will be no
            // retained message for that topic
            //
            // We choose to keep it
            if publication.payload.is_empty() {
                info!(
                    "removing retained message for topic \"{}\"",
                    publication.topic_name
                );
                self.retained.remove(&publication.topic_name);
            } else {
                let maybe_retained = self
                    .retained
                    .insert(publication.topic_name.to_owned(), publication.clone());
                if maybe_retained.is_none() {
                    info!(
                        "new retained message for topic \"{}\"",
                        publication.topic_name
                    );
                }
            }
        }

        // Set the retain to false. This should only be set true
        // when sending due to a new subscription.
        //
        // This will not happen here.
        publication.retain = false;

        for session in self.sessions.values_mut() {
            if let Err(e) = publish_to(session, &publication).await {
                warn!(message = "error processing message", error=%e);
            }
        }

        Ok(())
    }
}

async fn publish_to(session: &mut Session, publication: &proto::Publication) -> Result<(), Error> {
    if let Some(event) = session.publish_to(&publication)? {
        session.send(event).await?
    }
    Ok(())
}

impl Default for Broker {
    fn default() -> Self {
        Broker::new()
    }
}

#[derive(Clone, Debug)]
pub struct BrokerHandle(Sender<Message>);

impl BrokerHandle {
    pub async fn send(&mut self, message: Message) -> Result<(), Error> {
        self.0
            .send(message)
            .await
            .context(ErrorKind::SendBrokerMessage)?;
        Ok(())
    }
}

#[derive(Debug)]
pub enum SessionError {
    PacketIdentifiersExhausted,
    ProtocolViolation(Session),
    DuplicateSession(Session, proto::ConnAck),
}

#[cfg(test)]
mod tests {
    use super::*;

    use futures_util::future::FutureExt;
    use matches::assert_matches;
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

    fn persistent_connect(id: String) -> proto::Connect {
        proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithExistingSession(id),
            keep_alive: Default::default(),
            protocol_name: "MQTT".to_string(),
            protocol_level: 0x4,
        }
    }

    #[tokio::test]
    #[should_panic]
    async fn test_double_connect_protocol_violation() {
        let broker = Broker::default();
        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Default::default(),
            protocol_name: "MQTT".to_string(),
            protocol_level: 0x4,
        };
        let connect2 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Default::default(),
            protocol_name: "MQTT".to_string(),
            protocol_level: 0x4,
        };
        let id = Uuid::new_v4();
        let (tx1, mut rx1) = mpsc::channel(128);
        let conn1 = ConnectionHandle::new(id, tx1.clone());
        let conn2 = ConnectionHandle::new(id, tx1);
        let client_id = ClientId::from("blah".to_string());

        let req1 = ConnReq::new(client_id.clone(), connect1, conn1);
        let req2 = ConnReq::new(client_id.clone(), connect2, conn2);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .await
            .unwrap();
        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req2),
            ))
            .await
            .unwrap();

        assert_matches!(
            rx1.recv().await.unwrap(),
            Message::Client(_, ClientEvent::ConnAck(_))
        );
        assert_matches!(
            rx1.recv().await.unwrap(),
            Message::Client(_, ClientEvent::DropConnection)
        );
        assert!(rx1.recv().await.is_none());
    }

    #[tokio::test]
    async fn test_double_connect_drop_first_transient() {
        let broker = Broker::default();
        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Default::default(),
            protocol_name: "MQTT".to_string(),
            protocol_level: 0x4,
        };
        let connect2 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Default::default(),
            protocol_name: "MQTT".to_string(),
            protocol_level: 0x4,
        };
        let (tx1, mut rx1) = mpsc::channel(128);
        let (tx2, mut rx2) = mpsc::channel(128);
        let conn1 = ConnectionHandle::from_sender(tx1);
        let conn2 = ConnectionHandle::from_sender(tx2);
        let client_id = ClientId::from("blah".to_string());

        let req1 = ConnReq::new(client_id.clone(), connect1, conn1);
        let req2 = ConnReq::new(client_id.clone(), connect2, conn2);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .await
            .unwrap();
        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req2),
            ))
            .await
            .unwrap();

        assert_matches!(
            rx1.recv().await.unwrap(),
            Message::Client(_, ClientEvent::ConnAck(_))
        );
        assert_matches!(
            rx1.recv().await.unwrap(),
            Message::Client(_, ClientEvent::DropConnection)
        );
        assert!(rx1.recv().await.is_none());

        assert_matches!(
            rx2.recv().await.unwrap(),
            Message::Client(_, ClientEvent::ConnAck(_))
        );
    }

    #[tokio::test]
    async fn test_invalid_protocol_name() {
        let broker = Broker::default();
        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Default::default(),
            protocol_name: "AMQP".to_string(),
            protocol_level: 0x4,
        };
        let (tx1, mut rx1) = mpsc::channel(128);
        let conn1 = ConnectionHandle::from_sender(tx1);
        let client_id = ClientId::from("blah".to_string());
        let req1 = ConnReq::new(client_id.clone(), connect1, conn1);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .await
            .unwrap();

        assert_matches!(
            rx1.recv().await.unwrap(),
            Message::Client(_, ClientEvent::DropConnection)
        );
        assert!(rx1.recv().await.is_none());
    }

    #[tokio::test]
    async fn test_invalid_protocol_level() {
        let broker = Broker::default();
        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Default::default(),
            protocol_name: "MQTT".to_string(),
            protocol_level: 0x3,
        };
        let (tx1, mut rx1) = mpsc::channel(128);
        let conn1 = ConnectionHandle::from_sender(tx1);
        let client_id = ClientId::from("blah".to_string());
        let req1 = ConnReq::new(client_id.clone(), connect1, conn1);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .await
            .unwrap();

        assert_matches!(
            rx1.recv().await.unwrap(),
            Message::Client(_, ClientEvent::ConnAck(proto::ConnAck {
                return_code:
                    proto::ConnectReturnCode::Refused(
                        proto::ConnectionRefusedReason::UnacceptableProtocolVersion,
                    ),
                ..
            }))
        );
        assert_matches!(
            rx1.recv().await.unwrap(),
            Message::Client(_, ClientEvent::DropConnection)
        );
        assert!(rx1.recv().await.is_none());
    }

    #[test]
    fn test_add_session_empty_transient() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect = transient_connect(id.clone());
        let handle = connection_handle();
        let req = ConnReq::new(client_id.clone(), connect, handle);

        broker.open_session(req).unwrap();

        // check new session
        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Transient(_));

        // close session and check behavior
        let old_session = broker.close_session(&client_id);
        assert_matches!(old_session, Some(Session::Disconnecting(_)));
        assert_eq!(0, broker.sessions.len());
    }

    #[test]
    fn test_add_session_empty_persistent() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect = persistent_connect(id.clone());
        let handle = connection_handle();
        let req = ConnReq::new(client_id.clone(), connect, handle);

        broker.open_session(req).unwrap();

        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));

        // close session and check behavior
        let old_session = broker.close_session(&client_id);
        assert_matches!(old_session, Some(Session::Disconnecting(_)));
        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Offline(_));
    }

    #[test]
    #[should_panic]
    fn test_add_session_same_connection_transient() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id.clone());
        let connect2 = transient_connect(id.clone());
        let id = Uuid::new_v4();
        let (tx1, _rx1) = mpsc::channel(128);
        let handle1 = ConnectionHandle::new(id.clone(), tx1.clone());
        let handle2 = ConnectionHandle::new(id, tx1);

        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);
        let req2 = ConnReq::new(client_id.clone(), connect2, handle2);

        broker.open_session(req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let result = broker.open_session(req2);
        assert_matches!(result, Err(SessionError::ProtocolViolation(_)));
        assert_eq!(0, broker.sessions.len());
    }

    #[test]
    #[should_panic]
    fn test_add_session_same_connection_persistent() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let connect2 = persistent_connect(id.clone());
        let id = Uuid::new_v4();
        let (tx1, _rx1) = mpsc::channel(128);
        let handle1 = ConnectionHandle::new(id.clone(), tx1.clone());
        let handle2 = ConnectionHandle::new(id, tx1);

        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);
        let req2 = ConnReq::new(client_id.clone(), connect2, handle2);

        broker.open_session(req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let result = broker.open_session(req2);
        assert_matches!(result, Err(SessionError::ProtocolViolation(_)));
        assert_eq!(0, broker.sessions.len());
    }

    #[test]
    fn test_add_session_different_connection_transient_then_transient() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id.clone());
        let connect2 = transient_connect(id.clone());
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);

        broker.open_session(req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let req2 = ConnReq::new(client_id.clone(), connect2, handle2);
        let result = broker.open_session(req2);
        assert_matches!(result, Err(SessionError::DuplicateSession(_, _)));
        assert_matches!(broker.sessions[&client_id], Session::Transient(_));
        assert_eq!(1, broker.sessions.len());
    }

    #[test]
    fn test_add_session_different_connection_transient_then_persistent() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id.clone());
        let connect2 = persistent_connect(id.clone());
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);

        broker.open_session(req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let req2 = ConnReq::new(client_id.clone(), connect2, handle2);
        let result = broker.open_session(req2);
        assert_matches!(result, Err(SessionError::DuplicateSession(_, _)));
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));
        assert_eq!(1, broker.sessions.len());
    }

    #[test]
    fn test_add_session_different_connection_persistent_then_transient() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let connect2 = transient_connect(id.clone());
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);

        broker.open_session(req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let req2 = ConnReq::new(client_id.clone(), connect2, handle2);
        let result = broker.open_session(req2);
        assert_matches!(result, Err(SessionError::DuplicateSession(_, _)));
        assert_matches!(broker.sessions[&client_id], Session::Transient(_));
        assert_eq!(1, broker.sessions.len());
    }

    #[test]
    fn test_add_session_different_connection_persistent_then_persistent() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let connect2 = persistent_connect(id.clone());
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);
        let req2 = ConnReq::new(client_id.clone(), connect2, handle2);

        broker.open_session(req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let result = broker.open_session(req2);
        assert_matches!(result, Err(SessionError::DuplicateSession(_, _)));
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));
        assert_eq!(1, broker.sessions.len());
    }

    #[test]
    fn test_add_session_offline_persistent() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let connect2 = persistent_connect(id.clone());
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);
        let req2 = ConnReq::new(client_id.clone(), connect2, handle2);

        broker.open_session(req1).unwrap();

        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));

        // close session and check behavior
        let old_session = broker.close_session(&client_id);
        assert_matches!(old_session, Some(Session::Disconnecting(_)));
        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Offline(_));

        // Reopen session
        broker.open_session(req2).unwrap();

        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));
    }

    #[test]
    fn test_add_session_offline_transient() {
        let id = "id1".to_string();
        let mut broker = Broker::default();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, handle1);

        broker.open_session(req1).unwrap();

        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));

        // close session and check behavior
        let old_session = broker.close_session(&client_id);
        assert_matches!(old_session, Some(Session::Disconnecting(_)));
        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Offline(_));

        // Reopen session
        let connect2 = transient_connect(id.clone());
        let req2 = ConnReq::new(client_id.clone(), connect2, handle2);
        broker.open_session(req2).unwrap();

        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Transient(_));
    }
}
