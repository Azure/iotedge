use std::{
    cmp::Ordering,
    collections::{btree_map::Entry, BTreeMap},
};

use crate::errors::Result;
use crate::{substituter::Substituter, Error, ResourceMatcher};

mod builder;
pub use builder::{Effect, PolicyBuilder, PolicyDefinition, Statement};

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
            // static rules undefined. Need to check variable rules.
            Ok(None) => match self.eval_variable_rules(request) {
                // variable rules undefined as well. Return default decision.
                Ok(None) => Ok(self.default_decision),
                // variable rules defined. Return the decision.
                Ok(Some(effect)) => Ok(effect.into()),
                Err(e) => Err(e),
            },
            // static rules are defined. Evaluate variable rules and compare priority.
            Ok(Some(static_effect)) => {
                match self.eval_variable_rules(request) {
                    // variable rules undefined. Proceed with static rule decision.
                    Ok(None) => Ok(static_effect.into()),
                    // variable rules defined. Compare priority and return.
                    Ok(Some(variable_effect)) => {
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

    #[allow(clippy::unnecessary_wraps)]
    fn eval_static_rules(&self, request: &Request<RC>) -> Result<Option<EffectOrd>> {
        // lookup an identity
        match self.static_rules.get(&request.identity) {
            // identity exists. Look up operations.
            Some(operations) => match operations.0.get(&request.operation) {
                // operation exists.
                Some(resources) => {
                    // iterate over and match resources.
                    // we need to go through all resources and find one with highest priority (smallest order).
                    let mut result: Option<EffectOrd> = None;
                    for (resource, effect) in &resources.0 {
                        // check the order first
                        if effect.order < result.map_or(usize::MAX, |e| e.order)
                             // only then check that matches
                            && self.resource_matcher.do_match( // only then check that matches
                                request,
                                &request.resource,
                                resource,
                            )
                        {
                            result = Some(*effect);
                        }
                    }
                    Ok(result)
                }
                None => Ok(None),
            },
            None => Ok(None),
        }
    }

    fn eval_variable_rules(&self, request: &Request<RC>) -> Result<Option<EffectOrd>> {
        for (identity, operations) in &self.variable_rules {
            // process identity variables.
            let identity = self.substituter.visit_identity(identity, request)?;
            // check if it does match after processing variables.
            if identity == request.identity {
                // lookup operation.
                return match operations.0.get(&request.operation) {
                    // operation exists.
                    Some(resources) => {
                        // iterate over and match resources.
                        // we need to go through all resources and find one with highest priority (smallest order).
                        let mut result: Option<EffectOrd> = None;
                        for (resource, effect) in &resources.0 {
                            let resource = self.substituter.visit_resource(resource, request)?;
                            // check the order first
                            if effect.order < result.map_or(usize::MAX, |e| e.order)
                                // only then check that matches
                                && self.resource_matcher.do_match(
                                    request,
                                    &request.resource,
                                    &resource,
                                )
                            {
                                result = Some(*effect);
                            }
                        }
                        // continue to look for other identity variable rules
                        // if no resources matched the current one.
                        if result == None {
                            continue;
                        }
                        Ok(result)
                    }
                    // continue to look for other identity variable rules
                    // if no operation found for the current one.
                    None => continue,
                };
            }
        }
        Ok(None)
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

#[derive(Debug, Copy, Clone, PartialEq)]
struct EffectOrd {
    order: usize,
    effect: Effect,
}

impl EffectOrd {
    pub fn new(effect: Effect, order: usize) -> Self {
        Self { order, effect }
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
                        "actor_a"
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
            .with_substituter(TestIdentitySubstituter)
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
            .with_substituter(TestIdentitySubstituter)
            .build()
            .expect("Unable to build policy from json.");

        let request = Request::new("actor_a", "connect", "").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Denied));

        let request = Request::new("other_actor", "connect", "").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));
    }

    #[test]
    fn evaluate_definition_no_statements() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [ ]
        }"#;

        let policy = build_policy(json);

        let request = Request::new("actor_a", "connect", "").unwrap();

        // default decision expected.
        assert_matches!(policy.evaluate(&request), Ok(Decision::Denied));
    }

    /// Scenario:
    /// - Have a policy with a custom resource matcher
    /// - Have conflicting rules for resources that
    ///   are different, but both will match according to
    ///   custom resource matcher.
    /// - Expected: match first "allow" rule
    ///
    /// This case is created as a result of a discovered bug.
    #[test]
    fn rule_ordering_should_work_for_custom_matchers() {
        let json = r###"{
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
                        "hello/b"
                    ]
                },
                {
                    "effect": "deny",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "hello/a"
                    ]
                }
            ]
        }"###;

        let policy = PolicyBuilder::from_json(json)
            .with_default_decision(Decision::Denied)
            .with_substituter(TestIdentitySubstituter)
            .with_matcher(StartWithMatcher)
            .build()
            .expect("Unable to build policy from json.");

        let request = Request::new("actor_a", "write", "hello").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));
    }

    /// See test case above for details.
    #[test]
    fn rule_ordering_should_work_for_custom_matchers_variable_rules() {
        let json = r###"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "{{any}}"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "hello/b"
                    ]
                },
                {
                    "effect": "deny",
                    "identities": [
                        "{{any}}"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "hello/a"
                    ]
                }
            ]
        }"###;

        let policy = PolicyBuilder::from_json(json)
            .with_default_decision(Decision::Denied)
            .with_substituter(TestIdentitySubstituter)
            .with_matcher(StartWithMatcher)
            .build()
            .expect("Unable to build policy from json.");

        let request = Request::new("actor_a", "write", "hello").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));
    }

    /// Scenario:
    /// - Have a policy with a custom identity matcher
    /// - Have two variable rules (deny and allow) for an identity, such that
    ///   both rules match a given request identity.
    /// - But the two rules must be different in resources.
    /// - Make a request to the allowed resource.
    /// - The deny rule resources do not match the request.
    /// - The allow rule resources do match the request.
    /// - Expected: request allowed.
    ///
    /// This case is created as a result of a discovered bug.
    #[test]
    fn all_identity_variable_rules_must_be_evaluated_resources_do_not_match() {
        let json = r###"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "deny",
                    "identities": [
                        "{{any}}"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "hello/b"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "{{identity}}"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "hello/a"
                    ]
                }
            ]
        }"###;

        let policy = PolicyBuilder::from_json(json)
            .with_default_decision(Decision::Denied)
            .with_substituter(TestIdentitySubstituter)
            .with_matcher(DefaultResourceMatcher)
            .build()
            .expect("Unable to build policy from json.");

        let request = Request::new("actor_a", "write", "hello/a").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));
    }

    /// Scenario:
    /// - The same as test case above,
    ///   but statement operations are different.
    ///
    /// This case is created as a result of a discovered bug.
    #[test]
    fn all_identity_variable_rules_must_be_evaluated_operations_do_not_match() {
        let json = r###"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "deny",
                    "identities": [
                        "{{any}}"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "hello/b"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "{{identity}}"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "hello/a"
                    ]
                }
            ]
        }"###;

        let policy = PolicyBuilder::from_json(json)
            .with_default_decision(Decision::Denied)
            .with_substituter(TestIdentitySubstituter)
            .with_matcher(DefaultResourceMatcher)
            .build()
            .expect("Unable to build policy from json.");

        let request = Request::new("actor_a", "write", "hello/a").unwrap();

        assert_matches!(policy.evaluate(&request), Ok(Decision::Allowed));
    }

    /// `TestSubstituter` replaces any value with the corresponding identity
    /// from the request, thus making the variable rule to always match the request.
    #[derive(Debug)]
    struct TestIdentitySubstituter;

    impl Substituter for TestIdentitySubstituter {
        type Context = ();

        fn visit_identity(&self, _value: &str, context: &Request<Self::Context>) -> Result<String> {
            Ok(context.identity.clone())
        }

        fn visit_resource(&self, value: &str, _context: &Request<Self::Context>) -> Result<String> {
            Ok(value.into())
        }
    }

    /// `StartWithMatcher` matches resources that start with requested value. For
    /// example, if a policy defines a resource "hello/world", then request for "hello/"
    /// will match.
    #[derive(Debug)]
    struct StartWithMatcher;

    impl ResourceMatcher for StartWithMatcher {
        type Context = ();

        fn do_match(&self, _: &Request<Self::Context>, input: &str, policy: &str) -> bool {
            policy.starts_with(input)
        }
    }

    #[cfg(feature = "proptest")]
    mod proptests {
        use crate::{Decision, Effect, PolicyBuilder, PolicyDefinition, Request, Statement};
        use proptest::{collection::vec, prelude::*};

        proptest! {
            /// The goal of this test is to verify the following scenarios:
            /// - PolicyBuilder does not crash.
            /// - All combinations of identity/operation/resource in a statement in the definition
            ///   should produce expected result.
            ///   Since some statements can be overridden by the previous ones,
            ///   we can only safely verify the very first statement.
            #[test]
            fn policy_engine_proptest(definition in arb_policy_definition()){
                use itertools::iproduct;

                // take very first statement, which should have top priority.
                let statement = &definition.statements()[0];
                let expected = match statement.effect() {
                    Effect::Allow => Decision::Allowed,
                    Effect::Deny => Decision::Denied,
                };

                // collect all combos of identity/operation/resource
                // in the statement.
                let requests = iproduct!(
                    statement.identities(),
                    statement.operations(),
                    statement.resources()
                )
                .map(|item| Request::new(item.0, item.1, item.2).expect("unable to create a request"))
                .collect::<Vec<_>>();

                let policy = PolicyBuilder::from_definition(definition)
                    .build()
                    .expect("unable to build policy from definition");

                // evaluate and assert.
                for request in requests {
                    assert_eq!(policy.evaluate(&request).unwrap(), expected);
                }
            }
        }

        prop_compose! {
            pub fn arb_policy_definition()(
                statements in vec(arb_statement(), 1..5)
            ) -> PolicyDefinition {
                PolicyDefinition {
                    statements
                }
            }
        }

        prop_compose! {
            pub fn arb_statement()(
                description in arb_description(),
                effect in arb_effect(),
                identities in vec(arb_identity(), 1..5),
                operations in vec(arb_operation(), 1..5),
                resources in vec(arb_resource(), 1..5),
            ) -> Statement {
                Statement{
                    order: 0,
                    description,
                    effect,
                    identities,
                    operations,
                    resources,
                }
            }
        }

        pub fn arb_effect() -> impl Strategy<Value = Effect> {
            prop_oneof![Just(Effect::Allow), Just(Effect::Deny)]
        }

        pub fn arb_description() -> impl Strategy<Value = String> {
            "\\PC+"
        }

        pub fn arb_identity() -> impl Strategy<Value = String> {
            "(\\PC+)|(\\{\\{\\PC+\\}\\})"
        }

        pub fn arb_operation() -> impl Strategy<Value = String> {
            "\\PC+"
        }

        pub fn arb_resource() -> impl Strategy<Value = String> {
            "\\PC+(/(\\PC+|\\{\\{\\PC+\\}\\}))*"
        }
    }
}
