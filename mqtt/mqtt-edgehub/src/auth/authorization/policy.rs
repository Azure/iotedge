use thiserror::Error;
use tracing::debug;

use mqtt_broker::{
    auth::{Activity, Authorization, Authorizer, Operation},
    AuthId,
};
use mqtt_policy::{MqttSubstituter, MqttTopicFilterMatcher, MqttValidator};
use policy::{Decision, Policy, PolicyBuilder, Request};

/// `PolicyAuthorizer` uses policy engine to evaluate the activity.
///
/// Policy definition comes from the Edge Hub twin. Before use, `PolicyAuthorizer` must be
/// initialized with policy definition by calling `update` method.
///
/// This is the last authorizer in the chain of edgehub-specific authorizers (see `EdgeHubAuthorizer`).
/// It's purpose is to evaluate customer rules for generic MQTT topics.
pub struct PolicyAuthorizer {
    policy: Option<Policy<MqttTopicFilterMatcher, MqttSubstituter>>,
    device_id: String,
}

impl PolicyAuthorizer {
    pub fn new(device_id: impl Into<String>) -> Self {
        Self {
            policy: None,
            device_id: device_id.into(),
        }
    }
}

impl Authorizer for PolicyAuthorizer {
    type Error = Error;

    fn authorize(&self, activity: &Activity) -> Result<Authorization, Self::Error> {
        let request = Request::with_context(
            get_identity(&activity),
            get_operation(&activity),
            get_resource(&activity),
            activity.clone(),
        )
        .map_err(Error::Authorization)?;

        debug!("authorizing request: {:?}", request);

        match &self.policy {
            Some(policy) => Ok(
                match policy.evaluate(&request).map_err(Error::Authorization)? {
                    Decision::Allowed => Authorization::Allowed,
                    Decision::Denied => Authorization::Forbidden("denied by policy".into()),
                },
            ),
            None => Err(Error::PolicyNotReady),
        }
    }

    fn update(&mut self, update: Box<dyn std::any::Any>) -> Result<(), Self::Error> {
        if let Some(policy_update) = update.downcast_ref::<PolicyUpdate>() {
            self.policy = Some(build_policy(&policy_update.definition, &self.device_id)?);
            debug!("policy engine has been updated.");
        }
        Ok(())
    }
}

pub struct PolicyUpdate {
    definition: String,
}

#[derive(Debug, Error)]
pub enum Error {
    #[error("An error occurred authorizing the request: {0}")]
    Authorization(#[source] policy::Error),

    #[error("An error occurred building policy from the definition: {0}")]
    BuildPolicy(#[source] policy::Error),

    #[error("An error occurred authorizing the request: Policy is not ready.")]
    PolicyNotReady,
}

fn build_policy(
    definition: impl Into<String>,
    device_id: impl Into<String>,
) -> Result<Policy<MqttTopicFilterMatcher, MqttSubstituter>, Error> {
    PolicyBuilder::from_json(definition)
        .with_validator(MqttValidator)
        .with_matcher(MqttTopicFilterMatcher)
        .with_substituter(MqttSubstituter::new(device_id))
        .with_default_decision(Decision::Denied)
        .build()
        .map_err(Error::BuildPolicy)
}

fn get_identity(activity: &Activity) -> &str {
    match activity.client_info().auth_id() {
        AuthId::Anonymous => "*", //TODO: think about this one.
        AuthId::Identity(identity) => identity.as_str(),
    }
}

fn get_operation(activity: &Activity) -> &str {
    match activity.operation() {
        Operation::Connect(_) => "mqtt:connect",
        Operation::Publish(_) => "mqtt:publish",
        Operation::Subscribe(_) => "mqtt:subscribe",
    }
}

fn get_resource(activity: &Activity) -> &str {
    match activity.operation() {
        // this is intentional. mqtt:connect should have empty resource.
        Operation::Connect(_) => "",
        Operation::Publish(publish) => publish.publication().topic_name(),
        Operation::Subscribe(subscribe) => subscribe.topic_filter(),
    }
}

#[cfg(test)]
mod tests {
    use matches::assert_matches;
    use test_case::test_case;

    use mqtt_broker::auth::{Activity, Authorization, Authorizer};

    use crate::auth::authorization::tests;

    use super::{Error, PolicyAuthorizer, PolicyUpdate};

    #[test]
    fn error_on_uninitialized_policy() {
        let authorizer = PolicyAuthorizer::new("test");

        let activity = tests::publish_activity("device-1", "device-1", "topic");

        let auth = authorizer.authorize(&activity);

        assert_matches!(auth, Err(Error::PolicyNotReady));
    }

    #[test_case(&tests::connect_activity("monitor_a", "monitor_a"); "connect rule 1")]
    #[test_case(&tests::publish_activity("monitor_a", "monitor_a", "topic/a"); "publish rule 1")]
    #[test_case(&tests::publish_activity("monitor_b", "monitor_b", "events/monitor_b/alerts"); "publish rule 2")]
    #[test_case(&tests::subscribe_activity("monitor_a", "monitor_a", "topic/a"); "subscribe rule 1")]
    #[test_case(&tests::subscribe_activity("monitor_b", "monitor_b", "events/monitor_b/#"); "subscribe rule 2")]
    fn it_allows_activity(activity: &Activity) {
        let authorizer = authorizer();

        let auth = authorizer.authorize(&activity);

        assert_matches!(auth, Ok(Authorization::Allowed));
    }

    #[test_case(&tests::connect_activity("some_identity", "some_identity"); "connect denied 1")]
    #[test_case(&tests::publish_activity("some_identity", "some_identity", "wrong/topic"); "publish identity denied")]
    #[test_case(&tests::publish_activity("monitor_a", "monitor_a", "wrong/topic"); "publish topic denied")]
    #[test_case(&tests::subscribe_activity("some_identity", "some_identity", "topic/a"); "subscribe identity denied")]
    #[test_case(&tests::subscribe_activity("monitor_b", "monitor_b", "denied/monitor_b/#"); "subscribe topic denied")]
    fn it_forbids_activity(activity: &Activity) {
        let authorizer = authorizer();

        let auth = authorizer.authorize(&activity);

        assert_matches!(auth, Ok(Authorization::Forbidden(_)));
    }

    fn authorizer() -> PolicyAuthorizer {
        let mut authorizer = PolicyAuthorizer::new("test_device_id");

        let definition = r###"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "monitor_a"
                    ],
                    "operations": [
                        "mqtt:connect",
                        "mqtt:publish",
                        "mqtt:subscribe"
                    ],
                    "resources": [
                        "topic/a"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "monitor_b"
                    ],
                    "operations": [
                        "mqtt:subscribe",
                        "mqtt:publish"
                    ],
                    "resources": [
                        "events/#"
                    ]
                },
                {
                    "effect": "deny",
                    "identities": [
                        "{{iot:identity}}"
                    ],
                    "operations": [
                        "mqtt:connect",
                        "mqtt:subscribe",
                        "mqtt:publish"
                    ],
                    "resources": [
                        "#"
                    ]
                }
            ]
        }"###
            .into();

        authorizer
            .update(Box::new(PolicyUpdate { definition }))
            .expect("invalid policy definition");
        authorizer
    }
}
