use thiserror::Error;
use tracing::{debug, warn};

use mqtt_broker::{
    auth::{Activity, Authorization, Authorizer, Operation},
    AuthId,
};
use mqtt_policy::{MqttSubstituter, MqttTopicFilterMatcher, MqttValidator};
use policy::{Decision, Policy, PolicyBuilder, Request};

/// `PolicyAuthorizer` uses policy engine to evaluate the activity.
///
/// Policy definition comes from the EdgeHub twin. Before use, `PolicyAuthorizer` must be
/// initialized with policy definition by calling `update` method.
///
/// This is the last authorizer in the chain of edgehub-specific authorizers (see `EdgeHubAuthorizer`).
/// It's purpose is to evaluate customer rules for generic MQTT topics.
pub struct PolicyAuthorizer {
    policy: Option<Policy<MqttTopicFilterMatcher, MqttSubstituter>>,
    device_id: String,
}

impl PolicyAuthorizer {
    #[allow(dead_code)]
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
            get_identity(&activity).to_string(), //TODO: see if we can avoid cloning here.
            get_operation(&activity).to_string(),
            get_resource(&activity).to_string(),
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
        } else {
            warn!("received update does not match PolicyUpdate");
        }
        Ok(())
    }
}

pub struct PolicyUpdate {
    definition: String,
}

#[derive(Debug, Error)]
pub enum Error {
    #[error("An error occurred authorizing the request.")]
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
