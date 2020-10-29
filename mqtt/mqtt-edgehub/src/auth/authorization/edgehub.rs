use std::{any::Any, cell::RefCell, collections::HashMap, error::Error as StdError, fmt};

use serde::{Deserialize, Serialize};
use tracing::info;

use mqtt_broker::{
    auth::{Activity, Authorization, Authorizer, Operation, Publish, Subscribe},
    AuthId, BrokerReadyEvent, BrokerReadyHandle, ClientId,
};

/// `EdgeHubAuthorizer` implements authorization rules for iothub-specific primitives.
///
/// For example, it allows a client to publish (or subscribe for) twin updates, direct messages,
/// telemetry messages, etc...
///
/// For non-iothub-specific primitives it delegates the request to an inner authorizer (`PolicyAuthorizer`).
pub struct EdgeHubAuthorizer<Z> {
    iothub_allowed_topics: RefCell<HashMap<ClientId, Vec<String>>>,
    identities_cache: HashMap<ClientId, IdentityUpdate>,
    inner: Z,
    broker_ready: Option<BrokerReadyHandle>,
}

impl<Z, E> EdgeHubAuthorizer<Z>
where
    Z: Authorizer<Error = E>,
    E: StdError,
{
    pub fn new(authorizer: Z, broker_ready: BrokerReadyHandle) -> Self {
        Self::create(authorizer, Some(broker_ready))
    }

    pub fn without_ready_handle(authorizer: Z) -> Self {
        Self::create(authorizer, None)
    }

    fn create(authorizer: Z, broker_ready: Option<BrokerReadyHandle>) -> Self {
        Self {
            iothub_allowed_topics: RefCell::default(),
            identities_cache: HashMap::default(),
            inner: authorizer,
            broker_ready,
        }
    }

    #[allow(clippy::unused_self)]
    fn authorize_connect(&self, activity: &Activity) -> Result<Authorization, E> {
        match activity.client_info().auth_id() {
            // forbid anonymous clients to connect to the broker
            AuthId::Anonymous => Ok(Authorization::Forbidden(
                "Anonymous clients cannot connect to broker".to_string(),
            )),
            // allow only those clients whose auth_id and client_id identical
            AuthId::Identity(identity) => {
                if identity == activity.client_id() {
                    // delegate to inner authorizer.
                    self.inner.authorize(activity)
                } else {
                    Ok(Authorization::Forbidden(format!(
                        "client_id {} does not match registered iothub identity id {}",
                        activity.client_id(),
                        identity
                    )))
                }
            }
        }
    }

    fn authorize_publish(
        &self,
        activity: &Activity,
        publish: &Publish,
    ) -> Result<Authorization, E> {
        let topic = publish.publication().topic_name();

        if is_iothub_topic(topic) {
            // run authorization rules for publication to IoTHub topic
            self.authorize_iothub_topic(activity, topic)
        } else if is_forbidden_topic(topic) {
            // forbid any clients to access restricted topics
            Ok(Authorization::Forbidden(format!(
                "{} is forbidden topic filter",
                topic
            )))
        } else {
            // delegate to inner authorizer for to any non-iothub topics.
            self.inner.authorize(activity)
        }
    }

    fn authorize_subscribe(
        &self,
        activity: &Activity,
        subscribe: &Subscribe,
    ) -> Result<Authorization, E> {
        let topic = subscribe.topic_filter();

        if is_iothub_topic(topic) {
            // run authorization rules for subscription to IoTHub topic
            self.authorize_iothub_topic(activity, topic)
        } else if is_forbidden_topic_filter(topic) {
            // forbid any clients to access restricted topics
            Ok(Authorization::Forbidden(format!(
                "{} is forbidden topic filter",
                topic
            )))
        } else {
            // delegate to inner authorizer for to any non-iothub topics.
            self.inner.authorize(activity)
        }
    }

    fn authorize_iothub_topic(&self, activity: &Activity, topic: &str) -> Result<Authorization, E> {
        Ok(match activity.client_info().auth_id() {
            // forbid anonymous clients to subscribe to restricted topics
            AuthId::Anonymous => Authorization::Forbidden(
                "Anonymous clients do not have access to IoTHub topics".to_string(),
            ),
            // allow authenticated clients with client_id == auth_id and accessing its own IoTHub topic
            AuthId::Identity(identity) if identity == activity.client_id() => {
                if self.is_iothub_topic_allowed(activity.client_id(), topic)
                    && self.check_authorized_cache(activity.client_id(), topic)
                {
                    Authorization::Allowed
                } else {
                    // check if iothub policy is overridden by a custom policy.
                    if Authorization::Allowed == self.inner.authorize(activity)? {
                        Authorization::Allowed
                    } else {
                        Authorization::Forbidden(
                            "Client must access its own IoTHub topics only".to_string(),
                        )
                    }
                }
            }
            // forbid access otherwise
            AuthId::Identity(_) => Authorization::Forbidden(format!(
                "client_id {} must match registered iothub identity id to access IoTHub topic",
                activity.client_id()
            )),
        })
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

    fn check_authorized_cache(&self, client_id: &ClientId, topic: &str) -> bool {
        match get_on_behalf_of_id(topic) {
            Some(on_behalf_of_id) if client_id == &on_behalf_of_id => {
                self.identities_cache.contains_key(client_id)
            }
            Some(on_behalf_of_id) => self
                .identities_cache
                .get(&on_behalf_of_id)
                .and_then(IdentityUpdate::auth_chain)
                .map_or(false, |auth_chain| auth_chain.contains(client_id.as_str())),
            None => {
                // If there is no on_behalf_of_id, we are dealing with a legacy topic
                // The client_id must still be in the identities cache
                self.identities_cache.contains_key(client_id)
            }
        }
    }
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
        (Some(device_id), Some(module_id)) => Some(format!("{}/{}", device_id, module_id).into()),
        (Some(device_id), None) => Some((*device_id).into()),
        _ => None,
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

impl<Z, E> Authorizer for EdgeHubAuthorizer<Z>
where
    Z: Authorizer<Error = E>,
    E: StdError,
{
    type Error = E;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        match activity.operation() {
            Operation::Connect => self.authorize_connect(activity),
            Operation::Publish(publish) => self.authorize_publish(activity, &publish),
            Operation::Subscribe(subscribe) => self.authorize_subscribe(activity, &subscribe),
        }
    }

    fn update(&mut self, update: Box<dyn Any>) -> Result<(), Self::Error> {
        match update.downcast::<AuthorizerUpdate>() {
            Ok(update) => {
                // update identities cache
                self.identities_cache = update
                    .0
                    .into_iter()
                    .map(|id| (id.identity().into(), id))
                    .collect();

                info!("edgehub authorizer has been updated.");

                // signal that authorizer has been initialized
                if let Some(mut broker_ready) = self.broker_ready.take() {
                    broker_ready.send(BrokerReadyEvent::AuthorizerReady);
                }
            }
            Err(update) => {
                self.inner.update(update)?;
            }
        };

        Ok(())
    }
}

/// Represents updates to an `EdgeHubAuthorizer`.
#[derive(Debug, Serialize, Deserialize)]
pub struct AuthorizerUpdate(Vec<IdentityUpdate>);

/// Represents an update to an identity.
#[derive(Debug, Serialize, Deserialize)]
pub struct IdentityUpdate {
    /// Identity name.
    #[serde(rename = "Identity")]
    identity: String,

    /// Auth chain is used to authorize "on-behalf-of" operations.
    #[serde(rename = "AuthChain")]
    auth_chain: Option<String>,
}

impl IdentityUpdate {
    pub fn new(identity: String, auth_chain: Option<String>) -> Self {
        Self {
            identity,
            auth_chain,
        }
    }

    pub fn identity(&self) -> &str {
        &self.identity
    }

    pub fn auth_chain(&self) -> Option<&str> {
        self.auth_chain.as_deref()
    }
}

impl fmt::Display for IdentityUpdate {
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
    use matches::assert_matches;
    use test_case::test_case;

    use mqtt_broker::{
        auth::Authorizer,
        auth::{authorize_fn_ok, Activity, AllowAll, AuthId, Authorization, DenyAll},
    };

    use crate::auth::authorization::tests;

    use super::{AuthorizerUpdate, EdgeHubAuthorizer, IdentityUpdate};

    #[test_case(&tests::connect_activity("device-1", AuthId::Anonymous); "anonymous clients")]
    #[test_case(&tests::connect_activity("device-1", "device-2"); "different auth_id and client_id")]
    fn it_forbids_to_connect(activity: &Activity) {
        let authorizer = authorizer(AllowAll);

        let auth = authorizer.authorize(&activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/device-1/messages/events"); "device events")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/messages/events"); "edge module events")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/device-1/messages/c2d/post"); "device C2D messages")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/messages/c2d/post"); "edge module C2D messages")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/desired"); "device update desired properties")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/desired"); "edge module update desired properties")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/reported"); "device update reported properties")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/reported"); "edge module update reported properties")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/get"); "device twin request")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/get"); "edge module twin request")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/res"); "device twin response")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/inputs/route1"); "edge module access M2M inputs")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/outputs/route1"); "edge module access M2M outputs")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/messages/events"); "iothub telemetry")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/messages/events"); "iothub telemetry with moduleId")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/messages/c2d/post"); "iothub c2d messages")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/messages/c2d/post"); "iothub c2d messages with moduleId")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/twin/patch/properties/desired"); "iothub update desired properties")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/patch/properties/desired"); "iothub update desired properties with moduleId")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/twin/patch/properties/reported"); "iothub update reported properties")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/patch/properties/reported"); "iothub update reported properties with moduleId")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/twin/get"); "iothub device twin request")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/get"); "iothub module twin request")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/twin/res"); "iothub device twin response")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/res"); "iothub module twin response")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/methods/post"); "iothub device DM request")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/methods/post"); "iothub module DM request")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$iothub/clients/device-1/methods/res"); "iothub device DM response")]
    #[test_case(&tests::subscribe_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/methods/res"); "iothub module DM response")]
    fn it_allows_to_subscribe_to(activity: &Activity) {
        let authorizer = authorizer(DenyAll);

        let auth = authorizer.authorize(&activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::subscribe_activity("device-1", "device-1", "#"); "everything")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$SYS/connected"); "SYS topics")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$CUSTOM/topic"); "any special topics")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$upstream/#"); "everything with upstream prefixed")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$downstream/#"); "everything with downstream prefixed")]
    #[test_case(&tests::subscribe_activity("device-1", "device-2", "$edgehub/device-1/twin/get"); "twin request for another client")]
    #[test_case(&tests::subscribe_activity("device-1", AuthId::Anonymous, "$edgehub/device-1/twin/get"); "twin request by anonymous client")]
    fn it_forbids_to_subscribe_to(activity: &Activity) {
        let authorizer = authorizer(AllowAll);

        let auth = authorizer.authorize(&activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    #[test_case(&tests::connect_activity("device-1", "device-1"); "identical auth_id and client_id")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "topic"); "generic MQTT topic publish")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "topic"); "generic MQTT topic subscribe")]
    fn it_delegates_to_inner(activity: &Activity) {
        let inner = authorize_fn_ok(|_| Authorization::Forbidden("not allowed inner".to_string()));
        let authorizer = EdgeHubAuthorizer::without_ready_handle(inner);

        let auth = authorizer.authorize(&activity);

        // make sure error message matches inner authorizer.
        assert_matches!(auth, Ok(auth) if auth == Authorization::Forbidden("not allowed inner".to_string()));
    }

    #[test_case(&tests::publish_activity("device-1", "device-1", "$edgehub/some/topic"); "arbitrary edgehub prefixed topic")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$iothub/some/topic"); "arbitrary iothub prefixed topic")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/#"); "everything with edgehub prefixed")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$iothub/#"); "everything with iothub prefixed")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/device-2/twin/get"); "twin request for another device")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/+/twin/get"); "twin request for arbitrary device")]
    #[test_case(&tests::subscribe_activity("device-1", "device-1", "$edgehub/device-1/twin/+"); "both twin operations")]
    fn iothub_primitives_overridden_by_inner(activity: &Activity) {
        // these primitives must be denied, but overridden by AllowAll
        let authorizer = authorizer(AllowAll);

        let auth = authorizer.authorize(&activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::publish_activity("device-1", "device-1", "$edgehub/device-1/messages/events"); "device events")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/messages/events"); "edge module events")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$edgehub/device-1/messages/c2d/post"); "device C2D messages")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/messages/c2d/post"); "edge module C2D messages")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$edgehub/device-1/twin/desired"); "device update desired properties")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/desired"); "edge module update desired properties")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$edgehub/device-1/twin/reported"); "device update reported properties")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/reported"); "edge module update reported properties")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$edgehub/device-1/twin/get"); "device twin request")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/get"); "edge module twin request")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$edgehub/device-1/twin/res"); "device twin response")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/twin/res"); "edge module twin response")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/outputs/route1"); "edge module access M2M outputs")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$edgehub/device-1/module-a/inputs/route1"); "edge module access M2M inputs")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$iothub/clients/device-1/messages/events"); "iothub telemetry")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/messages/events"); "iothub telemetry with moduleId")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$iothub/clients/device-1/messages/c2d/post"); "iothub c2d messages")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/messages/c2d/post"); "iothub c2d messages with moduleId")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$iothub/clients/device-1/twin/patch/properties/desired"); "iothub update desired properties")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/patch/properties/desired"); "iothub update desired properties with moduleId")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$iothub/clients/device-1/twin/patch/properties/reported"); "iothub update reported properties")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/patch/properties/reported"); "iothub update reported properties with moduleId")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$iothub/clients/device-1/twin/get"); "iothub device twin request")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/get"); "iothub module twin request")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$iothub/clients/device-1/twin/res"); "iothub device twin response")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/twin/res"); "iothub module twin response")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$iothub/clients/device-1/methods/post"); "iothub device DM request")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/methods/post"); "iothub module DM request")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$iothub/clients/device-1/methods/res"); "iothub device DM response")]
    #[test_case(&tests::publish_activity("device-1/module-a", "device-1/module-a", "$iothub/clients/device-1/modules/module-a/methods/res"); "iothub module DM response")]
    fn it_allows_to_publish_to(activity: &Activity) {
        let authorizer = authorizer(DenyAll);

        let auth = authorizer.authorize(&activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::publish_activity("device-1", "device-1", "$downstream/some/topic"); "any downstream prefixed topics")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$upstream/some/topic"); "any upstream prefixed topics")]
    #[test_case(&tests::publish_activity("device-1", "device-2", "$edgehub/device-1/twin/get"); "twin request for another client")]
    #[test_case(&tests::publish_activity("device-1", AuthId::Anonymous, "$edgehub/device-1/twin/get"); "twin request by anonymous client")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$SYS/foo"); "any system topic")]
    #[test_case(&tests::publish_activity("device-1", "device-1", "$CUSTOM/foo"); "any special topic")]
    fn it_forbids_to_publish_to(activity: &Activity) {
        let authorizer = authorizer(AllowAll);

        let auth = authorizer.authorize(&activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    fn authorizer<Z>(inner: Z) -> EdgeHubAuthorizer<Z>
    where
        Z: Authorizer,
    {
        let mut authorizer = EdgeHubAuthorizer::without_ready_handle(inner);

        let service_identity = IdentityUpdate {
            identity: "device-1".to_string(),
            auth_chain: Some("edgeB;device-1;".to_string()),
        };
        let service_identity2 = IdentityUpdate {
            identity: "device-1/module-a".to_string(),
            auth_chain: Some("edgeB;device-1/module-a;".to_string()),
        };
        let _ = authorizer.update(Box::new(AuthorizerUpdate(vec![
            service_identity,
            service_identity2,
        ])));
        authorizer
    }
}
