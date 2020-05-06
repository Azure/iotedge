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
        let session = make_session(expected_id.as_str(), vec![]);
        let no_subs = StateChange::new_subscription(&expected_id, &session);
        if let StateChange::Subscriptions(stored_id, stored_subs) = &no_subs {
            assert_eq!(&&expected_id, stored_id);
            assert_eq!(&Vec::<&str>::new(), stored_subs.as_ref().unwrap());
        }

        let message: proto::Publication = no_subs.try_into().unwrap();
        matches_subscription_publication(message, expected_id.as_str(), &[]);

        // let single_session = make_session(
        //     "Session".to_owned(),
        //     (1..5).map(|i| format!("Subscription {}", i)),
        // );

        // let sessions: HashMap<ClientId, Session> = HashMap::from_iter((1..5).map(|i| {
        //     let id = format!("Session {}", i);
        //     let session = make_session(&id, (1..i).map(|j| format!("Subscription {}", j)));

        //     (id.into(), session)
        // }));
        // assert_eq!(true, false);
    }

    fn make_session<S>(id: &str, subscriptions: S) -> Session
    where
        S: IntoIterator<Item = String>,
    {
        let subscriptions = HashMap::from_iter(subscriptions.into_iter().map(|s| {
            (
                s.clone(),
                Subscription::new(TopicFilter::from_str(&s).unwrap(), proto::QoS::AtLeastOnce),
            )
        }));
        let state = SessionState::from_parts(id.into(), subscriptions, VecDeque::new());

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
    }

    fn matches_subscription_publication(
        publication: proto::Publication,
        client_id: &str,
        body: &[&str],
    ) {
        let proto::Publication {
            topic_name,
            qos,
            retain,
            payload,
        } = publication;

        assert_eq!(topic_name, format!("$edgehub/subscriptions/{}", client_id));
        assert_eq!(qos, STATE_CHANGE_QOS);
        assert_eq!(retain, true);
        is_notify_equal(&payload, body);
    }
}
