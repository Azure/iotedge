mod connected;
mod disconnecting;
pub(crate) mod identifiers;
mod offline;
mod state;

use chrono::{DateTime, Utc};
pub use connected::ConnectedSession;
use disconnecting::DisconnectingSession;
use offline::OfflineSession;
pub use state::SessionState;

use std::collections::HashMap;

use mqtt3::proto;

use crate::{
    subscription::Subscription, ClientEvent, ClientId, ClientInfo, ConnReq, ConnectionHandle, Error,
};

#[derive(Debug)]
pub enum Session {
    Transient(ConnectedSession),
    Persistent(ConnectedSession),
    Disconnecting(DisconnectingSession),
    Offline(OfflineSession),
}

impl Session {
    pub fn new_transient(connreq: ConnReq, state: SessionState) -> Self {
        let (_, _, connect, handle) = connreq.into_parts();
        let connected = ConnectedSession::new(state, connect.will, handle);
        Self::Transient(connected)
    }

    pub fn new_persistent(connreq: ConnReq, state: SessionState) -> Self {
        let (_, _, connect, handle) = connreq.into_parts();
        let connected = ConnectedSession::new(state, connect.will, handle);
        Self::Persistent(connected)
    }

    pub fn new_offline(state: SessionState, last_active: DateTime<Utc>) -> Self {
        let offline = OfflineSession::new(state, last_active);
        Self::Offline(offline)
    }

    pub fn new_disconnecting(
        client_info: ClientInfo,
        will: Option<proto::Publication>,
        handle: ConnectionHandle,
    ) -> Self {
        let disconnecting = DisconnectingSession::new(client_info, will, handle);
        Self::Disconnecting(disconnecting)
    }

    pub fn client_id(&self) -> &ClientId {
        match self {
            Self::Transient(connected) => connected.state().client_info().client_id(),
            Self::Persistent(connected) => connected.state().client_info().client_id(),
            Self::Offline(offline) => offline.client_id(),
            Self::Disconnecting(disconnecting) => disconnecting.client_info().client_id(),
        }
    }

    pub fn client_info(&self) -> &ClientInfo {
        match self {
            Self::Transient(connected) => connected.state().client_info(),
            Self::Persistent(connected) => connected.state().client_info(),
            Self::Offline(offline) => offline.last_client_info(),
            Self::Disconnecting(disconnecting) => disconnecting.client_info(),
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

    pub fn send(&self, event: ClientEvent) -> Result<(), Error> {
        match self {
            Self::Transient(connected) => connected.send(event),
            Self::Persistent(connected) => connected.send(event),
            Self::Disconnecting(disconnecting) => disconnecting.send(event),
            _ => Err(Error::SessionOffline),
        }
    }
}

#[cfg(test)]
mod tests {
    use std::{net::IpAddr, net::Ipv4Addr, net::SocketAddr, time::Duration};

    use chrono::Utc;
    use matches::assert_matches;
    use tokio::sync::mpsc;
    use uuid::Uuid;

    use mqtt3::{proto, PROTOCOL_LEVEL, PROTOCOL_NAME};

    use super::{Session, SessionState};
    use crate::{
        auth::AuthId, settings::QueueFullAction, tests::peer_addr, Auth, ClientId, ClientInfo,
        ConnReq, ConnectionHandle, Error, SessionConfig,
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

    fn default_config() -> SessionConfig {
        SessionConfig::new(
            Duration::default(),
            Duration::default(),
            None,
            16,
            1000,
            None,
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
        let auth_id: AuthId = "auth-id1".into();
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, auth_id);
        let state = SessionState::new(client_info, default_config());
        let mut session = Session::new_transient(req1, state);
        let subscribe_to = proto::SubscribeTo {
            topic_filter: "topic/new".to_string(),
            qos: proto::QoS::AtMostOnce,
        };

        let (ack, subscription) = session.subscribe_to(subscribe_to).unwrap();

        assert_eq!(ack, proto::SubAckQos::Success(proto::QoS::AtMostOnce));
        assert_matches!(subscription, Some(_));
        match &session {
            Session::Transient(connected) => {
                assert_eq!(1, connected.subscriptions().len());
                assert_eq!(
                    proto::QoS::AtMostOnce,
                    *connected.subscriptions()["topic/new"].max_qos()
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
                assert_eq!(1, connected.subscriptions().len());
                assert_eq!(
                    proto::QoS::AtLeastOnce,
                    *connected.subscriptions()["topic/new"].max_qos()
                );
            }
            _ => panic!("not transient"),
        }
    }

    #[test]
    fn test_subscribe_to_with_invalid_topic() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id.clone());
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id.clone(), socket, AuthId::from("authId1"));
        let connect1 = transient_connect(id);
        let handle1 = connection_handle();
        let req1 = ConnReq::new(client_id, peer_addr(), connect1, Auth::Unknown, handle1);
        let state = SessionState::new(client_info, default_config());
        let mut session = Session::new_transient(req1, state);
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
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id.clone(), socket, AuthId::from("authId1"));
        let connect1 = transient_connect(id);
        let handle1 = connection_handle();
        let req1 = ConnReq::new(client_id, peer_addr(), connect1, Auth::Unknown, handle1);
        let state = SessionState::new(client_info, default_config());
        let mut session = Session::new_transient(req1, state);

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
                assert_eq!(1, connected.subscriptions().len());
                assert_eq!(
                    proto::QoS::AtMostOnce,
                    *connected.subscriptions()["topic/new"].max_qos()
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
                assert_eq!(0, connected.subscriptions().len());
            }
            _ => panic!("not transient"),
        }
    }

    #[test]
    fn test_offline_subscribe_to() {
        let id = "id1".to_string();
        let client_id = ClientId::from(id);
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let mut session =
            Session::new_offline(SessionState::new(client_info, default_config()), Utc::now());

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
        let socket = SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080);
        let client_info = ClientInfo::new(client_id, socket, AuthId::from("authId1"));
        let mut session =
            Session::new_offline(SessionState::new(client_info, default_config()), Utc::now());

        let unsubscribe = proto::Unsubscribe {
            packet_identifier: proto::PacketIdentifier::new(24).unwrap(),
            unsubscribe_from: vec!["topic/new".to_string()],
        };
        let result = session.unsubscribe(&unsubscribe);
        assert_matches!(result, Err(Error::SessionOffline));
    }
}
