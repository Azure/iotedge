use std::collections::HashMap;
use std::convert::TryFrom;

use mqtt3::proto;

use crate::session::Session;
use crate::{ClientId, Error};

const STATE_CHANGE_QOS: proto::QoS = proto::QoS::AtLeastOnce;

pub enum StateChange<'a> {
    Subscriptions(&'a ClientId, Option<Vec<&'a str>>),
    Connections(Vec<&'a ClientId>),
    Sessions(Vec<&'a ClientId>),
}

impl<'a> StateChange<'a> {
    pub fn new_subscription_change(client_id: &'a ClientId, session: Option<&'a Session>) -> Self {
        let subscriptions = session.and_then(|session| {
            session
                .subscriptions()
                .map(|s| s.keys().map(String::as_str).collect())
        });

        Self::Subscriptions(client_id, subscriptions)
    }

    pub fn new_connection_change(sessions: &'a HashMap<ClientId, Session>) -> Self {
        let connections = sessions
            .iter()
            .filter_map(|(client_id, session)| match session {
                Session::Transient(_) => Some(client_id),
                Session::Persistent(_) => Some(client_id),
                _ => None,
            })
            .collect();

        Self::Connections(connections)
    }

    pub fn new_session_change(sessions: &'a HashMap<ClientId, Session>) -> Self {
        let sessions = sessions
            .iter()
            .filter_map(|(client_id, session)| match session {
                Session::Transient(_) => Some(client_id),
                Session::Persistent(_) => Some(client_id),
                Session::Offline(_) => Some(client_id),
                _ => None,
            })
            .collect();

        Self::Sessions(sessions)
    }
}

impl<'a> TryFrom<StateChange<'a>> for proto::Publication {
    type Error = Error;

    fn try_from(state: StateChange<'a>) -> Result<Self, Error> {
        Ok(match state {
            StateChange::Subscriptions(client_id, subscriptions) => {
                let payload = subscriptions
                    .map(|subscriptions| serde_json::to_string(&subscriptions))
                    .transpose()?
                    .map(|json| json.into())
                    .unwrap_or_default();

                proto::Publication {
                    topic_name: format!("$edgehub/{}/subscriptions", client_id),
                    qos: STATE_CHANGE_QOS,
                    retain: true,
                    payload,
                }
            }
            StateChange::Connections(connections) => proto::Publication {
                topic_name: "$edgehub/connected".to_owned(),
                qos: STATE_CHANGE_QOS,
                retain: true,
                payload: serde_json::to_string(&connections)?.into(),
            },
            StateChange::Sessions(sessions) => proto::Publication {
                topic_name: "$edgehub/sessions".to_owned(),
                qos: STATE_CHANGE_QOS,
                retain: true,
                payload: serde_json::to_string(&sessions)?.into(),
            },
        })
    }
}

#[cfg(test)]
mod tests {
    use std::{
        collections::HashMap, convert::TryInto, net::IpAddr, net::Ipv4Addr, net::SocketAddr,
        str::FromStr,
    };

    use chrono::Utc;

    use mqtt3::proto;

    use crate::{
        broker::tests::{connection_handle, is_notify_equal, persistent_connect},
        session::{Session, SessionState},
        state_change::{StateChange, STATE_CHANGE_QOS},
        subscription::{Subscription, TopicFilter},
        tests::peer_addr,
        Auth, AuthId, BrokerConfig, ClientId, ClientInfo, ConnReq, SessionConfig,
    };

    #[test]
    fn test_subscriptions() {
        let expected_id: ClientId = "Session".into();
        let client_info = ClientInfo::new(
            expected_id.clone(),
            SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080),
            AuthId::from("authId1"),
        );

        // Session with no subscriptions
        let session = make_session(expected_id.as_str(), &client_info, Vec::<&str>::new(), true);
        let no_subs = StateChange::new_subscription_change(&expected_id, Some(&session));

        if let StateChange::Subscriptions(stored_id, stored_subs) = &no_subs {
            assert_eq!(&&expected_id, stored_id);
            assert_eq!(&Some(Vec::new()), stored_subs);
        }

        let message: proto::Publication = no_subs.try_into().unwrap();
        matches_subscription_publication(message, expected_id.as_str(), &[]);

        // Session with 1 subscription
        let session = make_session(expected_id.as_str(), &client_info, &["Sub"], true);
        let one_sub = StateChange::new_subscription_change(&expected_id, Some(&session));

        if let StateChange::Subscriptions(stored_id, stored_subs) = &one_sub {
            assert_eq!(&&expected_id, stored_id);
            assert_eq!(&Some(vec!["Sub"]), stored_subs);
        }

        let message: proto::Publication = one_sub.try_into().unwrap();
        matches_subscription_publication(message, expected_id.as_str(), &["Sub"]);

        // Session with many subscriptions
        let session = make_session(
            expected_id.as_str(),
            &client_info,
            (1..4).map(|i| format!("Sub{}", i)),
            true,
        );
        let many_subs = StateChange::new_subscription_change(&expected_id, Some(&session));

        if let StateChange::Subscriptions(stored_id, stored_subs) = &many_subs {
            assert_eq!(&&expected_id, stored_id);
            let mut actual: Vec<&str> = stored_subs.clone().unwrap();
            actual.sort_unstable();
            assert_eq!(vec!["Sub1", "Sub2", "Sub3"], actual);
        }

        let message: proto::Publication = many_subs.try_into().unwrap();
        matches_subscription_publication(message, expected_id.as_str(), &["Sub1", "Sub2", "Sub3"]);

        // Clear subscriptions
        let clear_subs = StateChange::new_subscription_change(&expected_id, None);

        if let StateChange::Subscriptions(stored_id, stored_subs) = &clear_subs {
            assert_eq!(&&expected_id, stored_id);
            assert!(stored_subs.is_none());
        }

        let message: proto::Publication = clear_subs.try_into().unwrap();
        let expected: bytes::Bytes = "".into();
        assert_eq!(expected, message.payload);
    }

    #[test]
    fn test_connections() {
        // No sessions
        let sessions: HashMap<ClientId, Session> = HashMap::new();
        let client_info = ClientInfo::new(
            "client_id",
            SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080),
            AuthId::from("authId1"),
        );
        let no_connections = StateChange::new_connection_change(&sessions);
        if let StateChange::Connections(no_connections) = &no_connections {
            assert_eq!(&Vec::<&ClientId>::new(), no_connections);
        } else {
            panic!("Expected Connection Change")
        }

        let message: proto::Publication = no_connections.try_into().unwrap();
        matches_connection_publication(message, &[]);

        // One session
        let sessions: HashMap<ClientId, Session> = (1..2)
            .map(|i| {
                let id = format!("Session {}", i);
                let session = make_session(
                    &id,
                    &client_info,
                    (1..i).map(|j| format!("Subscription {}", j)),
                    true,
                );

                (id.into(), session)
            })
            .collect();
        let one_connection = StateChange::new_connection_change(&sessions);
        if let StateChange::Connections(one_connection) = &one_connection {
            let expected: ClientId = "Session 1".into();
            assert_eq!(&vec![&expected], one_connection);
        } else {
            panic!("Expected Connection Change")
        }

        let message: proto::Publication = one_connection.try_into().unwrap();
        matches_connection_publication(message, &["Session 1"]);

        // Multiple sessions
        let sessions: HashMap<ClientId, Session> = (1..4)
            .map(|i| {
                let id = format!("Session {}", i);
                let session = make_session(
                    &id,
                    &client_info,
                    (1..i).map(|j| format!("Subscription {}", j)),
                    true,
                );

                (id.into(), session)
            })
            .collect();
        let many_connections = StateChange::new_connection_change(&sessions);
        if let StateChange::Connections(many_connections) = &many_connections {
            let mut actual: Vec<&str> = many_connections.iter().map(|id| id.as_str()).collect();
            actual.sort_unstable();
            assert_eq!(vec!["Session 1", "Session 2", "Session 3"], actual);
        } else {
            panic!("Expected Connection Change")
        }

        let message: proto::Publication = many_connections.try_into().unwrap();
        matches_connection_publication(message, &["Session 1", "Session 2", "Session 3"]);

        // Offline sessions
        let sessions: HashMap<ClientId, Session> = (1..7)
            .map(|i| {
                let id = format!("Session {}", i);
                let session = make_session(
                    &id,
                    &client_info,
                    (1..i).map(|j| format!("Subscription {}", j)),
                    i % 2 == 0, // even sessions are online, odd offline
                );

                (id.into(), session)
            })
            .collect();
        let many_connections = StateChange::new_connection_change(&sessions);
        if let StateChange::Connections(many_connections) = &many_connections {
            let mut actual: Vec<&str> = many_connections.iter().map(|id| id.as_str()).collect();
            actual.sort_unstable();
            assert_eq!(vec!["Session 2", "Session 4", "Session 6"], actual);
        } else {
            panic!("Expected Connection Change")
        }

        let message: proto::Publication = many_connections.try_into().unwrap();
        matches_connection_publication(message, &["Session 2", "Session 4", "Session 6"]);
    }

    #[test]
    fn test_sessions() {
        // No sessions
        let sessions: HashMap<ClientId, Session> = HashMap::new();
        let client_info = ClientInfo::new(
            "client_id",
            SocketAddr::new(IpAddr::V4(Ipv4Addr::new(127, 0, 0, 1)), 8080),
            AuthId::from("authId1"),
        );
        let no_sessions = StateChange::new_session_change(&sessions);
        if let StateChange::Sessions(no_sessions) = &no_sessions {
            assert_eq!(&Vec::<&ClientId>::new(), no_sessions);
        } else {
            panic!("Expected Session Change")
        }

        let message: proto::Publication = no_sessions.try_into().unwrap();
        matches_session_publication(message, &[]);

        // One session
        let sessions: HashMap<ClientId, Session> = (1..2)
            .map(|i| {
                let id = format!("Session {}", i);
                let session = make_session(
                    &id,
                    &client_info,
                    (1..i).map(|j| format!("Subscription {}", j)),
                    true,
                );

                (id.into(), session)
            })
            .collect();
        let one_session = StateChange::new_session_change(&sessions);
        if let StateChange::Sessions(one_session) = &one_session {
            let expected: ClientId = "Session 1".into();
            assert_eq!(&vec![&expected], one_session);
        } else {
            panic!("Expected Session Change")
        }

        let message: proto::Publication = one_session.try_into().unwrap();
        matches_session_publication(message, &["Session 1"]);

        // Multiple sessions
        let sessions: HashMap<ClientId, Session> = (1..4)
            .map(|i| {
                let id = format!("Session {}", i);
                let session = make_session(
                    &id,
                    &client_info,
                    (1..i).map(|j| format!("Subscription {}", j)),
                    true,
                );

                (id.into(), session)
            })
            .collect();
        let many_sessions = StateChange::new_session_change(&sessions);
        if let StateChange::Sessions(many_sessions) = &many_sessions {
            let mut actual: Vec<&str> = many_sessions.iter().map(|id| id.as_str()).collect();
            actual.sort_unstable();
            assert_eq!(vec!["Session 1", "Session 2", "Session 3"], actual);
        } else {
            panic!("Expected Session Change")
        }

        let message: proto::Publication = many_sessions.try_into().unwrap();
        matches_session_publication(message, &["Session 1", "Session 2", "Session 3"]);

        // Offline sessions
        let sessions: HashMap<ClientId, Session> = (1..7)
            .map(|i| {
                let id = format!("Session {}", i);
                let session = make_session(
                    &id,
                    &client_info,
                    (1..i).map(|j| format!("Subscription {}", j)),
                    i % 2 == 0, // even sessions are online, odd offline
                );

                (id.into(), session)
            })
            .collect();
        let many_sessions = StateChange::new_session_change(&sessions);
        if let StateChange::Sessions(many_sessions) = &many_sessions {
            let mut actual: Vec<&str> = many_sessions.iter().map(|id| id.as_str()).collect();
            actual.sort_unstable();
            assert_eq!(
                vec![
                    "Session 1",
                    "Session 2",
                    "Session 3",
                    "Session 4",
                    "Session 5",
                    "Session 6"
                ],
                actual
            );
        } else {
            panic!("Expected Session Change")
        }

        let message: proto::Publication = many_sessions.try_into().unwrap();
        matches_session_publication(
            message,
            &[
                "Session 1",
                "Session 2",
                "Session 3",
                "Session 4",
                "Session 5",
                "Session 6",
            ],
        );
    }

    fn default_config() -> SessionConfig {
        BrokerConfig::default().session().clone()
    }

    fn make_session<I, S>(
        id: &str,
        client_info: &ClientInfo,
        subscriptions: I,
        online: bool,
    ) -> Session
    where
        I: IntoIterator<Item = S>,
        S: AsRef<str>,
    {
        let mut state = SessionState::new(client_info.clone(), default_config());

        for topic_filter in subscriptions {
            state.update_subscription(
                topic_filter.as_ref().to_owned(),
                Subscription::new(
                    TopicFilter::from_str(&topic_filter.as_ref()).unwrap(),
                    proto::QoS::AtLeastOnce,
                ),
            );
        }

        if online {
            Session::new_persistent(
                ConnReq::new(
                    id.into(),
                    peer_addr(),
                    persistent_connect(id.to_owned()),
                    Auth::Identity(AuthId::Anonymous),
                    connection_handle(),
                ),
                state,
            )
        } else {
            Session::new_offline(state, Utc::now())
        }
    }

    fn matches_subscription_publication(
        publication: proto::Publication,
        client_id: &str,
        body: &[&str],
    ) {
        matches_publication(
            publication,
            &format!("$edgehub/{}/subscriptions", client_id),
            body,
        );
    }

    fn matches_connection_publication(publication: proto::Publication, body: &[&str]) {
        matches_publication(publication, "$edgehub/connected", body);
    }

    fn matches_session_publication(publication: proto::Publication, body: &[&str]) {
        matches_publication(publication, "$edgehub/sessions", body);
    }

    fn matches_publication(publication: proto::Publication, topic: &str, body: &[&str]) {
        let proto::Publication {
            topic_name,
            qos,
            retain,
            payload,
        } = publication;

        assert_eq!(&topic_name, topic);
        assert_eq!(qos, STATE_CHANGE_QOS);
        assert_eq!(retain, true);
        is_notify_equal(&payload, body);
    }
}
