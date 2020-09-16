use lazy_static::lazy_static;
use regex::Regex;
use serde::Deserialize;

use crate::{
    core::{Identities, Operations, Resources},
    Decision, DefaultResourceMatcher, DefaultSubstituter, DefaultValidator, Error, Policy,
    PolicyValidator, ResourceMatcher, Result, Substituter,
};

/// A policy builder, responsible for parsing policy definition
/// and constructing `Policy` struct.
///
/// It handles policy definition versioning and allows fine-grained
/// configuration of `Policy` components.
pub struct PolicyBuilder<V, M, S> {
    validator: V,
    matcher: M,
    substituter: S,
    json: String,
    default_decision: Decision,
}

impl PolicyBuilder<DefaultValidator, DefaultResourceMatcher, DefaultSubstituter> {
    /// Constructs a `PolicyBuilder` from provided json policy definition and
    /// default configuration.
    ///
    /// Call to this method does not parse or validate the json, all heavy work
    /// is done in `build` method.
    pub fn from_json(
        json: &str,
    ) -> PolicyBuilder<DefaultValidator, DefaultResourceMatcher, DefaultSubstituter> {
        PolicyBuilder {
            json: json.into(),
            validator: DefaultValidator,
            matcher: DefaultResourceMatcher,
            substituter: DefaultSubstituter,
            default_decision: Decision::Denied,
        }
    }
}

impl<V, M, S> PolicyBuilder<V, M, S>
where
    V: PolicyValidator,
    M: ResourceMatcher,
    S: Substituter,
{
    /// Specifies the `PolicyValidator` to validate the policy definition.
    pub fn with_validator<V1>(self, validator: V1) -> PolicyBuilder<V1, M, S> {
        PolicyBuilder {
            json: self.json,
            validator,
            matcher: self.matcher,
            substituter: self.substituter,
            default_decision: self.default_decision,
        }
    }

    /// Specifies the `ResourceMatcher` to use with `Policy`.
    pub fn with_matcher<M1>(self, matcher: M1) -> PolicyBuilder<V, M1, S> {
        PolicyBuilder {
            json: self.json,
            validator: self.validator,
            matcher,
            substituter: self.substituter,
            default_decision: self.default_decision,
        }
    }

    /// Specifies the `Substituter` to use with `Policy`.
    pub fn with_substituter<S1>(self, substituter: S1) -> PolicyBuilder<V, M, S1> {
        PolicyBuilder {
            json: self.json,
            validator: self.validator,
            matcher: self.matcher,
            substituter,
            default_decision: self.default_decision,
        }
    }

    /// Specifies the default decision that `Policy` will return if
    /// no rules match the request.
    pub fn with_default_decision(mut self, decision: Decision) -> Self {
        self.default_decision = decision;
        self
    }

    /// Builds a `Policy` consuming the builder.
    ///
    /// This method does all the heavy lifting of deserializing json, validating and
    /// constructing the policy rules tree.
    ///
    /// Any validation errors are collected and returned as `Error::ValidationSummary`.
    pub fn build(self) -> Result<Policy<M, S>> {
        let mut definition: PolicyDefinition =
            serde_json::from_str(&self.json).map_err(Error::Deserializing)?;

        for (order, mut statement) in definition.statements.iter_mut().enumerate() {
            statement.order = order;
        }

        self.validate(&definition)?;

        let mut static_rules = Identities::new();
        let mut variable_rules = Identities::new();

        for statement in definition.statements {
            process_statement(&statement, &mut static_rules, &mut variable_rules);
        }

        Ok(Policy {
            default_decision: self.default_decision,
            resource_matcher: self.matcher,
            substituter: self.substituter,
            static_rules: static_rules.0,
            variable_rules: variable_rules.0,
        })
    }

    fn validate(&self, definition: &PolicyDefinition) -> Result<()> {
        self.validator.validate(definition)
    }
}

fn process_statement(
    statement: &Statement,
    static_rules: &mut Identities,
    variable_rules: &mut Identities,
) {
    let (static_ids, variable_ids) = process_identities(statement);

    static_rules.merge(static_ids);
    variable_rules.merge(variable_ids);
}

fn process_identities(statement: &Statement) -> (Identities, Identities) {
    let mut static_ids = Identities::new();
    let mut variable_ids = Identities::new();
    for identity in &statement.identities {
        let (static_ops, variable_ops) = process_operations(&statement);

        if is_variable_rule(identity) {
            // if current identity has substitutions,
            // then the whole operation subtree need
            // to be cloned into substitutions tree.
            let mut all = static_ops.clone();
            all.merge(variable_ops);
            variable_ids.insert(identity, all);
        } else {
            // else, divide operations and operation substitutions
            // between identities and identity substitutions.
            static_ids.insert(identity, static_ops);
            variable_ids.insert(identity, variable_ops);
        }
    }

    (static_ids, variable_ids)
}

fn process_operations(statement: &Statement) -> (Operations, Operations) {
    let mut static_ops = Operations::new();
    let mut variable_ops = Operations::new();
    for operation in &statement.operations {
        let (static_res, variable_res) = process_resources(&statement);

        if is_variable_rule(operation) {
            // if current operation has variables,
            // then the whole resource subtree need
            // to be cloned into variables tree.
            let mut all = static_res.clone();
            all.merge(variable_res);
            variable_ops.insert(operation, all);
        } else {
            // else, divide static resources and variable resources
            // between static operations and variable operation.
            static_ops.insert(operation, static_res);
            variable_ops.insert(operation, variable_res);
        }
    }

    (static_ops, variable_ops)
}

fn process_resources(statement: &Statement) -> (Resources, Resources) {
    let mut static_res = Resources::new();
    let mut variable_res = Resources::new();
    if statement.resources.is_empty() {
        static_res.insert("", statement.into());
    }

    for resource in &statement.resources {
        // split resources into two buckets - static or variable rules:
        let map = if is_variable_rule(resource) {
            &mut variable_res
        } else {
            &mut static_res
        };

        map.insert(resource, statement.into());
    }

    (static_res, variable_res)
}

fn is_variable_rule(value: &str) -> bool {
    lazy_static! {
        static ref VAR_PATTERN: Regex =
            Regex::new(r#"\{\{[^\{\}]+\}\}"#).expect("failed to create a Regex from pattern");
    }
    VAR_PATTERN.is_match(value)
}

/// Represents a deserialized policy definition.
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PolicyDefinition {
    schema_version: String,
    statements: Vec<Statement>,
}

impl PolicyDefinition {
    pub fn schema_version(&self) -> &str {
        &self.schema_version
    }

    pub fn statements(&self) -> &Vec<Statement> {
        &self.statements
    }
}

/// Represents a statement in a policy definition.
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Statement {
    #[serde(default)]
    order: usize,
    #[serde(default)]
    description: String,
    effect: Effect,
    identities: Vec<String>,
    operations: Vec<String>,
    #[serde(default)]
    resources: Vec<String>,
}

impl Statement {
    pub(crate) fn order(&self) -> usize {
        self.order
    }

    pub fn description(&self) -> &str {
        &self.description
    }

    pub fn effect(&self) -> Effect {
        self.effect
    }

    pub fn identities(&self) -> &Vec<String> {
        &self.identities
    }

    pub fn operations(&self) -> &Vec<String> {
        &self.operations
    }

    pub fn resources(&self) -> &Vec<String> {
        &self.resources
    }
}

/// Represents an effect on a statement.
#[derive(Deserialize, Copy, Clone)]
#[serde(rename_all = "camelCase")]
pub enum Effect {
    Allow,
    Deny,
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::core::{tests::build_policy, Effect as CoreEffect, EffectOrd};
    use matches::assert_matches;

    #[test]
    fn test_basic_definition() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "contoso.azure-devices.net/monitor_a"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "resource_group"
                    ]
                },
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
                    "description": "Deny all other identities to read",
                    "effect": "deny",
                    "identities": [
                        "{{var_actor}}"
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

        let policy = build_policy(json);

        assert_eq!(1, policy.variable_rules.len());
        assert_eq!(2, policy.static_rules.len());
    }

    #[test]
    fn identity_merge_rules() {
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
                        "events/telemetry"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "resource_1"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "{{variable}}/#"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "write"
                    ],
                    "resources": [
                        "{{variable}}/#"
                    ]
                }
            ]
        }"#;

        let policy = build_policy(json);

        // assert static rules have 1 identity and 2 operations
        assert_eq!(1, policy.static_rules.len());
        assert_eq!(2, policy.static_rules["actor_a"].0.len());

        // assert variable rules have 1 identity and 2 operations
        assert_eq!(1, policy.variable_rules.len());
        assert_eq!(2, policy.variable_rules["actor_a"].0.len());
    }

    #[test]
    fn operation_merge_rules() {
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
                        "events/telemetry"
                    ]
                },
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
                    "effect": "allow",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "{{variable}}/#"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "devices/{{variable}}/#"
                    ]
                }
            ]
        }"#;

        let policy = build_policy(json);

        // assert static rules have 1 identity, 1 operations and 2 resources
        assert_eq!(1, policy.static_rules["actor_a"].0.len());
        assert_eq!(2, policy.static_rules["actor_a"].0["write"].0.len());

        // assert variable rules have 1 identity, 1 operations and 2 resources
        assert_eq!(1, policy.variable_rules["actor_a"].0.len());
        assert_eq!(2, policy.variable_rules["actor_a"].0["read"].0.len());
    }

    #[test]
    fn resource_merge_rules_higher_priority_statement_wins() {
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
                        "events/telemetry"
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
                        "events/telemetry"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "{{variable}}/#"
                    ]
                },
                {
                    "effect": "deny",
                    "identities": [
                        "actor_a"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "{{variable}}/#"
                    ]
                }
            ]
        }"#;

        let policy = build_policy(json);

        // assert higher priority rule wins.
        assert_eq!(
            EffectOrd {
                order: 0,
                effect: CoreEffect::Allow
            },
            policy.static_rules["actor_a"].0["write"].0["events/telemetry"]
        );

        // assert higher priority rule wins for variable rules.
        assert_eq!(
            EffectOrd {
                order: 2,
                effect: CoreEffect::Allow
            },
            policy.variable_rules["actor_a"].0["read"].0["{{variable}}/#"]
        );
    }

    #[test]
    #[allow(clippy::too_many_lines)]
    fn grouping_rules_with_variables_test() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "actor_a",
                        "actor_b",
                        "{{var_actor}}"
                    ],
                    "operations": [
                        "write",
                        "read"
                    ],
                    "resources": [
                        "events/telemetry",
                        "devices/{{variable}}/#"
                    ]
                }
            ]
        }"#;

        let policy = build_policy(json);

        // assert static rules.
        assert_eq!(2, policy.static_rules.len());
        assert_eq!(
            policy.static_rules["actor_a"].0["write"].0["events/telemetry"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );
        assert_eq!(
            policy.static_rules["actor_a"].0["read"].0["events/telemetry"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );
        assert_eq!(
            policy.static_rules["actor_b"].0["write"].0["events/telemetry"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );
        assert_eq!(
            policy.static_rules["actor_b"].0["read"].0["events/telemetry"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );

        // assert variable rules.
        assert_eq!(3, policy.variable_rules.len());
        assert_eq!(
            policy.variable_rules["actor_a"].0["write"].0["devices/{{variable}}/#"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );
        assert_eq!(
            policy.variable_rules["actor_a"].0["read"].0["devices/{{variable}}/#"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );
        assert_eq!(
            policy.variable_rules["actor_b"].0["write"].0["devices/{{variable}}/#"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );
        assert_eq!(
            policy.variable_rules["actor_b"].0["read"].0["devices/{{variable}}/#"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );
        assert_eq!(
            policy.variable_rules["{{var_actor}}"].0["write"].0["devices/{{variable}}/#"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );
        assert_eq!(
            policy.variable_rules["{{var_actor}}"].0["read"].0["devices/{{variable}}/#"],
            EffectOrd {
                effect: CoreEffect::Allow,
                order: 0
            }
        );
    }

    #[test]
    fn policy_validation_test() {
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
                        "events/telemetry"
                    ]
                },
                {
                    "effect": "allow",
                    "identities": [
                        "contoso.azure-devices.net/monitor"
                    ],
                    "operations": [
                        "read"
                    ],
                    "resources": [
                        "events/telemetry"
                    ]
                }
            ]
        }"#;

        let result = PolicyBuilder::from_json(json)
            .with_validator(FailAllValidator)
            .with_default_decision(Decision::Denied)
            .build();

        assert_matches!(result, Err(Error::ValidationSummary(errors)) if errors.len() == 1 );
    }

    #[derive(Debug)]
    struct FailAllValidator;

    impl PolicyValidator for FailAllValidator {
        fn validate(&self, _definition: &PolicyDefinition) -> Result<()> {
            Err(Error::ValidationSummary(vec![Error::Validation(
                "Unable to parse policy definition".to_string(),
            )]))
        }
    }
}
