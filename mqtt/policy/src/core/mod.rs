use std::{
    cmp::Ordering,
    collections::{btree_map::Entry, BTreeMap},
};

use crate::errors::Result;
use crate::{substituter::Substituter, Error, ResourceMatcher};

mod builder;
pub use builder::{PolicyBuilder, PolicyDefinition, Statement};

/// Policy engine. Represents a read-only set of rules and can
/// evaluate `Request` based on those rules.
///
/// Policy engine consists of two sets:
/// - static rules
/// - variable rules - any rule that contains variables ("{{..}}").
/// Static rules are organized in a data structure with fast querying time.
/// Variable rules are evaluated on every request.
#[derive(Debug)]
pub struct Policy<R, S> {
    default_decision: Decision,
    resource_matcher: R,
    substituter: S,
    static_rules: BTreeMap<String, Operations>,
    variable_rules: BTreeMap<String, Operations>,
}

impl<R, S, RC> Policy<R, S>
where
    R: ResourceMatcher<Context = RC>,
    S: Substituter<Context = RC>,
{
    /// Evaluates the provided `&Request` and produces the `Decision`.
    ///
    /// If no rules match the `&Request` - the default `Decision` is returned.
    pub fn evaluate(&self, request: &Request<RC>) -> Result<Decision> {
        match self.eval_static_rules(request) {
            // static rules not defined. Need to check variable rules.
            Ok(EffectOrd {
                effect: Effect::Undefined,
                ..
            }) => match self.eval_variable_rules(request) {
                // variable rules undefined as well. Return default decision.
                Ok(EffectOrd {
                    effect: Effect::Undefined,
                    ..
                }) => Ok(self.default_decision),
                // variable rules defined. Return the decision.
                Ok(effect) => Ok(effect.into()),
                Err(e) => Err(e),
            },
            // static rules are defined. Evaluate variable rules and compare priority.
            Ok(static_effect) => {
                match self.eval_variable_rules(request) {
                    // variable rules undefined. Proceed with static rule decision.
                    Ok(EffectOrd {
                        effect: Effect::Undefined,
                        ..
                    }) => Ok(static_effect.into()),
                    // variable rules defined. Compare priority and return.
                    Ok(variable_effect) => {
                        // compare order.
                        Ok(if variable_effect > static_effect {
                            static_effect
                        } else {
                            variable_effect
                        }
                        .into())
                    }
                    Err(e) => Err(e),
                }
            }

            Err(e) => Err(e),
        }
    }

    fn eval_static_rules(&self, request: &Request<RC>) -> Result<EffectOrd> {
        // lookup an identity
        match self.static_rules.get(&request.identity) {
            // identity exists. Look up operations.
            Some(operations) => match operations.0.get(&request.operation) {
                // operation exists.
                Some(resources) => {
                    // Iterate over and match resources.
                    for (resource, effect) in &resources.0 {
                        if self
                            .resource_matcher
                            .do_match(request, &request.resource, &resource)
                        {
                            return Ok(*effect);
                        }
                    }
                    Ok(EffectOrd::undefined())
                }
                None => Ok(EffectOrd::undefined()),
            },
            None => Ok(EffectOrd::undefined()),
        }
    }

    fn eval_variable_rules(&self, request: &Request<RC>) -> Result<EffectOrd> {
        for (identity, operations) in &self.variable_rules {
            // process identity variables.
            let identity = self.substituter.visit_identity(identity, request)?;
            // check if it does match after processing variables.
            if identity == request.identity {
                // lookup operation.
                return match operations.0.get(&request.operation) {
                    // operation exists.
                    Some(resources) => {
                        // Iterate over and match resources.
                        for (resource, effect) in &resources.0 {
                            let resource = self.substituter.visit_resource(resource, request)?;
                            if self
                                .resource_matcher
                                .do_match(request, &request.resource, &resource)
                            {
                                return Ok(*effect);
                            }
                        }
                        Ok(EffectOrd::undefined())
                    }
                    None => Ok(EffectOrd::undefined()),
                };
            }
        }
        Ok(EffectOrd::undefined())
    }
}

#[derive(Debug, Clone)]
struct Identities(BTreeMap<String, Operations>);

impl Identities {
    pub fn new() -> Self {
        Identities(BTreeMap::new())
    }

    pub fn merge(&mut self, collection: Identities) {
        for (key, value) in collection.0 {
            self.insert(&key, value);
        }
    }

    fn insert(&mut self, operation: &str, resources: Operations) {
        if !resources.0.is_empty() {
            let entry = self.0.entry(operation.to_string());
            match entry {
                Entry::Vacant(item) => {
                    item.insert(resources);
                }
                Entry::Occupied(mut item) => item.get_mut().merge(resources),
            }
        }
    }
}

#[derive(Debug, Clone)]
struct Operations(BTreeMap<String, Resources>);

impl Operations {
    pub fn new() -> Self {
        Operations(BTreeMap::new())
    }

    pub fn merge(&mut self, collection: Operations) {
        for (key, value) in collection.0 {
            self.insert(&key, value);
        }
    }

    fn insert(&mut self, operation: &str, resources: Resources) {
        if !resources.0.is_empty() {
            let entry = self.0.entry(operation.to_string());
            match entry {
                Entry::Vacant(item) => {
                    item.insert(resources);
                }
                Entry::Occupied(mut item) => item.get_mut().merge(resources),
            }
        }
    }
}

impl From<BTreeMap<String, Resources>> for Operations {
    fn from(map: BTreeMap<String, Resources>) -> Self {
        Operations(map)
    }
}

#[derive(Debug, Clone)]
struct Resources(BTreeMap<String, EffectOrd>);

impl Resources {
    pub fn new() -> Self {
        Resources(BTreeMap::new())
    }

    pub fn merge(&mut self, collection: Resources) {
        for (key, value) in collection.0 {
            self.insert(&key, value);
        }
    }

    fn insert(&mut self, resource: &str, effect: EffectOrd) {
        let entry = self.0.entry(resource.to_string());
        match entry {
            Entry::Vacant(item) => {
                item.insert(effect);
            }
            Entry::Occupied(mut item) => item.get_mut().merge(effect),
        }
    }
}

impl From<BTreeMap<String, EffectOrd>> for Resources {
    fn from(map: BTreeMap<String, EffectOrd>) -> Self {
        Resources(map)
    }
}

/// Represents a request that needs to be `evaluate`d by `Policy` engine.
#[derive(Debug)]
pub struct Request<RC> {
    identity: String,
    operation: String,
    resource: String,

    /// Optional request context that can be used for request processing.
    context: Option<RC>,
}

impl<RC> Request<RC> {
    /// Creates a new `Request`. Returns an error if either identity or operation is an empty string.
    pub fn new(
        identity: impl Into<String>,
        operation: impl Into<String>,
        resource: impl Into<String>,
    ) -> Result<Self> {
        Self::create(identity, operation, resource, None)
    }

    pub fn with_context(
        identity: impl Into<String>,
        operation: impl Into<String>,
        resource: impl Into<String>,
        context: RC,
    ) -> Result<Self> {
        Self::create(identity, operation, resource, Some(context))
    }

    fn create(
        identity: impl Into<String>,
        operation: impl Into<String>,
        resource: impl Into<String>,
        context: Option<RC>,
    ) -> Result<Self> {
        let (identity, operation, resource) = (identity.into(), operation.into(), resource.into());

        if identity.is_empty() {
            return Err(Error::BadRequest("Identity must be specified".into()));
        }

        if operation.is_empty() {
            return Err(Error::BadRequest("Operation must be specified".into()));
        }

        Ok(Self {
            identity,
            operation,
            resource,
            context,
        })
    }

    pub fn context(&self) -> Option<&RC> {
        self.context.as_ref()
    }
}

/// Represents a decision on the `Request` to the `Policy` engine.
#[derive(Debug, Copy, Clone, PartialEq)]
pub enum Decision {
    Allowed,
    Denied,
}

impl From<Effect> for Decision {
    fn from(effect: Effect) -> Self {
        match effect {
            Effect::Allow => Decision::Allowed,
            Effect::Deny => Decision::Denied,
            Effect::Undefined => Decision::Denied,
        }
    }
}

#[derive(Debug, Copy, Clone, PartialOrd, PartialEq)]
enum Effect {
    Allow,
    Deny,
    Undefined,
}

#[derive(Debug, Copy, Clone, PartialEq)]
struct EffectOrd {
    order: usize,
    effect: Effect,
}

impl EffectOrd {
    pub fn new(effect: Effect, order: usize) -> Self {
        Self { order, effect }
    }

    pub fn undefined() -> Self {
        Self {
            order: 0,
            effect: Effect::Undefined,
        }
    }

    /// Merges two `EffectOrd` by replacing with the one with higher priority.
    ///
    /// Lower the order value => higher the effect priority.
    pub fn merge(&mut self, item: EffectOrd) {
        if self.order > item.order {
            *self = item;
        }
    }
}

impl PartialOrd for EffectOrd {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.order.cmp(&other.order))
    }
}

impl From<EffectOrd> for Decision {
    fn from(effect: EffectOrd) -> Self {
        match effect.effect {
            Effect::Allow => Decision::Allowed,
            Effect::Deny => Decision::Denied,
            Effect::Undefined => Decision::Denied,
        }
    }
}

impl From<&Statement> for EffectOrd {
    fn from(statement: &Statement) -> Self {
        match statement.effect() {
            builder::Effect::Allow => EffectOrd::new(Effect::Allow, statement.order()),
            builder::Effect::Deny => EffectOrd::new(Effect::Deny, statement.order()),
        }
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    use crate::{DefaultResourceMatcher, DefaultSubstituter};
    use matches::assert_matches;

    /// Helper method to build a policy.
    /// Used in both policy and builder tests.
    pub(crate) fn build_policy(json: &str) -> Policy<DefaultResourceMatcher, DefaultSubstituter> {
        PolicyBuilder::from_json(json)
            .with_default_decision(Decision::Denied)
            .build()
            .expect("Unable to build policy from json.")
    }

    #[test]
    fn evaluate_static_rules() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "deny",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "resource_1"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "actor_b"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "resource_1"
                    ]
                }
            ]
        }"#;

        let policy = build_policy(json);

        let request = Request::new("actor_a", "write", "resource_1").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Denied));

        let request = Request::new("actor_b", "read", "resource_1").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));
    }

    #[test]
    fn evaluate_undefined_rules_expected_default_action() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "contoso.azure-devices.net/some_device"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "resource_1"
                    ]
                }
            ]
        }"#;

        // assert default allow
        let request = Request::new("other_actor", "write", "resource_1").unwrap();

        let allow_default_policy = PolicyBuilder::from_json(json)
            .with_default_decision(Decision::Allowed)
            .build()
            .expect("Unable to build policy from json.");

        assert_matches!(
            allow_default_policy.evaluate(&request),
            Ok(Decision::Allowed)
        );

        // assert default deny
        let deny_default_policy = PolicyBuilder::from_json(json)
            .with_default_decision(Decision::Denied)
            .build()
            .expect("Unable to build policy from json.");

        assert_matches!(deny_default_policy.evaluate(&request), Ok(Decision::Denied));
    }

    #[test]
    fn evaluate_static_variable_rule_conflict_first_rule_wins() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "resource_1"
                    ]
                },
                {
                    "effect": "deny",
                    "identities": [
                        "{{test}}"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "resource_1"
                    ]
                },               
                {
                    "effect": "allow",
                    "identities": [
                        "{{test}}"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "resource_group"
                    ]
                },
                {
                    "effect": "deny",
                    "identities": [
                        "actor_b"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "resource_group"
                    ]
                }
            ]
        }"#;

        let policy = PolicyBuilder::from_json(json)
            .with_default_decision(Decision::Denied)
            .with_substituter(TestSubstituter)
            .build()
            .expect("Unable to build policy from json.");

        // assert static rule wins
        let request = Request::new("actor_a", "write", "resource_1").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));

        // assert variable rule wins
        let request = Request::new("actor_b", "read", "resource_group").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));
    }

    #[test]
    fn evaluate_rule_no_resource() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "connect"
                    ]
                }
            ]
        }"#;

        let policy = build_policy(json);

        let request = Request::new("actor_a", "connect", "").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));
    }

    #[test]
    fn evaluate_variable_rule_no_resource() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "deny",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "connect"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "{{test}}"
                    ],
                    "operations": [
                        "connect"
                    ]
                }
            ]
        }"#;

        let policy = PolicyBuilder::from_json(json)
            .with_default_decision(Decision::Denied)
            .with_substituter(TestSubstituter)
            .build()
            .expect("Unable to build policy from json.");

        let request = Request::new("actor_a", "connect", "").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Denied));

        let request = Request::new("other_actor", "connect", "").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));
    }

    /// `TestSubstituter` replaces any value with the corresponding identity or resource
    /// from the request, thus making the variable rule to always match the request.
    struct TestSubstituter;

    impl Substituter for TestSubstituter {
        type Context = ();

        fn visit_identity(&self, _value: &str, context: &Request<Self::Context>) -> Result<String> {
            Ok(context.identity.clone())
        }

        fn visit_resource(&self, _value: &str, context: &Request<Self::Context>) -> Result<String> {
            Ok(context.resource.clone())
        }
    }
}
