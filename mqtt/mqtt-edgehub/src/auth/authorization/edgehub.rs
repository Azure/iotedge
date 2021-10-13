use std::{any::Any, collections::HashMap, error::Error as StdError, fmt};

use lazy_static::lazy_static;
use regex::Regex;
use serde::{Deserialize, Serialize};
use tracing::info;

use mqtt_broker::{
    auth::{Activity, Authorization, Authorizer, Operation},
    AuthId, BrokerReadyEvent, BrokerReadyHandle, ClientId,
};

/// `EdgeHubAuthorizer` implements authorization rules for iothub-specific primitives.
///
/// For example, it allows a client to publish (or subscribe for) twin updates, direct messages,
/// telemetry messages, etc...
///
/// For non-iothub-specific primitives it delegates the request to an inner authorizer (`PolicyAuthorizer`).
pub struct EdgeHubAuthorizer<Z> {
    identities_cache: HashMap<ClientId, IdentityUpdate>,
    inner: Z,
    broker_ready: Option<BrokerReadyHandle>,
    device_id: String,
    iothub_id: String,
}

impl<Z, E> EdgeHubAuthorizer<Z>
where
    Z: Authorizer<Error = E>,
    E: StdError,
{
    pub fn new(
        authorizer: Z,
        device_id: impl Into<String>,
        iothub_id: impl Into<String>,
        broker_ready: BrokerReadyHandle,
    ) -> Self {
        Self::create(authorizer, device_id, iothub_id, Some(broker_ready))
    }

    pub fn without_ready_handle(
        authorizer: Z,
        device_id: impl Into<String>,
        iothub_id: impl Into<String>,
    ) -> Self {
        Self::create(authorizer, device_id, iothub_id, None)
    }

    fn create(
        authorizer: Z,
        device_id: impl Into<String>,
        iothub_id: impl Into<String>,
        broker_ready: Option<BrokerReadyHandle>,
    ) -> Self {
        Self {
            identities_cache: HashMap::default(),
            inner: authorizer,
            broker_ready,
            device_id: device_id.into(),
            iothub_id: iothub_id.into(),
        }
    }

    #[allow(clippy::unused_self)]
    fn authorize_connect(&self, activity: &Activity) -> Result<Authorization, E> {
        match activity.client_info().auth_id() {
            // forbid anonymous clients to connect to the broker
            AuthId::Anonymous => Ok(Authorization::Forbidden(
                "Anonymous clients cannot connect to the broker".to_string(),
            )),
            // allow only those clients whose auth_id and client_id identical
            AuthId::Identity(identity) => {
                let actor_id = format!("{}/{}", self.iothub_id, activity.client_id());
                if *identity == actor_id {
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

    fn authorize_topic(&self, activity: &Activity, topic: &str) -> Result<Authorization, E> {
        if is_iothub_topic(topic) {
            // run authorization rules for publication to IoTHub topic
            self.authorize_iothub_topic(activity, topic)
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
            AuthId::Identity(_) => {
                if self.is_iothub_operation_authorized(topic, activity.client_id()) {
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
        })
    }

    fn is_iothub_operation_authorized(&self, topic: &str, client_id: &ClientId) -> bool {
        // actor id is either id of a leaf/edge device or on-behalf-of id (when
        // child edge acting on behalf of it's own children).
        match get_actor_id(topic) {
            Some(actor_id) if actor_id == *client_id => {
                // if actor = client, it means it is a regular leaf/edge device request.
                // check that it is in the current edgehub auth chain.
                //
                // [edgehub] <- [actor = client (leaf or child edge)]
                match self.identities_cache.get(&actor_id) {
                    Some(identity) => identity
                        .auth_chain()
                        .map_or(false, |auth_chain| auth_chain.contains(&self.device_id)),
                    None => false,
                }
            }
            Some(actor_id) => {
                // if actor != client, it means it is an on-behalf-of request.
                // check that:
                // - actor_id is in the auth chain for client_id (that client
                //   making a request can actually do it on behalf of the actor)
                // - check that actor_id is in the auth chain for current edgehub.
                // - check that client_id is in the auth chain for current edgehub.
                //
                // [edgehub] <- [client (child edgehub)] <- [actor (grandchild)]

                let parent_ok = match self.identities_cache.get(&actor_id) {
                    Some(identity) => identity.auth_chain().map_or(false, |auth_chain| {
                        auth_chain.contains(&client_id.as_str().replace("/$edgeHub", ""))
                    }),
                    None => false,
                };

                let actor_ok = match self.identities_cache.get(&actor_id) {
                    Some(identity) => identity
                        .auth_chain()
                        .map_or(false, |auth_chain| auth_chain.contains(&self.device_id)),
                    None => false,
                };

                let client_ok = match self.identities_cache.get(client_id) {
                    Some(identity) => identity
                        .auth_chain()
                        .map_or(false, |auth_chain| auth_chain.contains(&self.device_id)),
                    None => false,
                };

                parent_ok && actor_ok && client_ok
            }
            // If there is no actor_id, we are dealing with a legacy topic/unknown format.
            // Delegated to inner authorizer.
            None => false,
        }
    }
}

fn get_actor_id(topic: &str) -> Option<ClientId> {
    lazy_static! {
        static ref TOPIC_PATTERN: Regex = Regex::new(
            // this regex tries to capture all possible iothub/edgehub specific topic format.
            // we need this
            // - to validate that this is correct iothub/edgehub topic.
            // - to extract device_id and module_id.
            //
            // format! is for ease of reading only.
            &format!(r"^(\$edgehub|\$iothub)/(?P<device_id>[^/\+\#]+)(/(?P<module_id>[^/\+\#]+))?/({}|{}|{}|{}|{}|{}|{}|{}|{})",
                "messages/events",
                "messages/c2d/post",
                "twin/desired",
                "twin/reported",
                "twin/get",
                "twin/res",
                "methods/post",
                "methods/res",
                "\\+/inputs")
        ).expect("failed to create new Regex from pattern");
    }

    match TOPIC_PATTERN.captures(topic) {
        Some(captures) => match (captures.name("device_id"), captures.name("module_id")) {
            (Some(device_id), None) => Some(device_id.as_str().into()),
            (Some(device_id), Some(module_id)) => {
                Some(format!("{}/{}", device_id.as_str(), module_id.as_str()).into())
            }
            (_, _) => None,
        },
        None => None,
    }
}

const IOTHUB_TOPICS_PREFIX: [&str; 2] = ["$edgehub/", "$iothub/"];

fn is_iothub_topic(topic: &str) -> bool {
    IOTHUB_TOPICS_PREFIX
        .iter()
        .any(|prefix| topic.starts_with(prefix))
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
            Operation::Publish(publish) => {
                self.authorize_topic(activity, publish.publication().topic_name())
            }
            Operation::Subscribe(subscribe) => {
                self.authorize_topic(activity, subscribe.topic_filter())
            }
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

    #[test_case(&tests::connect_activity("leaf-1", AuthId::Anonymous); "anonymous clients")]
    #[test_case(&tests::connect_activity("leaf-1", "leaf-2"); "different auth_id and client_id")]
    fn it_forbids_to_connect(activity: &Activity) {
        let authorizer = authorizer(AllowAll, vec![]);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/messages/events"); "device events")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/messages/c2d/post"); "device C2D messages")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/twin/desired"); "device update desired properties")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/twin/reported"); "device update reported properties")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/twin/get"); "device twin request")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/twin/res"); "device twin response")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/methods/post"); "device DM request")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/methods/res"); "device DM response")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/leaf-1/messages/events"); "iothub telemetry")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/leaf-1/messages/c2d/post"); "iothub c2d messages")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/leaf-1/twin/desired"); "iothub update desired properties")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/leaf-1/twin/reported"); "iothub update reported properties")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/leaf-1/twin/get"); "iothub device twin request")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/leaf-1/twin/res"); "iothub device twin response")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/leaf-1/methods/post"); "iothub device DM request")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/leaf-1/methods/res"); "iothub device DM response")]
    fn it_allows_to_subscribe_for_leaf(activity: &Activity) {
        let identities = vec![IdentityUpdate {
            identity: "leaf-1".to_string(),
            auth_chain: Some("leaf-1;this_edge".to_string()),
        }];
        let authorizer = authorizer(DenyAll, identities);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/messages/events"); "device events")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/messages/c2d/post"); "device C2D messages")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/twin/desired"); "device update desired properties")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/twin/reported"); "device update reported properties")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/twin/get"); "device twin request")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/twin/res"); "device twin response")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/methods/post"); "device DM request")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/methods/res"); "device DM response")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/+/inputs/route1"); "edge module access M2M inputs")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/messages/events"); "iothub telemetry")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/messages/c2d/post"); "iothub c2d messages")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/twin/desired"); "iothub update desired properties")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/twin/reported"); "iothub update reported properties")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/twin/get"); "iothub device twin request")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/twin/res"); "iothub device twin response")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/methods/post"); "iothub device DM request")]
    #[test_case(&tests::subscribe_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/methods/res"); "iothub device DM response")]
    fn it_allows_to_subscribe_for_module(activity: &Activity) {
        let identities = vec![IdentityUpdate {
            identity: "this_edge/module-a".to_string(),
            auth_chain: Some("this_edge/module-a;this_edge".to_string()),
        }];
        let authorizer = authorizer(DenyAll, identities);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/messages/events"); "edge module events")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/messages/c2d/post"); "edge module C2D messages")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/twin/desired"); "edge module update desired properties")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/twin/reported"); "edge module update reported properties")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/twin/get"); "edge module twin request")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/methods/post"); "module DM request")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/methods/res"); "module DM response")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/messages/events"); "iothub telemetry with moduleId")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/messages/c2d/post"); "iothub c2d messages with moduleId")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/twin/desired"); "iothub update desired properties with moduleId")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/twin/reported"); "iothub update reported properties with moduleId")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/twin/get"); "iothub module twin request")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/twin/res"); "iothub module twin response")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/methods/post"); "iothub module DM request")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/methods/res"); "iothub module DM response")]
    fn it_allows_to_subscribe_for_on_behalf_of_module(activity: &Activity) {
        let identities = vec![
            // child edgehub
            IdentityUpdate {
                identity: "edge-1/$edgeHub".to_string(),
                auth_chain: Some("edge-1/$edgeHub;this_edge".to_string()),
            },
            // grandchild module
            IdentityUpdate {
                identity: "edge-1/module-a".to_string(),
                auth_chain: Some("edge-1/module-a;edge-1;this_edge".to_string()),
            },
        ];
        let authorizer = authorizer(DenyAll, identities);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/messages/events"); "edge module events")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/messages/c2d/post"); "edge module C2D messages")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/twin/desired"); "edge module update desired properties")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/twin/reported"); "edge module update reported properties")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/twin/get"); "edge module twin request")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/methods/post"); "module DM request")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/methods/res"); "module DM response")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/messages/events"); "iothub telemetry with moduleId")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/messages/c2d/post"); "iothub c2d messages with moduleId")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/twin/desired"); "iothub update desired properties with moduleId")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/twin/reported"); "iothub update reported properties with moduleId")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/twin/get"); "iothub module twin request")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/twin/res"); "iothub module twin response")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/methods/post"); "iothub module DM request")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/methods/res"); "iothub module DM response")]
    fn it_allows_to_subscribe_for_on_behalf_of_leaf(activity: &Activity) {
        let identities = vec![
            // child edgehub
            IdentityUpdate {
                identity: "edge-1/$edgeHub".to_string(),
                auth_chain: Some("edge-1/$edgeHub;this_edge".to_string()),
            },
            // grandchild leaf
            IdentityUpdate {
                identity: "leaf-2".to_string(),
                auth_chain: Some("leaf-2;edge-1;this_edge".to_string()),
            },
        ];
        let authorizer = authorizer(DenyAll, identities);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::connect_activity("edge-1/$edgeHub", "myhub.azure-devices.net/edge-1/$edgeHub"); "module identical auth_id and client_id")]
    #[test_case(&tests::connect_activity("edge-1", "myhub.azure-devices.net/edge-1"); "leaf identical auth_id and client_id")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "topic"); "module generic MQTT topic publish")]
    #[test_case(&tests::publish_activity("edge-1", "edge-1", "topic"); "leaf generic MQTT topic publish")]
    #[test_case(&tests::subscribe_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "topic"); "module generic MQTT topic subscribe")]
    #[test_case(&tests::subscribe_activity("edge-1", "edge-1", "topic"); "leaf generic MQTT topic subscribe")]
    fn it_delegates_to_inner(activity: &Activity) {
        let inner = authorize_fn_ok(|_| Authorization::Forbidden("not allowed inner".to_string()));
        let authorizer =
            EdgeHubAuthorizer::without_ready_handle(inner, "edgehub_id", "myhub.azure-devices.net");

        let auth = authorizer.authorize(activity);

        // make sure error message matches inner authorizer.
        assert_matches!(auth, Ok(auth) if auth == Authorization::Forbidden("not allowed inner".to_string()));
    }

    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/some/topic"); "arbitrary edgehub prefixed topic")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$iothub/some/topic"); "arbitrary iothub prefixed topic")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/#"); "everything with edgehub prefixed")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/#"); "everything with iothub prefixed")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-2/twin/get"); "twin request for another device")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/+/twin/get"); "twin request for arbitrary device")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/edge-1/twin/+"); "both twin operations")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/leaf-2/twin/get"); "iothub twin request for another device")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/+/twin/get"); "iothub twin request for arbitrary device")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$iothub/edge-1/twin/+"); "iothub both twin operations")]
    fn iothub_primitives_overridden_by_inner(activity: &Activity) {
        // these primitives must be denied, but overridden by AllowAll
        let authorizer = authorizer(AllowAll, vec![]);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/messages/events"); "device events")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/messages/c2d/post"); "device C2D messages")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/twin/desired"); "device update desired properties")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/twin/reported"); "device update reported properties")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/twin/get"); "device twin request")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/twin/res"); "device twin response")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/methods/post"); "device DM request")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/methods/res"); "device DM response")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$iothub/leaf-1/messages/events"); "iothub telemetry")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$iothub/leaf-1/messages/c2d/post"); "iothub c2d messages")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$iothub/leaf-1/twin/desired"); "iothub update desired properties")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$iothub/leaf-1/twin/reported"); "iothub update reported properties")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$iothub/leaf-1/twin/get"); "iothub device twin request")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$iothub/leaf-1/twin/res"); "iothub device twin response")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$iothub/leaf-1/methods/post"); "iothub device DM request")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$iothub/leaf-1/methods/res"); "iothub device DM response")]
    fn it_allows_to_publish_for_leaf(activity: &Activity) {
        let identities = vec![IdentityUpdate {
            identity: "leaf-1".to_string(),
            auth_chain: Some("leaf-1;this_edge".to_string()),
        }];
        let authorizer = authorizer(DenyAll, identities);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/messages/events"); "device events")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/messages/c2d/post"); "device C2D messages")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/twin/desired"); "device update desired properties")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/twin/reported"); "device update reported properties")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/twin/get"); "device twin request")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/twin/res"); "device twin response")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/methods/post"); "device DM request")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$edgehub/this_edge/module-a/methods/res"); "device DM response")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/messages/events"); "iothub telemetry")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/messages/c2d/post"); "iothub c2d messages")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/twin/desired"); "iothub update desired properties")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/twin/reported"); "iothub update reported properties")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/twin/get"); "iothub device twin request")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/twin/res"); "iothub device twin response")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/methods/post"); "iothub device DM request")]
    #[test_case(&tests::publish_activity("this_edge/module-a", "this_edge/module-a", "$iothub/this_edge/module-a/methods/res"); "iothub device DM response")]
    fn it_allows_to_publish_for_module(activity: &Activity) {
        let identities = vec![IdentityUpdate {
            identity: "this_edge/module-a".to_string(),
            auth_chain: Some("this_edge/module-a;this_edge".to_string()),
        }];
        let authorizer = authorizer(DenyAll, identities);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/messages/events"); "edge module events")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/messages/c2d/post"); "edge module C2D messages")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/twin/desired"); "edge module update desired properties")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/twin/reported"); "edge module update reported properties")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/twin/get"); "edge module twin request")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/twin/res"); "edge module twin response")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/methods/post"); "module DM request")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/leaf-2/methods/res"); "module DM response")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/messages/events"); "iothub telemetry with moduleId")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/messages/c2d/post"); "iothub c2d messages with moduleId")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/twin/desired"); "iothub update desired properties with moduleId")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/twin/reported"); "iothub update reported properties with moduleId")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/twin/get"); "iothub module twin request")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/twin/res"); "iothub module twin response")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/methods/post"); "iothub module DM request")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/leaf-2/methods/res"); "iothub module DM response")]
    fn it_allows_to_publish_for_on_behalf_of_leaf(activity: &Activity) {
        let identities = vec![
            // child edgehub
            IdentityUpdate {
                identity: "edge-1/$edgeHub".to_string(),
                auth_chain: Some("edge-1/$edgeHub;this_edge".to_string()),
            },
            // grandchild leaf
            IdentityUpdate {
                identity: "leaf-2".to_string(),
                auth_chain: Some("leaf-2;edge-1;this_edge".to_string()),
            },
        ];
        let authorizer = authorizer(DenyAll, identities);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/messages/events"); "edge module events")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/messages/c2d/post"); "edge module C2D messages")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/twin/desired"); "edge module update desired properties")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/twin/reported"); "edge module update reported properties")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/twin/get"); "edge module twin request")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/twin/res"); "edge module twin response")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/methods/post"); "module DM request")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$edgehub/edge-1/module-a/methods/res"); "module DM response")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/messages/events"); "iothub telemetry with moduleId")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/messages/c2d/post"); "iothub c2d messages with moduleId")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/twin/desired"); "iothub update desired properties with moduleId")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/twin/reported"); "iothub update reported properties with moduleId")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/twin/get"); "iothub module twin request")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/twin/res"); "iothub module twin response")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/methods/post"); "iothub module DM request")]
    #[test_case(&tests::publish_activity("edge-1/$edgeHub", "edge-1/$edgeHub", "$iothub/edge-1/module-a/methods/res"); "iothub module DM response")]
    fn it_allows_to_publish_for_on_behalf_of_module(activity: &Activity) {
        let identities = vec![
            // child edgehub
            IdentityUpdate {
                identity: "edge-1/$edgeHub".to_string(),
                auth_chain: Some("edge-1/$edgeHub;this_edge".to_string()),
            },
            // grandchild module
            IdentityUpdate {
                identity: "edge-1/module-a".to_string(),
                auth_chain: Some("edge-1/module-a;edge-1;this_edge".to_string()),
            },
        ];
        let authorizer = authorizer(DenyAll, identities);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::subscribe_activity("edge-1/module-a", "edge-1/module-a", "$edgehub/edge-1/module-a/messages/events"); "module events sub")]
    #[test_case(&tests::publish_activity("edge-1/module-a", "edge-1/module-a", "$edgehub/edge-1/module-a/messages/events"); "module events pub")]
    #[test_case(&tests::subscribe_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/messages/events"); "leaf events sub")]
    #[test_case(&tests::publish_activity("leaf-1", "leaf-1", "$edgehub/leaf-1/messages/events"); "leaf events pub")]
    fn it_forbids_operations_for_not_in_scope_identities(activity: &Activity) {
        let identities = vec![
            // leaf
            IdentityUpdate {
                identity: "another-leaf".to_string(),
                auth_chain: Some("another-leaf;this_edge".to_string()),
            },
            // module
            IdentityUpdate {
                identity: "this_edge/another-module".to_string(),
                auth_chain: Some("edge-1/another-module;this_edge".to_string()),
            },
        ];
        let authorizer = authorizer(DenyAll, identities);

        let auth = authorizer.authorize(activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    fn authorizer<Z>(inner: Z, identities: Vec<IdentityUpdate>) -> EdgeHubAuthorizer<Z>
    where
        Z: Authorizer,
    {
        let mut authorizer =
            EdgeHubAuthorizer::without_ready_handle(inner, "this_edge", "myhub.azure-devices.net");

        let _result = authorizer.update(Box::new(AuthorizerUpdate(identities)));
        authorizer
    }
}
