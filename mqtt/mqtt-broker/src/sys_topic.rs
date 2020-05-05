use std::collections::HashMap;
use std::iter::FromIterator;

use mqtt3::proto;
use tracing::error;

use crate::session::Session;
use crate::ClientId;

pub enum StateChange<'a> {
    Subscriptions(&'a ClientId, Vec<&'a str>),
    Connections(Vec<&'a ClientId>),
    Sessions(Vec<&'a ClientId>),
}

impl<'a> StateChange<'a> {
    pub fn get_subscriptions(client_id: &'a ClientId, session: &'a Session) -> Self {
        Self::Subscriptions(
            client_id,
            session
                .subscriptions()
                .unwrap()
                .keys()
                .map(String::as_str)
                .collect(),
        )
    }

    pub fn clear_subscriptions(client_id: &'a ClientId) -> Self {
        Self::Subscriptions(client_id, vec![])
    }

    pub fn get_connections(sessions: &'a HashMap<ClientId, Session>) -> Self {
        Self::Connections(
            sessions
                .iter()
                .filter_map(|(client_id, session)| match session {
                    Session::Transient(_) => Some(client_id),
                    Session::Persistent(_) => Some(client_id),
                    _ => None,
                })
                .collect(),
        )
    }

    pub fn get_sessions(sessions: &'a HashMap<ClientId, Session>) -> Self {
        Self::Sessions(
            sessions
                .iter()
                .filter_map(|(client_id, session)| match session {
                    Session::Transient(_) => Some(client_id),
                    Session::Persistent(_) => Some(client_id),
                    Session::Offline(_) => Some(client_id),
                    _ => None,
                })
                .collect(),
        )
    }
}

impl<'a> From<StateChange<'a>> for proto::Publication {
    fn from(state: StateChange<'a>) -> Self {
        const QOS: proto::QoS = proto::QoS::AtLeastOnce;

        match state {
            StateChange::Subscriptions(client_id, subscriptions) => proto::Publication {
                topic_name: format!("$edgehub/subscriptions/{}", client_id),
                qos: QOS,
                retain: true,
                payload: get_message_body(subscriptions.into_iter()).into(),
            },
            StateChange::Connections(connections) => proto::Publication {
                topic_name: "$edgehub/connected".to_owned(),
                qos: QOS,
                retain: true,
                payload: get_message_body(connections.iter().map(|c| c.as_str())).into(),
            },
            StateChange::Sessions(sessions) => proto::Publication {
                topic_name: "$edgehub/sessions".to_owned(),
                qos: QOS,
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

    if payload.is_empty() {
        "".to_owned()
    } else {
        serde_json::to_string(&payload).unwrap_or_else(|e| {
            error!("Json Error: {}", e);
            "".to_owned()
        })
    }
}
