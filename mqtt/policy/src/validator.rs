use thiserror::Error;

use crate::{PolicyDefinition, Statement};

/// Trait to extend `PolicyBuilder` validation for policy definition.
pub trait PolicyValidator {
    /// The type of the validation error.
    type Error;

    /// This method is being called by `PolicyBuilder` for policy definition
    /// while `Policy` is being constructed.
    ///
    /// If a policy definitions fails the validation, the error is returned.
    fn validate(&self, definition: &PolicyDefinition) -> Result<(), Self::Error>;
}

/// Provides basic validation that policy definition elements are not empty.
#[derive(Debug)]
pub struct DefaultValidator;

impl PolicyValidator for DefaultValidator {
    type Error = ValidatorError;

    fn validate(&self, definition: &PolicyDefinition) -> Result<(), Self::Error> {
        let errors = definition
            .statements()
            .iter()
            .flat_map(visit_statement)
            .collect::<Vec<_>>();

        if !errors.is_empty() {
            return Err(ValidatorError::ValidationSummary(errors));
        }
        Ok(())
    }
}

fn visit_statement(statement: &Statement) -> Vec<String> {
    let mut result = vec![];
    if statement.identities().is_empty() {
        result.push("Identities list must not be empty".into());
    }
    if statement.operations().is_empty() {
        result.push("Operations list must not be empty".into());
    }
    result
}

#[derive(Debug, Error)]
pub enum ValidatorError {
    #[error("An error occurred validating policy definition: {0:?}.")]
    ValidationSummary(Vec<String>),
}
