mod edgehub;
mod iothub;
mod local;
mod policy;

pub use self::policy::PolicyAuthorizer;
pub use edgehub::EdgeHubAuthorizer;
pub use iothub::{IotHubAuthorizer, ServiceIdentity};
pub use local::LocalAuthorizer;

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use mqtt3::proto;
    use mqtt_broker::{auth::Activity, auth::Operation, AuthId, ClientInfo};

    pub(crate) fn connect_activity(client_id: &str, auth_id: impl Into<AuthId>) -> Activity {
        let connect = proto::Connect {
            username: None,
            password: None,
            will: None,
            client_id: proto::ClientId::IdWithCleanSession(client_id.into()),
            keep_alive: Duration::from_secs(1),
            protocol_name: mqtt3::PROTOCOL_NAME.to_string(),
            protocol_level: mqtt3::PROTOCOL_LEVEL,
        };

        let operation = Operation::new_connect(connect);
        activity(operation, client_id, auth_id)
    }

    pub(crate) fn publish_activity(
        client_id: &str,
        auth_id: impl Into<AuthId>,
        topic_name: &str,
    ) -> Activity {
        let publish = proto::Publish {
            packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
            retain: false,
            topic_name: topic_name.into(),
            payload: "data".into(),
        };

        let operation = Operation::new_publish(publish);
        activity(operation, client_id, auth_id)
    }

    pub(crate) fn subscribe_activity(
        client_id: &str,
        auth_id: impl Into<AuthId>,
        topic_filter: &str,
    ) -> Activity {
        let subscribe = proto::SubscribeTo {
            topic_filter: topic_filter.into(),
            qos: proto::QoS::AtLeastOnce,
        };

        let operation = Operation::new_subscribe(subscribe);
        activity(operation, client_id, auth_id)
    }

    fn activity(operation: Operation, client_id: &str, auth_id: impl Into<AuthId>) -> Activity {
        let client_info = ClientInfo::new("10.0.0.1:12345".parse().unwrap(), auth_id.into());
        Activity::new(client_id, client_info, operation)
    }
}
