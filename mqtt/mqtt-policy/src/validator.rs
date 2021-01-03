use std::collections::HashSet;
use std::{iter::FromIterator, str::FromStr};

use lazy_static::lazy_static;

use mqtt_broker::TopicFilter;
use policy::{PolicyDefinition, PolicyValidator, Statement};

use crate::{errors::Error, substituter::VariableIter};

/// MQTT-specific implementation of `PolicyValidator`. It checks the following rules:
/// * Valid schema version.
/// * Presence of all elements in the policy definition (identities, operations, resources)
/// * Valid list of operations: mqtt:connect, mqtt:publish, mqtt:subscribe.
/// * Valid topic filter structure.
/// * Valid variable names.
#[derive(Debug)]
pub struct MqttValidator;

impl PolicyValidator for MqttValidator {
    type Error = Error;

    fn validate(&self, definition: &PolicyDefinition) -> Result<(), Error> {
        visit_definition(definition)
    }
}

fn visit_definition(definition: &PolicyDefinition) -> Result<(), Error> {
    let errors = definition
        .statements()
        .iter()
        .flat_map(|statement| {
            let statement_errors = visit_statement(statement);
            let identity_errors = statement
                .identities()
                .iter()
                .filter_map(|i| visit_identity(i).err());
            let operation_errors = statement
                .operations()
                .iter()
                .filter_map(|o| visit_operation(o).err());
            let resource_errors = statement
                .resources()
                .iter()
                .filter_map(|r| visit_resource(r).err());

            statement_errors
                .into_iter()
                .chain(identity_errors)
                .chain(operation_errors)
                .chain(resource_errors)
                .collect::<Vec<_>>()
        })
        .collect::<Vec<_>>();

    if !errors.is_empty() {
        return Err(Error::ValidationSummary(errors));
    }
    Ok(())
}

fn visit_statement(statement: &Statement) -> Vec<Error> {
    let mut result = vec![];
    if statement.identities().is_empty() {
        result.push(Error::EmptyIdentities);
    }
    if statement.operations().is_empty() {
        result.push(Error::EmptyOperations);
    }
    // resources list can be empty only for connect operation.
    if statement.resources().is_empty() && !is_connect_op(statement) {
        result.push(Error::EmptyResources);
    }
    result
}

fn visit_identity(value: &str) -> Result<(), Error> {
    if value.is_empty() {
        return Err(Error::InvalidIdentity(value.into()));
    }
    for variable in VariableIter::new(value) {
        if VALID_VARIABLES.get(variable).is_none() {
            return Err(Error::InvalidIdentityVariable(variable.into()));
        }
    }
    Ok(())
}

fn visit_operation(value: &str) -> Result<(), Error> {
    match value {
        "mqtt:publish" | "mqtt:subscribe" | "mqtt:connect" => Ok(()),
        _ => Err(Error::InvalidOperation(value.into())),
    }
}

fn visit_resource(value: &str) -> Result<(), Error> {
    if value.is_empty() {
        return Err(Error::InvalidResource(value.into()));
    }
    for variable in VariableIter::new(value) {
        if VALID_VARIABLES.get(variable).is_none() {
            return Err(Error::InvalidResourceVariable(variable.into()));
        }
    }
    if TopicFilter::from_str(value).is_err() {
        return Err(Error::InvalidResource(value.into()));
    }
    Ok(())
}

fn is_connect_op(statement: &Statement) -> bool {
    // check that there is exactly one operation and it is mqtt:connect.
    statement.operations().len() == 1 && statement.operations()[0] == "mqtt:connect"
}

lazy_static! {
    static ref VALID_VARIABLES: HashSet<String> = HashSet::from_iter(vec![
        crate::IDENTITY_VAR.into(),
        crate::DEVICE_ID_VAR.into(),
        crate::MODULE_ID_VAR.into(),
        crate::CLIENT_ID_VAR.into(),
        crate::EDGEHUB_ID_VAR.into(),
    ]);
}

#[cfg(test)]
mod tests {
    use assert_matches::assert_matches;

    use policy::PolicyDefinition;

    use super::*;

    impl Error {
        /// Helper method to extract error summary.
        fn into_summary(self) -> Vec<Error> {
            match self {
                Error::ValidationSummary(errors) => errors,
                _ => panic!("Not a ValidationSummary variant"),
            }
        }
    }

    fn build_definition(json: &str) -> PolicyDefinition {
        PolicyDefinition::from_json(json).expect("Unable to build definition from json")
    }

    #[test]
    fn successful_validation() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "contoso.azure-devices.net/monitor_a"
                    ],
                    "operations": [
                        "mqtt:connect",
                        "mqtt:subscribe",
                        "mqtt:publish"
                    ],
                    "resources": [
                        "topic/a"
                    ]
                }
            ]
        }"#;

        assert_matches!(MqttValidator.validate(&build_definition(json)), Ok(()));
    }

    #[test]
    fn invalid_operation() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "contoso.azure-devices.net/monitor_a"
                    ],
                    "operations": [
                        "invalid"
                    ],
                    "resources": [
                        "topic/a"
                    ]
                }
            ]
        }"#;

        let err = MqttValidator.validate(&build_definition(json)).unwrap_err();
        assert_eq!(
            err.into_summary(),
            vec![Error::InvalidOperation("invalid".into())]
        );
    }

    #[test]
    fn empty_elements() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                    ],
                    "operations": [
                    ],
                    "resources": [
                    ]
                }
            ]
        }"#;

        let err = MqttValidator.validate(&build_definition(json)).unwrap_err();
        assert_eq!(
            err.into_summary(),
            vec![
                Error::EmptyIdentities,
                Error::EmptyOperations,
                Error::EmptyResources
            ]
        );
    }

    #[test]
    fn empty_strings() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        ""
                    ],
                    "operations": [
                        ""
                    ],
                    "resources": [
                        ""
                    ]
                }
            ]
        }"#;

        let err = MqttValidator.validate(&build_definition(json)).unwrap_err();
        assert_eq!(
            err.into_summary(),
            vec![
                Error::InvalidIdentity("".into()),
                Error::InvalidOperation("".into()),
                Error::InvalidResource("".into())
            ]
        );
    }

    #[test]
    fn invalid_variables() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "{{invalid_id}}"
                    ],
                    "operations": [
                        "mqtt:publish"
                    ],
                    "resources": [
                        "{{invalid_res}}"
                    ]
                }
            ]
        }"#;

        let err = MqttValidator.validate(&build_definition(json)).unwrap_err();
        assert_eq!(
            err.into_summary(),
            vec![
                Error::InvalidIdentityVariable("{{invalid_id}}".into()),
                Error::InvalidResourceVariable("{{invalid_res}}".into())
            ]
        );
    }

    #[test]
    fn empty_resource_for_connect() {
        let json = r#"{
            "schemaVersion": "2020-10-30",
            "statements": [
                {
                    "effect": "allow",
                    "identities": [
                        "contoso.azure-devices.net/monitor_a"
                    ],
                    "operations": [
                        "mqtt:connect"
                    ] 
                }
            ]
        }"#;

        assert!(MqttValidator.validate(&build_definition(json)).is_ok());
    }
}
