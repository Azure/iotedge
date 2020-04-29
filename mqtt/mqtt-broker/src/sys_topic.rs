use std::collections::HashMap;

use bytes::Bytes;
use mqtt3::proto;

use crate::session::Session;
use crate::ClientId;

pub enum StateChange<'a> {
    Subscriptions(&'a Session),
    AddConnection(&'a HashMap<ClientId, Session>),
    RemoveConnection(&'a HashMap<ClientId, Session>, &'a ClientId),
}

pub enum SysMessage {
    Single(proto::Publication),
    Multiple(Vec<proto::Publication>),
    None,
}

pub fn get_sys_message(change: &StateChange<'_>) -> SysMessage {
    match change {
        StateChange::Subscriptions(session) => get_subscription(session),
        StateChange::AddConnection(sessions) => get_connect(sessions),
        StateChange::RemoveConnection(sessions, client_id) => get_disconnect(sessions, client_id),
    }
}

fn get_subscription(session: &Session) -> SysMessage {
    let state = match session {
        // get session state
        Session::Transient(connected) => connected.state(),
        Session::Persistent(connected) => connected.state(),
        Session::Offline(offline) => offline.state(),
        _ => return SysMessage::None,
    };

    let topics = state
        .subscriptions()
        .keys() // get topic filters
        .map(|t| t.as_ref())
        .collect::<Vec<&str>>()
        .join(r"\u{0000}"); // join to string for payload

    let client_id = state.client_id();

    let publication = proto::Publication {
        topic_name: format!("$sys/subscriptions/{}", client_id),
        qos: proto::QoS::AtMostOnce, //no ack
        retain: true,
        payload: Bytes::from(topics),
    };

    SysMessage::Single(publication)
}

fn get_connect(sessions: &HashMap<ClientId, Session>) -> SysMessage {
    SysMessage::Multiple(vec![
        get_connection_change(sessions),
        get_session_change(sessions),
    ])
}

fn get_disconnect(sessions: &HashMap<ClientId, Session>, client_id: &ClientId) -> SysMessage {
    let mut result = Vec::new();
    if sessions.get(client_id).is_none() {
        // If transient session is closed, remove its subscriptions
        result.push(proto::Publication {
            topic_name: format!("$sys/subscriptions/{}", client_id),
            qos: proto::QoS::AtMostOnce, //no ack
            retain: true,
            payload: "".into(),
        });
    }

    result.push(get_connection_change(sessions));
    result.push(get_session_change(sessions));

    SysMessage::Multiple(result)
}

fn get_connection_change(sessions: &HashMap<ClientId, Session>) -> proto::Publication {
    let connected: String = sessions
        .iter()
        .filter_map(|(client_id, session)| match session {
            Session::Transient(_) => Some(client_id.as_str()),
            Session::Persistent(_) => Some(client_id.as_str()),
            _ => None,
        })
        .collect::<Vec<&str>>()
        .join(r"\u{0000}");

    proto::Publication {
        topic_name: "$sys/connected".to_owned(),
        qos: proto::QoS::AtMostOnce, //no ack
        retain: true,
        payload: connected.into(),
    }
}

fn get_session_change(sessions: &HashMap<ClientId, Session>) -> proto::Publication {
    let existing_sessions: String = sessions
        .iter()
        .filter_map(|(client_id, session)| match session {
            Session::Transient(_) => Some(client_id.as_str()),
            Session::Persistent(_) => Some(client_id.as_str()),
            Session::Offline(_) => Some(client_id.as_str()),
            _ => None,
        })
        .collect::<Vec<&str>>()
        .join(r"\u{0000}");

    proto::Publication {
        topic_name: "$sys/sessions".to_owned(),
        qos: proto::QoS::AtMostOnce, //no ack
        retain: true,
        payload: existing_sessions.into(),
    }
}
