#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::cognitive_complexity,
    clippy::large_enum_variant,
    clippy::similar_names,
    clippy::module_name_repetitions,
    clippy::use_self,
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

pub(crate) const IDENTITY_VAR: &str = "{{iot:identity}}";
pub(crate) const DEVICE_ID_VAR: &str = "{{iot:device_id}}";
pub(crate) const MODULE_ID_VAR: &str = "{{iot:module_id}}";
pub(crate) const CLIENT_ID_VAR: &str = "{{mqtt:client_id}}";
pub(crate) const EDGEHUB_ID_VAR: &str = "{{iot:this_device_id}}";

#[cfg(test)]
mod tests {
    use bytes::Bytes;
    use mqtt3::proto;
    use mqtt_broker::{auth::Activity, auth::Operation, AuthId, ClientId, ClientInfo};

    pub(crate) fn create_connect_activity(
        client_id: impl Into<ClientId>,
        auth_id: impl Into<AuthId>,
    ) -> Activity {
        let client_id = client_id.into();
        Activity::new(
            ClientInfo::new(client_id, "127.0.0.1:80".parse().unwrap(), auth_id),
            Operation::new_connect(),
        )
    }

    pub(crate) fn create_publish_activity(
        client_id: impl Into<ClientId>,
        auth_id: impl Into<AuthId>,
    ) -> Activity {
        Activity::new(
            ClientInfo::new(client_id.into(), "127.0.0.1:80".parse().unwrap(), auth_id),
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
