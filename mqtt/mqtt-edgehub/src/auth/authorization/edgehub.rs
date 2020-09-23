use std::{any::Any, cell::RefCell, collections::HashMap, convert::Infallible, fmt};

use serde::{Deserialize, Serialize};
use tracing::debug;

use mqtt_broker::{
    auth::{Activity, Authorization, Authorizer, Connect, Operation, Publish, Subscribe},
    AuthId, ClientId, ClientInfo,
};

#[derive(Debug, Default)]
pub struct EdgeHubAuthorizer {
    iothub_allowed_topics: RefCell<HashMap<ClientId, Vec<String>>>,
    service_identities_cache: HashMap<ClientId, ServiceIdentity>,
}

impl EdgeHubAuthorizer {
    #[allow(clippy::unused_self)]
    fn authorize_connect(
        &self,
        client_id: &ClientId,
        client_info: &ClientInfo,
        _connect: &Connect,
    ) -> Authorization {
        match client_info.auth_id() {
            // forbid anonymous clients to connect to the broker
            AuthId::Anonymous => {
                Authorization::Forbidden("Anonymous clients cannot connect to broker".to_string())
            }
            // allow only those clients whose auth_id and client_id identical
            AuthId::Identity(identity) => {
                if identity == client_id {
                    Authorization::Allowed
                } else {
                    Authorization::Forbidden(format!(
                        "client_id {} does not match registered iothub identity id.",
                        client_id
                    ))
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
        let topic = publish.publication().topic_name();

        if is_iothub_topic(topic) {
            // run authorization rules for publication to IoTHub topic
            self.authorize_iothub_topic(client_id, client_info, topic)
        } else if is_forbidden_topic(topic) {
            // forbid any clients to access restricted topics
            Authorization::Forbidden(format!("{} is forbidden topic filter", topic))
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
        let topic = subscribe.topic_filter();

        if is_iothub_topic(topic) {
            // run authorization rules for subscription to IoTHub topic
            self.authorize_iothub_topic(client_id, client_info, topic)
        } else if is_forbidden_topic_filter(topic) {
            // forbid any clients to access restricted topics
            Authorization::Forbidden(format!("{} is forbidden topic filter", topic))
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
            AuthId::Anonymous => Authorization::Forbidden(
                "Anonymous clients do not have access to IoTHub topics".to_string(),
            ),
            // allow authenticated clients with client_id == auth_id and accessing its own IoTHub topic
            AuthId::Identity(identity) if identity == client_id => {
                if self.is_iothub_topic_allowed(client_id, topic)
                    && self.check_authorized_cache(client_id, topic)
                {
                    Authorization::Allowed
                } else {
                    Authorization::Forbidden(
                        "Client must connect to its own IoTHub topic".to_string(),
                    )
                }
            }
            // forbid access otherwise
            AuthId::Identity(_) => Authorization::Forbidden(format!(
                "client_id {} must match registered iothub identity id to access IoTHub topic",
                client_id
            )),
        }
    }

    fn is_iothub_topic_allowed(&self, client_id: &ClientId, topic: &str) -> bool {
        let mut iothub_allowed_topics = self.iothub_allowed_topics.borrow_mut();
        let allowed_topics = iothub_allowed_topics
            .entry(client_id.clone())
            .or_insert_with(|| allowed_iothub_topic(&client_id));

        allowed_topics
            .iter()
            .any(|allowed_topic| topic.starts_with(allowed_topic))
    }

    fn get_on_behalf_of_id(topic: &str) -> Option<ClientId> {
        // topics without the new topic format cannot have on_behalf_of_ids
        if !topic.starts_with("$iothub/clients") {
            return None;
        }
        let topic_parts = topic.split('/').collect::<Vec<_>>();
        let device_id = topic_parts.get(2);
        let module_id = match topic_parts.get(3) {
            Some(s) if *s == "modules" => topic_parts.get(4),
            _ => None,
        };

        match (device_id, module_id) {
            (Some(device_id), Some(module_id)) => {
                Some(format!("{}/{}", device_id, module_id).into())
            }
            (Some(device_id), None) => Some((*device_id).into()),
            _ => None,
        }
    }

    fn check_authorized_cache(&self, client_id: &ClientId, topic: &str) -> bool {
        match EdgeHubAuthorizer::get_on_behalf_of_id(topic) {
            Some(on_behalf_of_id) if client_id == &on_behalf_of_id => {
                self.service_identities_cache.contains_key(client_id)
            }
            Some(on_behalf_of_id) => self
                .service_identities_cache
                .get(&on_behalf_of_id)
                .and_then(ServiceIdentity::auth_chain)
                .map_or(false, |auth_chain| auth_chain.contains(client_id.as_str())),
            None => {
                // If there is no on_behalf_of_id, we are dealing with a legacy topic
                // The client_id must still be in the identities cache
                self.service_identities_cache.contains_key(client_id)
            }
        }
    }
}

const FORBIDDEN_TOPIC_FILTER_PREFIXES: [&str; 2] = ["#", "$"];

fn is_forbidden_topic_filter(topic_filter: &str) -> bool {
    FORBIDDEN_TOPIC_FILTER_PREFIXES
        .iter()
        .any(|prefix| topic_filter.starts_with(prefix))
}

fn is_forbidden_topic(topic_filter: &str) -> bool {
    topic_filter.starts_with('$')
}

const IOTHUB_TOPICS_PREFIX: [&str; 2] = ["$edgehub/", "$iothub/"];

fn is_iothub_topic(topic: &str) -> bool {
    IOTHUB_TOPICS_PREFIX
        .iter()
        .any(|prefix| topic.starts_with(prefix))
}

fn allowed_iothub_topic(client_id: &ClientId) -> Vec<String> {
    let client_id_parts = client_id.as_str().split('/').collect::<Vec<&str>>();
    let x = match client_id_parts.len() {
        1 => client_id_parts[0].to_string(),
        2 => format!("{}/modules/{}", client_id_parts[0], client_id_parts[1]),
        _ => {
            panic!("ClientId cannot have more than deviceId and moduleId");
        }
    };
    vec![
        format!("$edgehub/{}/messages/events", client_id),
        format!("$edgehub/{}/messages/c2d/post", client_id),
        format!("$edgehub/{}/twin/desired", client_id),
        format!("$edgehub/{}/twin/reported", client_id),
        format!("$edgehub/{}/twin/get", client_id),
        format!("$edgehub/{}/twin/res", client_id),
        format!("$edgehub/{}/methods/post", client_id),
        format!("$edgehub/{}/methods/res", client_id),
        format!("$edgehub/{}/inputs", client_id),
        format!("$edgehub/{}/outputs", client_id),
        format!("$iothub/clients/{}/messages/events", x),
        format!("$iothub/clients/{}/messages/c2d/post", x),
        format!("$iothub/clients/{}/twin/patch/properties/desired", x),
        format!("$iothub/clients/{}/twin/patch/properties/reported", x),
        format!("$iothub/clients/{}/twin/get", x),
        format!("$iothub/clients/{}/twin/res", x),
        format!("$iothub/clients/{}/methods/post", x),
        format!("$iothub/clients/{}/methods/res", x),
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

    fn update(&mut self, update: Box<dyn Any>) -> Result<(), Self::Error> {
        let identities = update.as_ref();
        if let Some(service_identities) = identities.downcast_ref::<Vec<ServiceIdentity>>() {
            debug!(
                "service identities update received. Service identities: {:?}",
                service_identities
            );

            self.service_identities_cache = service_identities
                .iter()
                .map(|id| (ClientId::from(id.identity()), id.clone()))
                .collect();
        }
        Ok(())
    }
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct ServiceIdentity {
    #[serde(rename = "Identity")]
    identity: String,

    #[serde(rename = "AuthChain")]
    auth_chain: Option<String>,
}

impl ServiceIdentity {
    pub fn identity(&self) -> &str {
        &self.identity
    }
    pub fn auth_chain(&self) -> Option<&str> {
        self.auth_chain.as_deref()
    }
}

impl fmt::Display for ServiceIdentity {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match &self.auth_chain {
            Some(auth_chain) => {
                write!(f, "Identity: {}; Auth_Chain: {}", self.identity, auth_chain)
            }
            None => write!(f, "Identity: {}", self.identity),
        }
    }
}

#[cfg(test)]
mod tests {
    use crate::auth::EdgeHubAuthorizer;
    use std::time::Duration;

    use matches::assert_matches;
    use test_case::test_case;

    use mqtt3::proto;
    use mqtt_broker::{
        auth::Authorizer,
        auth::{Activity, AuthId, Authorization, Operation},
        ClientInfo,
    };

    use super::ServiceIdentity;

    #[test_case(connect_activity("device-1", "device-1"); "identical auth_id and client_id")]
    fn it_allows_to_connect(activity: Activity) {
        let authorizer = authorizer();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(connect_activity("device-1", AuthId::Anonymous); "anonymous clients")]
    #[test_case(connect_activity("device-1", "device-2"); "different auth_id and client_id")]
    fn it_forbids_to_connect(activity: Activity) {
        let authorizer = authorizer();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    #[test_case(subscribe_activity("device-1", "device-1", "topic"); "generic MQTT topic")]
    #[test_case(subscribe_activity("device-1", "device-1", "$edgehub/device-1/messages/events"); "device events")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/messages/events"); "edge module events")]
    #[test_case(subscribe_activity("device-1", "device-1", "$edgehub/device-1/messages/c2d/post"); "device C2D messages")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/messages/c2d/post"); "edge module C2D messages")]
    #[test_case(subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/desired"); "device update desired properties")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/desired"); "edge module update desired properties")]
    #[test_case(subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/reported"); "device update reported properties")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/reported"); "edge module update reported properties")]
    #[test_case(subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/get"); "device twin request")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/get"); "edge module twin request")]
    #[test_case(subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/res"); "device twin response")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/inputs/route1"); "edge module access M2M inputs")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/outputs/route1"); "edge module access M2M outputs")]
    #[test_case(subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/messages/events"); "iothub telemetry")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/messages/events"); "iothub telemetry with moduleId")]
    #[test_case(subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/messages/c2d/post"); "iothub c2d messages")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/messages/c2d/post"); "iothub c2d messages with moduleId")]
    #[test_case(subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/twin/patch/properties/desired"); "iothub update desired properties")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/patch/properties/desired"); "iothub update desired properties with moduleId")]
    #[test_case(subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/twin/patch/properties/reported"); "iothub update reported properties")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/patch/properties/reported"); "iothub update reported properties with moduleId")]
    #[test_case(subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/twin/get"); "iothub device twin request")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/get"); "iothub module twin request")]
    #[test_case(subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/twin/res"); "iothub device twin response")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/res"); "iothub module twin response")]
    #[test_case(subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/methods/post"); "iothub device DM request")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/methods/post"); "iothub module DM request")]
    #[test_case(subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/methods/res"); "iothub device DM response")]
    #[test_case(subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/methods/res"); "iothub module DM response")]
    fn it_allows_to_subscribe_to(activity: Activity) {
        let authorizer = authorizer();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(subscribe_activity("device-1", "device-1", "#"); "everything")]
    #[test_case(subscribe_activity("device-1", "device-1", "$SYS/connected"); "SYS topics")]
    #[test_case(subscribe_activity("device-1", "device-1", "$CUSTOM/topic"); "any special topics")]
    #[test_case(subscribe_activity("device-1", "device-1", "$edgehub/#"); "everything with edgehub prefixed")]
    #[test_case(subscribe_activity("device-1", "device-1", "$iothub/#"); "everything with iothub prefixed")]
    #[test_case(subscribe_activity("device-1", "device-1", "$upstream/#"); "everything with upstream prefixed")]
    #[test_case(subscribe_activity("device-1", "device-1", "$downstream/#"); "everything with downstream prefixed")]
    #[test_case(subscribe_activity("device-1", "device-2", "$edgehub/device-1/twin/get"); "twin request for another client")]
    #[test_case(subscribe_activity("device-1", "device-1", "$edgehub/+/twin/get"); "twin request for any device")]
    #[test_case(subscribe_activity("device-1", AuthId::Anonymous, "$edgehub/device-1/twin/get"); "twin request by anonymous client")]
    #[test_case(subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/+"); "both twin operations")]
    fn it_forbids_to_subscribe_to(activity: Activity) {
        let authorizer = authorizer();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    #[test_case(publish_activity("device-1", "device-1", "topic"); "generic MQTT topic")]
    #[test_case(publish_activity("device-1", "device-1", "$edgehub/device-1/messages/events"); "device events")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/messages/events"); "edge module events")]
    #[test_case(publish_activity("device-1", "device-1", "$edgehub/device-1/messages/c2d/post"); "device C2D messages")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/messages/c2d/post"); "edge module C2D messages")]
    #[test_case(publish_activity("device-1", "device-1", "$edgehub/device-1/twin/desired"); "device update desired properties")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/desired"); "edge module update desired properties")]
    #[test_case(publish_activity("device-1", "device-1", "$edgehub/device-1/twin/reported"); "device update reported properties")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/reported"); "edge module update reported properties")]
    #[test_case(publish_activity("device-1", "device-1", "$edgehub/device-1/twin/get"); "device twin request")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/get"); "edge module twin request")]
    #[test_case(publish_activity("device-1", "device-1", "$edgehub/device-1/twin/res"); "device twin response")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/res"); "edge module twin response")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/outputs/route1"); "edge module access M2M outputs")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/inputs/route1"); "edge module access M2M inputs")]
    #[test_case(publish_activity("device-1", "device-1", "$iothub/clients/device-1/messages/events"); "iothub telemetry")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/messages/events"); "iothub telemetry with moduleId")]
    #[test_case(publish_activity("device-1", "device-1", "$iothub/clients/device-1/messages/c2d/post"); "iothub c2d messages")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/messages/c2d/post"); "iothub c2d messages with moduleId")]
    #[test_case(publish_activity("device-1", "device-1", "$iothub/clients/device-1/twin/patch/properties/desired"); "iothub update desired properties")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/patch/properties/desired"); "iothub update desired properties with moduleId")]
    #[test_case(publish_activity("device-1", "device-1", "$iothub/clients/device-1/twin/patch/properties/reported"); "iothub update reported properties")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/patch/properties/reported"); "iothub update reported properties with moduleId")]
    #[test_case(publish_activity("device-1", "device-1", "$iothub/clients/device-1/twin/get"); "iothub device twin request")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/get"); "iothub module twin request")]
    #[test_case(publish_activity("device-1", "device-1", "$iothub/clients/device-1/twin/res"); "iothub device twin response")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/res"); "iothub module twin response")]
    #[test_case(publish_activity("device-1", "device-1", "$iothub/clients/device-1/methods/post"); "iothub device DM request")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/methods/post"); "iothub module DM request")]
    #[test_case(publish_activity("device-1", "device-1", "$iothub/clients/device-1/methods/res"); "iothub device DM response")]
    #[test_case(publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/methods/res"); "iothub module DM response")]
    fn it_allows_to_publish_to(activity: Activity) {
        let authorizer = authorizer();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(publish_activity("device-1", "device-1", "$edgehub/some/topic"); "any edgehub prefixed topic")]
    #[test_case(publish_activity("device-1", "device-1", "$iothub/some/topic"); "any iothub prefixed topic")]
    #[test_case(publish_activity("device-1", "device-1", "$downstream/some/topic"); "any downstream prefixed topics")]
    #[test_case(publish_activity("device-1", "device-1", "$upstream/some/topic"); "any upstream prefixed topics")]
    #[test_case(publish_activity("device-1", "device-2", "$edgehub/device-1/twin/get"); "twin request for another client")]
    #[test_case(publish_activity("device-1", AuthId::Anonymous, "$edgehub/device-1/twin/get"); "twin request by anonymous client")]
    #[test_case(publish_activity("device-1", "device-1", "$SYS/foo"); "any system topic")]
    #[test_case(publish_activity("device-1", "device-1", "$CUSTOM/foo"); "any special topic")]
    fn it_forbids_to_publish_to(activity: Activity) {
        let authorizer = authorizer();

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    fn authorizer() -> EdgeHubAuthorizer {
        let mut authorizer = EdgeHubAuthorizer::default();

        let service_identity = ServiceIdentity {
            identity: "device-1".to_string(),
            auth_chain: Some("edgeB;device-1;".to_string()),
        };
        let service_identity2 = ServiceIdentity {
            identity: "device-1/module-a".to_string(),
            auth_chain: Some("edgeB;device-1/module-a;".to_string()),
        };
        let _ = authorizer.update(Box::new(vec![service_identity, service_identity2]));
        authorizer
    }

    fn connect_activity(client_id: &str, auth_id: impl Into<AuthId>) -> Activity {
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

    fn publish_activity(client_id: &str, auth_id: impl Into<AuthId>, topic_name: &str) -> Activity {
        let publish = proto::Publish {
            packet_identifier_dup_qos: proto::PacketIdentifierDupQoS::AtMostOnce,
            retain: false,
            topic_name: topic_name.into(),
            payload: "data".into(),
        };

        let operation = Operation::new_publish(publish);
        activity(operation, client_id, auth_id)
    }

    fn subscribe_activity(
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
