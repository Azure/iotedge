use std::collections::HashMap;
use std::{panic, thread};

use crossbeam_channel::{Receiver, Sender};
use mqtt3::proto;
use serde::{Deserialize, Serialize};
use tracing::{debug, error, info, span, warn, Level};

use crate::auth::{
    Activity, Authenticator, Authorizer, Credentials, DefaultAuthenticator, DefaultAuthorizer,
    Operation,
};
use crate::session::{ConnectedSession, Session, SessionState};
use crate::{
    subscription::Subscription, AuthId, ClientEvent, ClientId, ConnReq, Error, Message, SystemEvent,
};

static EXPECTED_PROTOCOL_NAME: &str = mqtt3::PROTOCOL_NAME;
const EXPECTED_PROTOCOL_LEVEL: u8 = mqtt3::PROTOCOL_LEVEL;

macro_rules! try_send {
    ($session:expr, $msg:expr) => {{
        if let Err(e) = $session.send($msg) {
            warn!(message = "error processing message", error = %e);
        }
    }};
}

pub struct Broker<N, Z> {
    sender: Sender<Message>,
    messages: Receiver<Message>,
    sessions: HashMap<ClientId, Session>,
    retained: HashMap<String, proto::Publication>,
    authenticator: N,
    authorizer: Z,

    #[cfg(feature = "__internal_broker_callbacks")]
    pub on_publish: Option<Sender<()>>,
}

impl<N, Z> Broker<N, Z>
where
    N: Authenticator + Send + 'static,
    Z: Authorizer + Send + 'static,
{
    pub fn handle(&self) -> BrokerHandle {
        BrokerHandle(self.sender.clone())
    }

    pub async fn run(self) -> Result<BrokerState, Error> {
        // There is a dance here between a `oneshot` and a thread join.
        //
        // This `run` function needs to do two things:
        //   1. Be `async` so that it can be joined with the other tasks in the `server`
        //   2. Handle and propagate any panics on the thread to the event loop thread
        //      so that the process exits/crashes properly. We don't want this thread
        //      to die and have the process still running, not able to do anything.
        //
        // The `oneshot` is used to make this function `async`. It is used by the thread
        // to signal that it has exited, so that the `run` function will return.
        // The nice thing about a `oneshot` is that the receiver will complete if the sender
        // is dropped. This allows a panic in the broker loop to gracefully signal the event
        // loop that it has exited.
        //
        // The `handle.join()` is used to propagate the panic from the broker thread
        // to the thread that is handling the async task.

        let (tx, rx) = tokio::sync::oneshot::channel::<()>();
        let handle = thread::Builder::new()
            .name("mqtt::broker".to_string())
            .spawn(|| {
                let state = self.broker_loop();
                if let Err(_e) = tx.send(()) {
                    error!("failed to signal the event loop that the broker thread is exiting");
                }
                state
            })
            .expect("failed to spawn broker thread");

        // Wait for the thread to exit
        // This rx will complete for two reasons:
        //   1. The thread gracefully shutdown and sent on the tx.
        //   2. The thread panicked and the tx was dropped.
        //
        // We don't really care about the error here ---
        //   we just want to join the thread handle to get the result
        //   or deal with the panic
        let _ = rx.await;

        // propagate any panics onto the event loop thread
        match handle.join() {
            Ok(state) => Ok(state),
            Err(e) => panic::resume_unwind(e),
        }
    }

    fn broker_loop(mut self) -> BrokerState {
        while let Ok(message) = self.messages.recv() {
            match message {
                Message::Client(client_id, event) => {
                    let span = span!(Level::INFO, "broker", client_id = %client_id, event="client");
                    let _enter = span.enter();
                    if let Err(e) = self.process_message(client_id, event) {
                        warn!(message = "an error occurred processing a message", error = %e);
                    }
                }
                Message::System(event) => {
                    let span = span!(Level::INFO, "broker", event = "system");
                    let _enter = span.enter();
                    match event {
                        SystemEvent::Shutdown => {
                            info!("gracefully shutting down the broker...");
                            debug!("closing sessions...");
                            if let Err(e) = self.process_shutdown() {
                                warn!(message = "an error occurred shutting down the broker", error = %e);
                            }
                            break;
                        }
                        SystemEvent::StateSnapshot(mut handle) => {
                            let state = self.snapshot();
                            let _guard = span.enter();
                            info!("asking snapshotter to persist state...");
                            if let Err(e) = handle.try_send(state) {
                                warn!(message = "an error occurred communicating with the snapshotter", error = %e);
                            } else {
                                info!("sent state to snapshotter.");
                            }
                        }
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
                Session::Persistent(c) => Some(c.state().clone()),
                Session::Offline(o) => Some(o.state().clone()),
                _ => None,
            })
            .collect::<Vec<SessionState>>();

        BrokerState { retained, sessions }
    }

    fn process_message(&mut self, client_id: ClientId, event: ClientEvent) -> Result<(), Error> {
        debug!("incoming: {:?}", event);
        let result = match event {
            ClientEvent::ConnReq(connreq) => self.process_connect(client_id, connreq),
            ClientEvent::ConnAck(_) => {
                info!("broker received CONNACK, ignoring");
                Ok(())
            }
            ClientEvent::Disconnect(_) => self.process_disconnect(&client_id),
            ClientEvent::DropConnection => self.process_drop_connection(&client_id),
            ClientEvent::CloseSession => self.process_close_session(&client_id),
            ClientEvent::PingReq(ping) => self.process_ping_req(&client_id, &ping),
            ClientEvent::PingResp(_) => {
                info!("broker received PINGRESP, ignoring");
                Ok(())
            }
            ClientEvent::Subscribe(subscribe) => self.process_subscribe(&client_id, subscribe),
            ClientEvent::SubAck(_) => {
                info!("broker received SUBACK, ignoring");
                Ok(())
            }
            ClientEvent::Unsubscribe(unsubscribe) => {
                self.process_unsubscribe(&client_id, &unsubscribe)
            }
            ClientEvent::UnsubAck(_) => {
                info!("broker received UNSUBACK, ignoring");
                Ok(())
            }
            ClientEvent::PublishFrom(publish) => self.process_publish(&client_id, publish),
            ClientEvent::PublishTo(_publish) => {
                info!("broker received a PublishTo, ignoring");
                Ok(())
            }
            ClientEvent::PubAck0(id) => self.process_puback0(&client_id, id),
            ClientEvent::PubAck(puback) => self.process_puback(&client_id, &puback),
            ClientEvent::PubRec(pubrec) => self.process_pubrec(&client_id, &pubrec),
            ClientEvent::PubRel(pubrel) => self.process_pubrel(&client_id, &pubrel),
            ClientEvent::PubComp(pubcomp) => self.process_pubcomp(&client_id, &pubcomp),
        };

        if let Err(e) = result {
            warn!(message = "error processing message", %e);
        }

        Ok(())
    }

    fn process_shutdown(&mut self) -> Result<(), Error> {
        let mut sessions = vec![];
        let client_ids = self.sessions.keys().cloned().collect::<Vec<ClientId>>();

        for client_id in client_ids {
            if let Some(session) = self.close_session(&client_id) {
                sessions.push(session)
            }
        }

        for mut session in sessions {
            if let Err(e) = session.send(ClientEvent::DropConnection) {
                warn!(error = %e, message = "an error occurred closing the session", client_id = %session.client_id());
            }
        }
        Ok(())
    }

    #[allow(clippy::too_many_lines)]
    fn process_connect(&mut self, client_id: ClientId, mut connreq: ConnReq) -> Result<(), Error> {
        debug!("handling connect...");

        macro_rules! refuse_connection {
            ($reason:expr) => {
                let ack = proto::ConnAck {
                    session_present: false,
                    return_code: proto::ConnectReturnCode::Refused($reason),
                };

                debug!("sending connack with: {:?}", ack.return_code);
                let event = ClientEvent::ConnAck(ack);
                let message = Message::Client(client_id.clone(), event);
                try_send!(connreq.handle_mut(), message);

                debug!("dropping connection due to authentication error");
                let message = Message::Client(client_id, ClientEvent::DropConnection);
                try_send!(connreq.handle_mut(), message);
            };
        };

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
            refuse_connection!(proto::ConnectionRefusedReason::UnacceptableProtocolVersion);
            return Ok(());
        }

        // [MQTT-3.1.4-3] - The Server MAY check that the contents of the CONNECT
        // Packet meet any further restrictions and MAY perform authentication
        // and authorization checks. If any of these checks fail, it SHOULD send an
        // appropriate CONNACK response with a non-zero return code as described in
        // section 3.2 and it MUST close the Network Connection.
        let credentials = connreq.certificate().map_or(
            Credentials::Basic(
                connreq.connect().username.clone(),
                connreq.connect().password.clone(),
            ),
            |certificate| Credentials::ClientCertificate(certificate.clone()),
        );
        let auth_id = match self.authenticator.authenticate(credentials) {
            Ok(Some(auth_id)) => {
                debug!(
                    "client {} successfully authenticated: {}",
                    client_id, auth_id
                );
                auth_id
            }
            Ok(None) => {
                warn!("unable to authenticate client: {}", client_id);
                refuse_connection!(proto::ConnectionRefusedReason::BadUserNameOrPassword);
                return Ok(());
            }
            Err(e) => {
                warn!(message = "error authenticating client: {}", error = %e);
                refuse_connection!(proto::ConnectionRefusedReason::ServerUnavailable);
                return Ok(());
            }
        };

        // Check client permissions to connect
        let operation = Operation::new_connect(connreq.connect().clone());
        let activity = Activity::new(auth_id.clone(), client_id.clone(), operation);
        match self.authorizer.authorize(activity) {
            Ok(true) => {
                debug!("client {} successfully authorized", client_id);
            }
            Ok(false) => {
                warn!("client {} not allowed to connect", client_id);
                refuse_connection!(proto::ConnectionRefusedReason::NotAuthorized);
                return Ok(());
            }
            Err(e) => {
                warn!(message="error authorizing client: {}", error = %e);
                refuse_connection!(proto::ConnectionRefusedReason::ServerUnavailable);
                return Ok(());
            }
        }

        // Process the CONNECT packet after it has been validated
        // TODO - fix ConnAck return_code != accepted to not add session to sessions map
        match self.open_session(auth_id, connreq) {
            Ok((ack, events)) => {
                // Send ConnAck on new session
                let session = self
                    .get_session_mut(&client_id)
                    .expect("session must exist");
                session.send(ClientEvent::ConnAck(ack))?;

                for event in events {
                    session.send(event)?;
                }
            }
            Err(SessionError::DuplicateSession(mut old_session, ack)) => {
                // Drop the old connection
                old_session.send(ClientEvent::DropConnection)?;

                // Send ConnAck on new connection
                let should_drop = ack.return_code != proto::ConnectReturnCode::Accepted;
                let session = self
                    .get_session_mut(&client_id)
                    .expect("session must exist");
                session.send(ClientEvent::ConnAck(ack))?;

                if should_drop {
                    session.send(ClientEvent::DropConnection)?;
                }
            }
            Err(SessionError::ProtocolViolation(mut old_session)) => {
                old_session.send(ClientEvent::DropConnection)?
            }
            Err(SessionError::PacketIdentifiersExhausted) => {
                panic!("Session identifiers exhausted, this can only be caused by a bug.");
            }
        }

        debug!("connect handled.");
        Ok(())
    }

    fn process_disconnect(&mut self, client_id: &ClientId) -> Result<(), Error> {
        debug!("handling disconnect...");
        if let Some(mut session) = self.close_session(client_id) {
            session.send(ClientEvent::Disconnect(proto::Disconnect))?;
        } else {
            debug!("no session for {}", client_id);
        }
        debug!("disconnect handled.");
        Ok(())
    }

    fn process_drop_connection(&mut self, client_id: &ClientId) -> Result<(), Error> {
        self.drop_connection(client_id)
    }

    fn drop_connection(&mut self, client_id: &ClientId) -> Result<(), Error> {
        debug!("handling drop connection...");
        if let Some(mut session) = self.close_session(client_id) {
            session.send(ClientEvent::DropConnection)?;

            // Ungraceful disconnect - send the will
            if let Some(will) = session.into_will() {
                self.publish_all(will)?;
            }
        } else {
            debug!("no session for {}", client_id);
        }
        debug!("drop connection handled.");
        Ok(())
    }

    fn process_close_session(&mut self, client_id: &ClientId) -> Result<(), Error> {
        debug!("handling close session...");
        if let Some(session) = self.close_session(client_id) {
            debug!("session removed");

            // Ungraceful disconnect - send the will
            if let Some(will) = session.into_will() {
                self.publish_all(will)?;
            }
        } else {
            debug!("no session for {}", client_id);
        }
        debug!("close session handled.");
        Ok(())
    }

    fn process_ping_req(
        &mut self,
        client_id: &ClientId,
        _ping: &proto::PingReq,
    ) -> Result<(), Error> {
        debug!("handling ping request...");
        match self.get_session_mut(client_id) {
            Ok(session) => session.send(ClientEvent::PingResp(proto::PingResp)),
            Err(NoSessionError) => {
                debug!("no session for {}", client_id);
                Ok(())
            }
        }
    }

    fn process_subscribe(
        &mut self,
        client_id: &ClientId,
        sub: proto::Subscribe,
    ) -> Result<(), Error> {
        let subscriptions = if let Some(session) = self.sessions.get_mut(client_id) {
            let (suback, subscriptions) = subscribe(&self.authorizer, session, sub)?;
            session.send(ClientEvent::SubAck(suback))?;
            subscriptions
        } else {
            debug!("no session for {}", client_id);
            return Ok(());
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

        if let Some(session) = self.sessions.get_mut(client_id) {
            for mut publication in publications {
                publication.retain = true;
                publish_to(&self.authorizer, session, &publication)?;
            }
        } else {
            debug!("no session for {}", client_id);
        }

        Ok(())
    }

    fn process_unsubscribe(
        &mut self,
        client_id: &ClientId,
        unsubscribe: &proto::Unsubscribe,
    ) -> Result<(), Error> {
        match self.get_session_mut(client_id) {
            Ok(session) => {
                let unsuback = session.unsubscribe(unsubscribe)?;
                session.send(ClientEvent::UnsubAck(unsuback))
            }
            Err(NoSessionError) => {
                debug!("no session for {}", client_id);
                Ok(())
            }
        }
    }

    fn process_publish(
        &mut self,
        client_id: &ClientId,
        publish: proto::Publish,
    ) -> Result<(), Error> {
        let operation = Operation::new_publish(publish.clone());
        if let Some(session) = self.sessions.get_mut(client_id) {
            let activity = Activity::new(session.auth_id()?.clone(), client_id.clone(), operation);
            match self.authorizer.authorize(activity) {
                Ok(true) => {
                    debug!("client {} successfully authorized", client_id);
                    let (maybe_publication, maybe_event) = session.handle_publish(publish)?;

                    if let Some(event) = maybe_event {
                        session.send(event)?;
                    }

                    if let Some(publication) = maybe_publication {
                        self.publish_all(publication)?
                    }
                }
                Ok(false) => {
                    warn!(
                        "client {} not allowed to publish to topic {}",
                        client_id, publish.topic_name,
                    );
                    self.drop_connection(&client_id)?;
                }
                Err(e) => {
                    warn!(message="error authorizing client: {}", error = %e);
                    self.drop_connection(&client_id)?;
                }
            }
        } else {
            debug!("no session for {}", client_id);
        }

        #[cfg(feature = "__internal_broker_callbacks")]
        {
            if let Some(on_publish) = &mut self.on_publish {
                on_publish.send(()).expect("on_publish");
            }
        }

        Ok(())
    }

    fn process_puback(
        &mut self,
        client_id: &ClientId,
        puback: &proto::PubAck,
    ) -> Result<(), Error> {
        match self.get_session_mut(client_id) {
            Ok(session) => {
                if let Some(event) = session.handle_puback(puback)? {
                    session.send(event)?
                }
                Ok(())
            }
            Err(NoSessionError) => {
                debug!("no session for {}", client_id);
                Ok(())
            }
        }
    }

    fn process_puback0(
        &mut self,
        client_id: &ClientId,
        id: proto::PacketIdentifier,
    ) -> Result<(), Error> {
        match self.get_session_mut(client_id) {
            Ok(session) => {
                if let Some(event) = session.handle_puback0(id)? {
                    session.send(event)?
                }
                Ok(())
            }
            Err(NoSessionError) => {
                debug!("no session for {}", client_id);
                Ok(())
            }
        }
    }

    fn process_pubrec(
        &mut self,
        client_id: &ClientId,
        pubrec: &proto::PubRec,
    ) -> Result<(), Error> {
        match self.get_session_mut(client_id) {
            Ok(session) => {
                if let Some(event) = session.handle_pubrec(pubrec)? {
                    session.send(event)?
                }
                Ok(())
            }
            Err(NoSessionError) => {
                debug!("no session for {}", client_id);
                Ok(())
            }
        }
    }

    fn process_pubrel(
        &mut self,
        client_id: &ClientId,
        pubrel: &proto::PubRel,
    ) -> Result<(), Error> {
        let maybe_publication = match self.get_session_mut(client_id) {
            Ok(session) => {
                let packet_identifier = pubrel.packet_identifier;
                let maybe_publication = session.handle_pubrel(pubrel)?;

                let pubcomp = proto::PubComp { packet_identifier };
                session.send(ClientEvent::PubComp(pubcomp))?;
                maybe_publication
            }
            Err(NoSessionError) => {
                debug!("no session for {}", client_id);
                return Ok(());
            }
        };

        if let Some(publication) = maybe_publication {
            self.publish_all(publication)?
        }
        Ok(())
    }

    fn process_pubcomp(
        &mut self,
        client_id: &ClientId,
        pubcomp: &proto::PubComp,
    ) -> Result<(), Error> {
        match self.get_session_mut(client_id) {
            Ok(session) => {
                if let Some(event) = session.handle_pubcomp(pubcomp)? {
                    session.send(event)?
                }
                Ok(())
            }
            Err(NoSessionError) => {
                debug!("no session for {}", client_id);
                Ok(())
            }
        }
    }

    fn get_session_mut(&mut self, client_id: &ClientId) -> Result<&mut Session, NoSessionError> {
        self.sessions
            .get_mut(client_id)
            .ok_or_else(|| NoSessionError)
    }

    fn open_session(
        &mut self,
        auth_id: AuthId,
        connreq: ConnReq,
    ) -> Result<(proto::ConnAck, Vec<ClientEvent>), SessionError> {
        let client_id = connreq.client_id().clone();

        match self.sessions.remove(&client_id) {
            Some(Session::Transient(current_connected)) => {
                self.open_session_connected(auth_id, connreq, current_connected)
            }
            Some(Session::Persistent(current_connected)) => {
                self.open_session_connected(auth_id, connreq, current_connected)
            }
            Some(Session::Offline(offline)) => {
                debug!("found an offline session for {}", client_id);

                let (new_session, events, session_present) =
                    if let proto::ClientId::IdWithExistingSession(_) = connreq.connect().client_id {
                        debug!("moving offline session to online for {}", client_id);
                        let (state, events) = offline
                            .into_online()
                            .map_err(|_| SessionError::PacketIdentifiersExhausted)?;
                        let new_session = Session::new_persistent(auth_id, connreq, state);
                        (new_session, events, true)
                    } else {
                        info!("cleaning offline session for {}", client_id);
                        let new_session = Session::new_transient(auth_id, connreq);
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
                    Session::new_persistent(auth_id, connreq, state)
                } else {
                    info!("creating new transient session for {}", client_id);
                    Session::new_transient(auth_id, connreq)
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
        auth_id: AuthId,
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
            let (auth_id_, state, _will, handle) = current_connected.into_parts();
            let old_session = Session::new_disconnecting(auth_id_, client_id.clone(), None, handle);
            let (new_session, session_present) =
                if let proto::ClientId::IdWithExistingSession(_) = connreq.connect().client_id {
                    debug!(
                        "moving persistent session to this connection for {}",
                        client_id
                    );
                    let new_session = Session::new_persistent(auth_id, connreq, state);
                    (new_session, true)
                } else {
                    info!("cleaning session for {}", client_id);
                    let new_session = Session::new_transient(auth_id, connreq);
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
                let (auth_id, _state, will, handle) = connected.into_parts();
                Some(Session::new_disconnecting(
                    auth_id,
                    client_id.clone(),
                    will,
                    handle,
                ))
            }
            Some(Session::Persistent(connected)) => {
                // Move a persistent session into the offline state
                // Return a disconnecting session to allow a disconnect
                // to be sent on the connection

                info!("moving persistent session to offline for {}", client_id);
                let (auth_id, state, will, handle) = connected.into_parts();
                let new_session = Session::new_offline(state);
                self.sessions.insert(client_id.clone(), new_session);
                Some(Session::new_disconnecting(
                    auth_id,
                    client_id.clone(),
                    will,
                    handle,
                ))
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

    fn publish_all(&mut self, mut publication: proto::Publication) -> Result<(), Error> {
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
            if let Err(e) = publish_to(&self.authorizer, session, &publication) {
                warn!(message = "error processing message", error = %e);
            }
        }

        Ok(())
    }
}

fn subscribe<Z>(
    authorizer: &Z,
    session: &mut Session,
    subscribe: proto::Subscribe,
) -> Result<(proto::SubAck, Vec<Subscription>), Error>
where
    Z: Authorizer,
{
    let auth_id = session.auth_id()?.clone();
    let client_id = session.client_id().clone();

    let mut subscriptions = Vec::with_capacity(subscribe.subscribe_to.len());
    let mut acks = Vec::with_capacity(subscribe.subscribe_to.len());

    let auth_results = subscribe.subscribe_to.into_iter().map(|subscribe_to| {
        let operation = Operation::new_subscribe(subscribe_to.clone());
        let activity = Activity::new(auth_id.clone(), client_id.clone(), operation);
        let auth = authorizer.authorize(activity);
        auth.map(|auth| (auth, subscribe_to))
    });

    for auth in auth_results {
        let ack_qos = match auth {
            Ok((true, subscribe_to)) => match session.subscribe_to(subscribe_to) {
                Ok((qos, subscription)) => {
                    if let Some(subscription) = subscription {
                        subscriptions.push(subscription);
                    }
                    qos
                }
                Err(e) => {
                    warn!(message="error subscribing to a topic: {}", error = %e);
                    proto::SubAckQos::Failure
                }
            },
            Ok((false, subscribe_to)) => {
                debug!(
                    "client {} not allowed to subscribe to topic {} qos {}",
                    client_id,
                    subscribe_to.topic_filter,
                    u8::from(subscribe_to.qos)
                );
                proto::SubAckQos::Failure
            }
            Err(e) => {
                warn!(message="error authorizing client subscription: {}", error = %e);
                proto::SubAckQos::Failure
            }
        };
        acks.push(ack_qos);
    }

    let suback = proto::SubAck {
        packet_identifier: subscribe.packet_identifier,
        qos: acks,
    };

    Ok((suback, subscriptions))
}

fn publish_to<Z>(
    authorizer: &Z,
    session: &mut Session,
    publication: &proto::Publication,
) -> Result<(), Error>
where
    Z: Authorizer,
{
    let operation = Operation::new_receive(publication.clone());
    let client_id = session.client_id().clone();
    // TODO refactor auth_id logic for offline sessions
    let auth_id = session.auth_id().unwrap_or(&AuthId::Anonymous);
    let activity = Activity::new(auth_id.clone(), client_id, operation);

    match authorizer.authorize(activity) {
        Ok(true) => {
            if let Some(event) = session.publish_to(&publication)? {
                session.send(event)?
            }
        }
        Ok(false) => {
            debug!(
                "client {} not allowed to receive messages",
                session.client_id()
            );
        }
        Err(e) => {
            warn!(message="error authorizing client: {}", error = %e);
        }
    }
    Ok(())
}

#[derive(Clone, Debug, Default, PartialEq, Deserialize, Serialize)]
pub struct BrokerState {
    retained: HashMap<String, proto::Publication>,
    sessions: Vec<SessionState>,
}

impl BrokerState {
    pub fn new(retained: HashMap<String, proto::Publication>, sessions: Vec<SessionState>) -> Self {
        Self { retained, sessions }
    }

    pub fn into_parts(self) -> (HashMap<String, proto::Publication>, Vec<SessionState>) {
        (self.retained, self.sessions)
    }
}

pub struct BrokerBuilder<N, Z> {
    state: Option<BrokerState>,
    authenticator: N,
    authorizer: Z,
}

impl Default for BrokerBuilder<DefaultAuthenticator, DefaultAuthorizer> {
    fn default() -> Self {
        Self {
            state: None,
            authenticator: DefaultAuthenticator,
            authorizer: DefaultAuthorizer,
        }
    }
}

impl<N, Z> BrokerBuilder<N, Z>
where
    N: Authenticator,
    Z: Authorizer,
{
    pub fn authenticator<N1>(self, authenticator: N1) -> BrokerBuilder<N1, Z>
    where
        N1: Authenticator,
    {
        BrokerBuilder {
            state: self.state,
            authenticator,
            authorizer: self.authorizer,
        }
    }

    pub fn authorizer<Z1>(self, authorizer: Z1) -> BrokerBuilder<N, Z1>
    where
        Z1: Authorizer,
    {
        BrokerBuilder {
            state: self.state,
            authenticator: self.authenticator,
            authorizer,
        }
    }

    pub fn state(mut self, state: BrokerState) -> Self {
        self.state = Some(state);
        self
    }

    pub fn build(self) -> Broker<N, Z> {
        let (retained, sessions) = match self.state {
            Some(state) => {
                let sessions = state
                    .sessions
                    .into_iter()
                    .map(|s| (s.client_id().clone(), Session::new_offline(s)))
                    .collect::<HashMap<ClientId, Session>>();
                (state.retained, sessions)
            }
            None => (HashMap::default(), HashMap::default()),
        };

        let (sender, messages) = crossbeam_channel::bounded(1024);

        Broker {
            sender,
            messages,
            sessions,
            retained,
            authenticator: self.authenticator,
            authorizer: self.authorizer,

            #[cfg(feature = "__internal_broker_callbacks")]
            on_publish: None,
        }
    }
}

#[derive(Clone, Debug)]
pub struct BrokerHandle(Sender<Message>);

impl BrokerHandle {
    pub fn send(&mut self, message: Message) -> Result<(), Error> {
        self.0
            .send(message)
            .map_err(|e| Error::SendBrokerMessage(e.into()))
    }

    pub fn try_send(&mut self, message: Message) -> Result<(), Error> {
        self.0.try_send(message).map_err(Error::SendBrokerMessage)
    }
}

#[derive(Debug)]
pub enum SessionError {
    PacketIdentifiersExhausted,
    ProtocolViolation(Session),
    DuplicateSession(Session, proto::ConnAck),
}

#[derive(Debug, thiserror::Error)]
#[error("No session.")]
pub struct NoSessionError;

#[cfg(test)]
pub(crate) mod tests {
    use std::time::Duration;

    use bytes::Bytes;
    use futures_util::future::FutureExt;
    use matches::assert_matches;
    use proptest::collection::{hash_map, vec};
    use proptest::prelude::*;
    use tokio::sync::mpsc::error::TryRecvError;
    use tokio::sync::mpsc::{self, UnboundedReceiver};
    use uuid::Uuid;

    use mqtt3::{proto, PROTOCOL_LEVEL, PROTOCOL_NAME};

    use crate::{
        auth::{Activity, AuthenticateError, AuthorizeError, Operation},
        broker::{BrokerBuilder, BrokerHandle, BrokerState, SessionError},
        error::Error,
        session::{tests::arb_session_state, Session},
        tests::{arb_publication, arb_topic},
        AuthId, ClientEvent, ClientId, ConnReq, ConnectionHandle, Message,
    };

    prop_compose! {
        pub fn arb_broker_state()(
            retained in hash_map(arb_topic(), arb_publication(), 0..20),
            sessions in vec(arb_session_state(), 0..10),
        ) -> BrokerState {
            BrokerState {
                retained,
                sessions,
            }
        }
    }

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

    fn persistent_connect(id: String) -> proto::Connect {
        proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithExistingSession(id),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        }
    }

    #[tokio::test]
    #[should_panic]
    async fn test_double_connect_protocol_violation() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };
        let connect2 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };
        let id = Uuid::new_v4();
        let (tx1, mut rx1) = mpsc::unbounded_channel();
        let conn1 = ConnectionHandle::new(id, tx1.clone());
        let conn2 = ConnectionHandle::new(id, tx1);
        let client_id = ClientId::from("blah".to_string());

        let req1 = ConnReq::new(client_id.clone(), connect1, None, conn1);
        let req2 = ConnReq::new(client_id.clone(), connect2, None, conn2);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .unwrap();
        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req2),
            ))
            .unwrap();

        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::ConnAck(_)))
        );
        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::DropConnection))
        );
        assert_matches!(rx1.recv().await, None)
    }

    #[tokio::test]
    async fn test_double_connect_drop_first_transient() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };
        let connect2 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };
        let (tx1, mut rx1) = mpsc::unbounded_channel();
        let (tx2, mut rx2) = mpsc::unbounded_channel();
        let conn1 = ConnectionHandle::from_sender(tx1);
        let conn2 = ConnectionHandle::from_sender(tx2);
        let client_id = ClientId::from("blah".to_string());

        let req1 = ConnReq::new(client_id.clone(), connect1, None, conn1);
        let req2 = ConnReq::new(client_id.clone(), connect2, None, conn2);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .unwrap();
        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req2),
            ))
            .unwrap();

        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::ConnAck(_)))
        );
        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::DropConnection))
        );
        assert_matches!(rx1.recv().await, None);

        assert_matches!(
            rx2.recv().await,
            Some(Message::Client(_, ClientEvent::ConnAck(_)))
        );
    }

    #[tokio::test]
    async fn test_invalid_protocol_name() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: "AMQP".to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };
        let (tx1, mut rx1) = mpsc::unbounded_channel();
        let conn1 = ConnectionHandle::from_sender(tx1);
        let client_id = ClientId::from("blah".to_string());
        let req1 = ConnReq::new(client_id.clone(), connect1, None, conn1);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .unwrap();

        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::DropConnection))
        );
        assert_matches!(rx1.recv().await, None)
    }

    #[tokio::test]
    async fn test_invalid_protocol_level() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: 0x3,
        };
        let (tx1, mut rx1) = mpsc::unbounded_channel();
        let conn1 = ConnectionHandle::from_sender(tx1);
        let client_id = ClientId::from("blah".to_string());
        let req1 = ConnReq::new(client_id.clone(), connect1, None, conn1);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .unwrap();

        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::ConnAck(proto::ConnAck {
                return_code:
                    proto::ConnectReturnCode::Refused(
                        proto::ConnectionRefusedReason::UnacceptableProtocolVersion,
                    ),
                ..
            })))
        );
        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::DropConnection))
        );
        assert_matches!(rx1.recv().await, None)
    }

    #[tokio::test]
    async fn test_connect_auth_succeeded() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };

        let (tx1, mut rx1) = mpsc::unbounded_channel();
        let conn1 = ConnectionHandle::from_sender(tx1);
        let client_id = ClientId::from("blah".to_string());
        let req1 = ConnReq::new(client_id.clone(), connect1, None, conn1);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .unwrap();

        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::ConnAck(proto::ConnAck {
                return_code:
                    proto::ConnectReturnCode::Accepted,
                ..
            })))
        );
    }

    #[tokio::test]
    async fn test_connect_unknown_client() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(None))
            .authorizer(|_| Ok(true))
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };

        let (tx1, mut rx1) = mpsc::unbounded_channel();
        let conn1 = ConnectionHandle::from_sender(tx1);
        let client_id = ClientId::from("blah".to_string());
        let req1 = ConnReq::new(client_id.clone(), connect1, None, conn1);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .unwrap();

        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::ConnAck(proto::ConnAck {
                return_code:
                    proto::ConnectReturnCode::Refused(
                        proto::ConnectionRefusedReason::BadUserNameOrPassword,
                    ),
                ..
            })))
        );
        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::DropConnection))
        );
        assert_matches!(rx1.recv().await, None)
    }

    #[tokio::test]
    async fn test_connect_authentication_failed() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Err(AuthenticateError))
            .authorizer(|_| Ok(true))
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };

        let (tx1, mut rx1) = mpsc::unbounded_channel();
        let conn1 = ConnectionHandle::from_sender(tx1);
        let client_id = ClientId::from("blah".to_string());
        let req1 = ConnReq::new(client_id.clone(), connect1, None, conn1);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .unwrap();

        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::ConnAck(proto::ConnAck {
                return_code:
                    proto::ConnectReturnCode::Refused(
                        proto::ConnectionRefusedReason::ServerUnavailable,
                    ),
                ..
            })))
        );
        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::DropConnection))
        );
        assert_matches!(rx1.recv().await, None)
    }

    #[tokio::test]
    async fn test_connect_client_has_no_permissions() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some("client-a".into())))
            .authorizer(|_| Ok(false))
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };

        let (tx1, mut rx1) = mpsc::unbounded_channel();
        let conn1 = ConnectionHandle::from_sender(tx1);
        let client_id = ClientId::from("blah".to_string());
        let req1 = ConnReq::new(client_id.clone(), connect1, None, conn1);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .unwrap();

        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::ConnAck(proto::ConnAck {
                return_code:
                    proto::ConnectReturnCode::Refused(
                        proto::ConnectionRefusedReason::NotAuthorized
                    ),
                ..
            })))
        );
        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::DropConnection))
        );
        assert_matches!(rx1.recv().await, None)
    }

    #[tokio::test]
    async fn test_connect_authorization_failed() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Err(AuthorizeError))
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let connect1 = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession("blah".to_string()),
            keep_alive: Duration::default(),
            protocol_name: PROTOCOL_NAME.to_string(),
            protocol_level: PROTOCOL_LEVEL,
        };

        let (tx1, mut rx1) = mpsc::unbounded_channel();
        let conn1 = ConnectionHandle::from_sender(tx1);
        let client_id = ClientId::from("blah".to_string());
        let req1 = ConnReq::new(client_id.clone(), connect1, None, conn1);

        broker_handle
            .send(Message::Client(
                client_id.clone(),
                ClientEvent::ConnReq(req1),
            ))
            .unwrap();

        assert_matches!(
            rx1.recv().await.unwrap(),
            Message::Client(_, ClientEvent::ConnAck(proto::ConnAck {
                return_code:
                    proto::ConnectReturnCode::Refused(
                        proto::ConnectionRefusedReason::ServerUnavailable,
                    ),
                ..
            }))
        );
        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::DropConnection))
        );
        assert_matches!(rx1.recv().await, None)
    }

    #[test]
    fn test_add_session_empty_transient() {
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect = transient_connect(id);
        let handle = connection_handle();
        let req = ConnReq::new(client_id.clone(), connect, None, handle);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id, req).unwrap();

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
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect = persistent_connect(id);
        let handle = connection_handle();
        let req = ConnReq::new(client_id.clone(), connect, None, handle);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id, req).unwrap();

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
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id.clone());
        let connect2 = transient_connect(id);
        let id = Uuid::new_v4();
        let (tx1, _rx1) = mpsc::unbounded_channel();
        let handle1 = ConnectionHandle::new(id, tx1.clone());
        let handle2 = ConnectionHandle::new(id, tx1);

        let req1 = ConnReq::new(client_id.clone(), connect1, None, handle1);
        let req2 = ConnReq::new(client_id, connect2, None, handle2);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id.clone(), req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let result = broker.open_session(auth_id, req2);
        assert_matches!(result, Err(SessionError::ProtocolViolation(_)));
        assert_eq!(0, broker.sessions.len());
    }

    #[test]
    #[should_panic]
    fn test_add_session_same_connection_persistent() {
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let connect2 = persistent_connect(id);
        let id = Uuid::new_v4();
        let (tx1, _rx1) = mpsc::unbounded_channel();
        let handle1 = ConnectionHandle::new(id, tx1.clone());
        let handle2 = ConnectionHandle::new(id, tx1);

        let req1 = ConnReq::new(client_id.clone(), connect1, None, handle1);
        let req2 = ConnReq::new(client_id, connect2, None, handle2);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id.clone(), req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let result = broker.open_session(auth_id, req2);
        assert_matches!(result, Err(SessionError::ProtocolViolation(_)));
        assert_eq!(0, broker.sessions.len());
    }

    #[test]
    fn test_add_session_different_connection_transient_then_transient() {
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id.clone());
        let connect2 = transient_connect(id);
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, None, handle1);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id.clone(), req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let req2 = ConnReq::new(client_id.clone(), connect2, None, handle2);
        let result = broker.open_session(auth_id, req2);
        assert_matches!(result, Err(SessionError::DuplicateSession(_, _)));
        assert_matches!(broker.sessions[&client_id], Session::Transient(_));
        assert_eq!(1, broker.sessions.len());
    }

    #[test]
    fn test_add_session_different_connection_transient_then_persistent() {
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = transient_connect(id.clone());
        let connect2 = persistent_connect(id);
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, None, handle1);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id.clone(), req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let req2 = ConnReq::new(client_id.clone(), connect2, None, handle2);
        let result = broker.open_session(auth_id, req2);
        assert_matches!(result, Err(SessionError::DuplicateSession(_, _)));
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));
        assert_eq!(1, broker.sessions.len());
    }

    #[test]
    fn test_add_session_different_connection_persistent_then_transient() {
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let connect2 = transient_connect(id);
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, None, handle1);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id.clone(), req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let req2 = ConnReq::new(client_id.clone(), connect2, None, handle2);
        let result = broker.open_session(auth_id, req2);
        assert_matches!(result, Err(SessionError::DuplicateSession(_, _)));
        assert_matches!(broker.sessions[&client_id], Session::Transient(_));
        assert_eq!(1, broker.sessions.len());
    }

    #[test]
    fn test_add_session_different_connection_persistent_then_persistent() {
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let connect2 = persistent_connect(id);
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, None, handle1);
        let req2 = ConnReq::new(client_id.clone(), connect2, None, handle2);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id.clone(), req1).unwrap();
        assert_eq!(1, broker.sessions.len());

        let result = broker.open_session(auth_id, req2);
        assert_matches!(result, Err(SessionError::DuplicateSession(_, _)));
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));
        assert_eq!(1, broker.sessions.len());
    }

    #[test]
    fn test_add_session_offline_persistent() {
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let connect2 = persistent_connect(id);
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, None, handle1);
        let req2 = ConnReq::new(client_id.clone(), connect2, None, handle2);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id.clone(), req1).unwrap();

        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));

        // close session and check behavior
        let old_session = broker.close_session(&client_id);
        assert_matches!(old_session, Some(Session::Disconnecting(_)));
        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Offline(_));

        // Reopen session
        broker.open_session(auth_id, req2).unwrap();

        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));
    }

    #[test]
    fn test_add_session_offline_transient() {
        let mut broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some(AuthId::Anonymous)))
            .authorizer(|_| Ok(true))
            .build();

        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let connect1 = persistent_connect(id.clone());
        let handle1 = connection_handle();
        let handle2 = connection_handle();
        let req1 = ConnReq::new(client_id.clone(), connect1, None, handle1);
        let auth_id = AuthId::Anonymous;

        broker.open_session(auth_id.clone(), req1).unwrap();

        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Persistent(_));

        // close session and check behavior
        let old_session = broker.close_session(&client_id);
        assert_matches!(old_session, Some(Session::Disconnecting(_)));
        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Offline(_));

        // Reopen session
        let connect2 = transient_connect(id);
        let req2 = ConnReq::new(client_id.clone(), connect2, None, handle2);
        broker.open_session(auth_id, req2).unwrap();

        assert_eq!(1, broker.sessions.len());
        assert_matches!(broker.sessions[&client_id], Session::Transient(_));
    }

    #[tokio::test]
    async fn test_publish_client_has_no_permissions() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some("client-a".into())))
            .authorizer(|activity: Activity| match activity.operation() {
                Operation::Connect(_) => Ok(true),
                _ => Ok(false),
            })
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let (client_id, mut rx) = connect_client("pub", &mut broker_handle).await.unwrap();

        let publish = proto::Publish {
            packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtLeastOnce(
                proto::PacketIdentifier::new(1).unwrap(),
                false,
            ),
            retain: true,
            topic_name: "/foo/bar".to_string(),
            payload: Bytes::new(),
        };

        let message = Message::Client(client_id.clone(), ClientEvent::PublishFrom(publish));
        broker_handle.send(message).unwrap();

        assert_matches!(
            rx.recv().await,
            Some(Message::Client(_, ClientEvent::DropConnection))
        );
        assert_matches!(rx.recv().await, None)
    }

    #[tokio::test]
    async fn test_subscribe_client_has_no_permissions() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some("client-a".into())))
            .authorizer(|activity: Activity| match activity.operation() {
                Operation::Connect(_) => Ok(true),
                Operation::Subscribe(subscribe) => match subscribe.topic_filter() {
                    "/topic/denied" => Ok(false),
                    _ => Ok(true),
                },
                _ => Ok(false),
            })
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let (client_id, mut rx1) = connect_client("sub", &mut broker_handle).await.unwrap();

        let subscribe = proto::Subscribe {
            packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![
                proto::SubscribeTo {
                    topic_filter: "/topic/allowed".to_string(),
                    qos: proto::QoS::AtLeastOnce,
                },
                proto::SubscribeTo {
                    topic_filter: "/topic/denied".to_string(),
                    qos: proto::QoS::AtMostOnce,
                },
                proto::SubscribeTo {
                    topic_filter: "/topic/in#va/#lid".to_string(),
                    qos: proto::QoS::ExactlyOnce,
                },
            ],
        };

        let message = Message::Client(client_id.clone(), ClientEvent::Subscribe(subscribe));
        broker_handle.send(message).unwrap();

        let expected_qos = vec![
            proto::SubAckQos::Success(proto::QoS::AtLeastOnce),
            proto::SubAckQos::Failure,
            proto::SubAckQos::Failure,
        ];
        assert_matches!(
            rx1.recv().await,
            Some(Message::Client(_, ClientEvent::SubAck(suback))) if suback.qos == expected_qos
        );
    }

    #[tokio::test]
    async fn test_receive_client_has_no_permissions() {
        let broker = BrokerBuilder::default()
            .authenticator(|_| Ok(Some("client-a".into())))
            .authorizer(|activity: Activity| match activity.operation() {
                Operation::Connect(_) => Ok(true),
                Operation::Publish(_) => Ok(true),
                Operation::Subscribe(_) => Ok(true),
                _ => Ok(false),
            })
            .build();

        let mut broker_handle = broker.handle();
        tokio::spawn(broker.run().map(drop));

        let (sub_id, mut sub_rx) = connect_client("sub", &mut broker_handle).await.unwrap();

        let subscribe = proto::Subscribe {
            packet_identifier: proto::PacketIdentifier::new(1).unwrap(),
            subscribe_to: vec![proto::SubscribeTo {
                topic_filter: "/foo/bar".to_string(),
                qos: proto::QoS::AtLeastOnce,
            }],
        };

        let message = Message::Client(sub_id.clone(), ClientEvent::Subscribe(subscribe));
        broker_handle.send(message).unwrap();

        let (pub_id, mut pub_rx) = connect_client("pub", &mut broker_handle).await.unwrap();

        let publish = proto::Publish {
            packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtLeastOnce(
                proto::PacketIdentifier::new(1).unwrap(),
                false,
            ),
            retain: true,
            topic_name: "/foo/bar".to_string(),
            payload: Bytes::new(),
        };

        let message = Message::Client(pub_id.clone(), ClientEvent::PublishFrom(publish));
        broker_handle.send(message).unwrap();

        assert_matches!(
            pub_rx.recv().await,
            Some(Message::Client(_, ClientEvent::PubAck(_)))
        );

        assert_matches!(
            sub_rx.recv().await,
            Some(Message::Client(_, ClientEvent::SubAck(_)))
        );
        assert_matches!(sub_rx.try_recv(), Err(TryRecvError::Empty))
    }

    async fn connect_client(
        client_id: &str,
        broker_handle: &mut BrokerHandle,
    ) -> Result<(ClientId, UnboundedReceiver<Message>), Error> {
        let connect = persistent_connect(client_id.into());

        let (tx, mut rx) = mpsc::unbounded_channel();
        let conn = ConnectionHandle::from_sender(tx);
        let client_id = ClientId::from(client_id);
        let req = ConnReq::new(client_id.clone(), connect, None, conn);
        broker_handle.send(Message::Client(
            client_id.clone(),
            ClientEvent::ConnReq(req),
        ))?;

        assert_matches!(
            rx.recv().await,
            Some(Message::Client(_, ClientEvent::ConnAck(proto::ConnAck {
                return_code:
                    proto::ConnectReturnCode::Accepted,
                ..
            })))
        );

        Ok((client_id, rx))
    }
}
