use std::convert::Infallible;

use mqtt_broker_core::{
    auth::{Activity, AuthId, Authorization, Authorizer, Connect, Operation, Publish, Subscribe},
    ClientId, ClientInfo,
};

#[derive(Debug, Default)]
pub struct EdgeHubAuthorizer;

#[allow(clippy::unused_self)]
impl EdgeHubAuthorizer {
    pub fn new() -> Self {
        Self::default()
    }

    fn authorize_connect(
        &self,
        client_id: &ClientId,
        client_info: &ClientInfo,
        _connect: &Connect,
    ) -> Authorization {
        match client_info.auth_id() {
            // forbid anonymous clients to connect to the broker
            AuthId::Anonymous => Authorization::Forbidden("anonymous client".to_string()),
            // allow only those clients whose auth_id and client_id identical
            AuthId::Identity(identity) => {
                if identity == client_id.as_str() {
                    Authorization::Allowed
                } else {
                    Authorization::Forbidden(format!("unknown client: {}", client_id))
                }
            }
        }
    }

    fn authorize_publish(
        &self,
        client_id: &ClientId,
        client_info: &ClientInfo,
        publish: &Publish,
    ) -> Authorization {
        if is_iothub_topic(&publish.publication.topic_name) {
            // run authorization rules for publication to IoTHub topic
            self.authorize_iothub_topic(client_id, client_info, &publish.publication.topic_name)
        } else {
            // allow any client to publish to any non-iothub topics
            Authorization::Allowed
        }
    }

    fn authorize_subscribe(
        &self,
        client_id: &ClientId,
        client_info: &ClientInfo,
        subscribe: &Subscribe,
    ) -> Authorization {
        if subscribe.topic_filter() == "#" {
            Authorization::Forbidden("# is forbidden topic filter".to_string())
        } else if is_iothub_topic(subscribe.topic_filter()) {
            // run authorization rules for subscription to IoTHub topic
            self.authorize_iothub_topic(client_id, client_info, subscribe.topic_filter())
        } else {
            // allow any client to subscribe to any non-iothub topics
            Authorization::Allowed
        }
    }

    fn authorize_iothub_topic(
        &self,
        client_id: &ClientId,
        client_info: &ClientInfo,
        topic: &str,
    ) -> Authorization {
        match client_info.auth_id() {
            // forbid anonymous clients to subscribe to restricted topics
            AuthId::Anonymous => {
                Authorization::Forbidden("IoTHub topic: anonymous client".to_string())
            }
            // allow authenticated clients with client_id == auth_id and accessing its own IoTHub topic
            AuthId::Identity(identity) if identity == client_id.as_str() => {
                let allowed_topics = allowed_iothub_topic(client_id);
                if allowed_topics
                    .iter()
                    .any(|allowed_topic| topic.starts_with(allowed_topic))
                {
                    Authorization::Allowed
                } else {
                    Authorization::Forbidden(
                        "IoTHub topic: client must connect to its own topic".to_string(),
                    )
                }
            }
            // forbid access otherwise
            AuthId::Identity(identity) => Authorization::Forbidden(format!(
                "IoTHub topic: client_id {} doesn't match auth_id {}",
                client_id, identity
            )),
        }
    }
}

fn is_iothub_topic(topic: &str) -> bool {
    topic.starts_with("$edgehub/") || topic.starts_with("$iothub/")
}

fn allowed_iothub_topic(client_id: &ClientId) -> Vec<String> {
    vec![
        format!("$edgehub/clients/{}/messages/events", client_id),
        format!("$iothub/clients/{}/messages/events", client_id),
        format!("$edgehub/clients/{}/messages/c2d/post", client_id),
        format!("$iothub/clients/{}/messages/c2d/post", client_id),
        format!(
            "$iothub/clients/{}/twin/patch/properties/desired",
            client_id
        ),
        format!(
            "$edgehub/clients/{}/twin/patch/properties/desired",
            client_id
        ),
        format!(
            "$iothub/clients/{}/twin/patch/properties/reported",
            client_id
        ),
        format!(
            "$edgehub/clients/{}/twin/patch/properties/reported",
            client_id
        ),
        format!("$edgehub/clients/{}/twin/get", client_id),
        format!("$iothub/clients/{}/twin/get", client_id),
        format!("$edgehub/clients/{}/twin/res", client_id),
        format!("$iothub/clients/{}/twin/res", client_id),
        format!("$edgehub/clients/{}/methods/post", client_id),
        format!("$iothub/clients/{}/methods/post", client_id),
        format!("$edgehub/clients/{}/methods/res", client_id),
        format!("$iothub/clients/{}/methods/res", client_id),
    ]
}

impl Authorizer for EdgeHubAuthorizer {
    type Error = Infallible;

    fn authorize(&self, activity: Activity) -> Result<Authorization, Self::Error> {
        let (client_id, client_info, operation) = activity.into_parts();
        let auth = match operation {
            Operation::Connect(connect) => {
                self.authorize_connect(&client_id, &client_info, &connect)
            }
            Operation::Publish(publish) => {
                self.authorize_publish(&client_id, &client_info, &publish)
            }
            Operation::Subscribe(subscribe) => {
                self.authorize_subscribe(&client_id, &client_info, &subscribe)
            }
        };

        Ok(auth)
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use matches::assert_matches;
    use test_case::test_case;

    use mqtt3::proto;
    use mqtt_broker_core::{
        auth::{Activity, AuthId, Authorization, Authorizer, Operation},
        ClientInfo,
    };

    use super::EdgeHubAuthorizer;

    #[test_case(connect_activity("device-1", AuthId::Identity("device-1".to_string())); "identical auth_id and client_id")]
    fn it_allows_to_connect(activity: Activity) {
        let authorizer = EdgeHubAuthorizer::new();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(connect_activity("device-1", AuthId::Anonymous); "anonymous clients")]
    #[test_case(connect_activity("device-1", AuthId::Identity("device-2".to_string())); "different auth_id and client_id")]
    fn it_forbids_to_connect(activity: Activity) {
        let authorizer = EdgeHubAuthorizer::new();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "topic"); "generic MQTT topic")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$SYS/connected"); "SYS topics")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/messages/events"); "old client events")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/messages/events"); "new client events")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/messages/c2d/post"); "old client C2D messages")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/messages/c2d/post"); "new client C2D messages")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/twin/patch/properties/desired"); "old client update desired")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/twin/patch/properties/desired"); "new client update desired")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/twin/patch/properties/reported"); "old client update reported")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/twin/patch/properties/reported"); "new client update reported")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/twin/get"); "old client twin request")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/twin/get"); "new client twin request")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/twin/res"); "old client twin response")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/twin/res"); "new client twin response")]
    fn it_allows_to_subscribe_to(activity: Activity) {
        let authorizer = EdgeHubAuthorizer::new();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "#"); "everything")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/#"); "everything with edgehub prefixed")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/#"); "everything with iothub prefixed")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-2".to_string()), "$edgehub/clients/device-1/twin/get"); "twin request for another client")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/+/twin/get"); "twin request for any device")]
    #[test_case(subscribe_activity("device-1", AuthId::Anonymous, "$edgehub/clients/device-1/twin/get"); "twin request by anonymous client")]
    #[test_case(subscribe_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/twin/+"); "both twin operations")]
    fn it_forbids_to_subscribe_to(activity: Activity) {
        let authorizer = EdgeHubAuthorizer::new();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "topic"); "generic MQTT topic")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$SYS/connected"); "SYS topics")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/messages/events"); "old client events")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/messages/events"); "new client events")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/messages/c2d/post"); "old client C2D messages")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/messages/c2d/post"); "new client C2D messages")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/twin/patch/properties/desired"); "old client update desired")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/twin/patch/properties/desired"); "new client update desired")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/twin/patch/properties/reported"); "old client update reported")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/twin/patch/properties/reported"); "new client update reported")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/twin/get"); "old client twin request")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/twin/get"); "new client twin request")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/clients/device-1/twin/res"); "old client twin response")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/clients/device-1/twin/res"); "new client twin response")]
    fn it_allows_to_publish_to(activity: Activity) {
        let authorizer = EdgeHubAuthorizer::new();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$edgehub/some/topic"); "any edgehub prefixed topic")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-1".to_string()), "$iothub/some/topic"); "any iothub prefixed topic")]
    #[test_case(publish_activity("device-1", AuthId::Identity("device-2".to_string()), "$edgehub/clients/device-1/twin/get"); "twin request for another client")]
    #[test_case(publish_activity("device-1", AuthId::Anonymous, "$edgehub/clients/device-1/twin/get"); "twin request by anonymous client")]
    fn it_forbids_to_publish_to(activity: Activity) {
        let authorizer = EdgeHubAuthorizer::new();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    fn connect_activity(client_id: &str, auth_id: AuthId) -> Activity {
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

    fn publish_activity(client_id: &str, auth_id: AuthId, topic_name: &str) -> Activity {
        let publish = proto::Publish {
            packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
            retain: false,
            topic_name: topic_name.into(),
            payload: "data".into(),
        };

        let operation = Operation::new_publish(publish);
        activity(operation, client_id, auth_id)
    }

    fn subscribe_activity(client_id: &str, auth_id: AuthId, topic_filter: &str) -> Activity {
        let subscribe = proto::SubscribeTo {
            topic_filter: topic_filter.into(),
            qos: proto::QoS::AtLeastOnce,
        };

        let operation = Operation::new_subscribe(subscribe);
        activity(operation, client_id, auth_id)
    }

    fn activity(operation: Operation, client_id: &str, auth_id: AuthId) -> Activity {
        let client_info = ClientInfo::new("10.0.0.1:12345".parse().unwrap(), auth_id);
        Activity::new(client_id, client_info, operation)
    }
}
