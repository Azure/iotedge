use std::collections::HashMap;
use std::iter::FromIterator;

use mqtt3::proto;
use tracing::error;

use crate::session::Session;
use crate::ClientId;

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

impl<'a> From<StateChange<'a>> for proto::Publication {
    fn from(state: StateChange<'a>) -> Self {
        const STATE_CHANGE_QOS: proto::QoS = proto::QoS::AtLeastOnce;

        match state {
            StateChange::Subscriptions(client_id, subscriptions) => {
                let payload = if let Some(subscriptions) = subscriptions {
                    get_message_body(subscriptions.into_iter()).into()
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
                payload: get_message_body(connections.iter().map(|c| c.as_str())).into(),
            },
            StateChange::Sessions(sessions) => proto::Publication {
                topic_name: "$edgehub/sessions".to_owned(),
                qos: STATE_CHANGE_QOS,
                retain: true,
                payload: get_message_body(sessions.iter().map(|c| c.as_str())).into(),
            },
        }
    }
}

fn get_message_body<'a, P>(payload: P) -> String
where
    P: IntoIterator<Item = &'a str>,
{
    let payload: Vec<&str> = Vec::from_iter(payload);

    serde_json::to_string(&payload).unwrap_or_else(|e| {
        error!("Json Error: {}", e);
        "".to_owned()
    })
}
