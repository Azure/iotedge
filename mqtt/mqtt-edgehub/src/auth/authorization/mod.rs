mod edgehub;
mod local;
mod policy;

pub use self::policy::{PolicyAuthorizer, PolicyUpdate};
pub use edgehub::{AuthorizerUpdate, EdgeHubAuthorizer, IdentityUpdate};
pub use local::LocalAuthorizer;

#[cfg(test)]
mod tests {
    use mqtt3::proto;
    use mqtt_broker::{auth::Activity, auth::Operation, AuthId, ClientInfo};

    pub(crate) fn connect_activity(client_id: &str, auth_id: impl Into<AuthId>) -> Activity {
        let operation = Operation::new_connect();
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
        let client_info =
            ClientInfo::new(client_id, "10.0.0.1:12345".parse().unwrap(), auth_id.into());
        Activity::new(client_info, operation)
    }
}
