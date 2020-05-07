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
    pub fn new_subscription(client_id: &'a ClientId, session: &'a Session) -> Self {
        let subscriptions = session
            .subscriptions()
            .unwrap()
            .keys()
            .map(String::as_str)
            .collect();

        Self::Subscriptions(client_id, Some(subscriptions))
    }

    pub fn clear_subscriptions(client_id: &'a ClientId) -> Self {
        Self::Subscriptions(client_id, None)
    }

    pub fn new_connection(sessions: &'a HashMap<ClientId, Session>) -> Self {
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

    pub fn new_session(sessions: &'a HashMap<ClientId, Session>) -> Self {
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
                let payload = if let Some(subscriptions) = subscriptions {
                    serde_json::to_string(&subscriptions)
                        .map_err(|_| Error::NotifyError)?
                        .into()
                } else {
                    "".into()
                };

                proto::Publication {
                    topic_name: format!("$edgehub/subscriptions/{}", client_id),
                    qos: STATE_CHANGE_QOS,
                    retain: true,
                    payload,
                }
            }
            StateChange::Connections(connections) => proto::Publication {
                topic_name: "$edgehub/connected".to_owned(),
                qos: STATE_CHANGE_QOS,
                retain: true,
                payload: serde_json::to_string(&connections)
                    .map_err(|_| Error::NotifyError)?
                    .into(),
            },
            StateChange::Sessions(sessions) => proto::Publication {
                topic_name: "$edgehub/sessions".to_owned(),
                qos: STATE_CHANGE_QOS,
                retain: true,
                payload: serde_json::to_string(&sessions)
                    .map_err(|_| Error::NotifyError)?
                    .into(),
            },
        })
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    use crate::broker::tests::*;
    use crate::session::*;
    use crate::subscription::*;
    use crate::*;
    use std::collections::{HashMap, VecDeque};
    use std::convert::TryInto;
    use std::iter::FromIterator;
    use std::str::FromStr;
    #[test]
    fn test_subscriptions() {
        let expected_id: ClientId = "Session".into();

        // Session with no subscriptions
        let session = make_session(expected_id.as_str(), Vec::<&str>::new(), true);
        let no_subs = StateChange::new_subscription(&expected_id, &session);

        if let StateChange::Subscriptions(stored_id, stored_subs) = &no_subs {
            assert_eq!(&&expected_id, stored_id);
            assert_eq!(&Vec::<&str>::new(), stored_subs.as_ref().unwrap());
        }

        let message: proto::Publication = no_subs.try_into().unwrap();
        matches_subscription_publication(message, expected_id.as_str(), &[]);

        // Session with 1 subscription
        let session = make_session(expected_id.as_str(), &["Sub"], true);
        let one_sub = StateChange::new_subscription(&expected_id, &session);

        if let StateChange::Subscriptions(stored_id, stored_subs) = &one_sub {
            assert_eq!(&&expected_id, stored_id);
            assert_eq!(&vec!["Sub"], stored_subs.as_ref().unwrap());
        }

        let message: proto::Publication = one_sub.try_into().unwrap();
        matches_subscription_publication(message, expected_id.as_str(), &["Sub"]);

        // Session with many subscriptions
        let session = make_session(
            expected_id.as_str(),
            (1..4).map(|i| format!("Sub{}", i)),
            true,
        );
        let many_subs = StateChange::new_subscription(&expected_id, &session);

        if let StateChange::Subscriptions(stored_id, stored_subs) = &many_subs {
            assert_eq!(&&expected_id, stored_id);
            let mut actual: Vec<&str> = stored_subs.clone().unwrap();
            actual.sort();
            assert_eq!(vec!["Sub1", "Sub2", "Sub3"], actual);
        }

        let message: proto::Publication = many_subs.try_into().unwrap();
        matches_subscription_publication(message, expected_id.as_str(), &["Sub1", "Sub2", "Sub3"]);

        // Clear subscriptions
        let clear_subs = StateChange::clear_subscriptions(&expected_id);

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
        let no_connections = StateChange::new_connection(&sessions);
        if let StateChange::Connections(no_connections) = &no_connections {
            assert_eq!(&Vec::<&ClientId>::new(), no_connections);
        }

        let message: proto::Publication = no_connections.try_into().unwrap();
        matches_connection_publication(message, &[]);

        // One session
        let sessions: HashMap<ClientId, Session> = HashMap::from_iter((1..2).map(|i| {
            let id = format!("Session {}", i);
            let session = make_session(&id, (1..i).map(|j| format!("Subscription {}", j)), true);

            (id.into(), session)
        }));
        let one_connection = StateChange::new_connection(&sessions);
        if let StateChange::Connections(one_connection) = &one_connection {
            let expected: ClientId = "Session 1".into();
            assert_eq!(&vec![&expected], one_connection);
        }

        let message: proto::Publication = one_connection.try_into().unwrap();
        matches_connection_publication(message, &["Session 1"]);

        // Multiple sessions
        let sessions: HashMap<ClientId, Session> = HashMap::from_iter((1..4).map(|i| {
            let id = format!("Session {}", i);
            let session = make_session(&id, (1..i).map(|j| format!("Subscription {}", j)), true);

            (id.into(), session)
        }));
        let many_connections = StateChange::new_connection(&sessions);
        if let StateChange::Connections(many_connections) = &many_connections {
            let mut actual: Vec<&str> = many_connections.iter().map(|id| id.as_str()).collect();
            actual.sort();
            assert_eq!(vec!["Session 1", "Session 2", "Session 3"], actual);
        }

        let message: proto::Publication = many_connections.try_into().unwrap();
        matches_connection_publication(message, &["Session 1", "Session 2", "Session 3"]);

        // Offline sessions
        let sessions: HashMap<ClientId, Session> = HashMap::from_iter((1..8).map(|i| {
            let id = format!("Session {}", i);
            let session = make_session(
                &id,
                (1..i).map(|j| format!("Subscription {}", j)),
                i % 2 == 0, // even sessions are online, odd offline
            );

            (id.into(), session)
        }));
        let many_connections = StateChange::new_connection(&sessions);
        if let StateChange::Connections(many_connections) = &many_connections {
            let mut actual: Vec<&str> = many_connections.iter().map(|id| id.as_str()).collect();
            actual.sort();
            assert_eq!(vec!["Session 2", "Session 4", "Session 6"], actual);
        }

        let message: proto::Publication = many_connections.try_into().unwrap();
        matches_connection_publication(message, &["Session 2", "Session 4", "Session 6"]);
    }

    fn make_session<I, S>(id: &str, subscriptions: I, online: bool) -> Session
    where
        I: IntoIterator<Item = S>,
        S: AsRef<str>,
    {
        let subscriptions = HashMap::from_iter(subscriptions.into_iter().map(|s| {
            let s = s.as_ref();
            (
                s.to_owned(),
                Subscription::new(TopicFilter::from_str(s).unwrap(), proto::QoS::AtLeastOnce),
            )
        }));
        let state = SessionState::from_parts(id.into(), subscriptions, VecDeque::new());

        if online {
            Session::new_persistent(
                AuthId::Anonymous,
                ConnReq::new(
                    id.into(),
                    persistent_connect(id.to_owned()),
                    None,
                    connection_handle(),
                ),
                state,
            )
        } else {
            Session::new_offline(state)
        }
    }

    fn matches_subscription_publication(
        publication: proto::Publication,
        client_id: &str,
        body: &[&str],
    ) {
        matches_publication(
            publication,
            format!("$edgehub/subscriptions/{}", client_id),
            body,
        );
    }

    fn matches_connection_publication(publication: proto::Publication, body: &[&str]) {
        matches_publication(publication, "$edgehub/connected".to_owned(), body);
    }

    fn matches_publication(publication: proto::Publication, topic: String, body: &[&str]) {
        let proto::Publication {
            topic_name,
            qos,
            retain,
            payload,
        } = publication;

        assert_eq!(topic_name, topic);
        assert_eq!(qos, STATE_CHANGE_QOS);
        assert_eq!(retain, true);
        is_notify_equal(&payload, body);
    }
}
