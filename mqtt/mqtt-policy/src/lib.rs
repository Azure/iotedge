#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
    clippy::match_same_arms,
    clippy::must_use_candidate,
    clippy::missing_errors_doc
)]

mod errors;
mod matcher;
mod substituter;
mod validator;

pub use crate::matcher::MqttTopicFilterMatcher;
pub use crate::substituter::MqttSubstituter;
pub use crate::validator::MqttValidator;

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use bytes::Bytes;
    use mqtt3::proto;
    use mqtt_broker::{auth::Activity, auth::Operation, AuthId, ClientId, ClientInfo};

    pub(crate) fn create_connect_activity(
        client_id: impl Into<ClientId>,
        auth_id: impl Into<AuthId>,
    ) -> Activity {
        let client_id = client_id.into();
        Activity::new(
            client_id.clone(),
            ClientInfo::new("127.0.0.1:80".parse().unwrap(), auth_id),
            Operation::new_connect(proto::Connect {
                username: None,
                password: None,
                will: None,
                client_id: proto::ClientId::IdWithExistingSession(client_id.to_string()),
                keep_alive: Duration::default(),
                protocol_name: mqtt3::PROTOCOL_NAME.into(),
                protocol_level: mqtt3::PROTOCOL_LEVEL,
            }),
        )
    }

    pub(crate) fn create_publish_activity(
        client_id: impl Into<ClientId>,
        auth_id: impl Into<AuthId>,
    ) -> Activity {
        Activity::new(
            client_id.into(),
            ClientInfo::new("127.0.0.1:80".parse().unwrap(), auth_id),
            Operation::new_publish(proto::Publish {
                packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtLeastOnce(
                    proto::PacketIdentifier::new(1).unwrap(),
                    false,
                ),
                retain: true,
                topic_name: "/foo/bar".to_string(),
                payload: Bytes::new(),
            }),
        )
    }
}
