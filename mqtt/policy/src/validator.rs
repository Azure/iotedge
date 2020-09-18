use crate::{errors::Result, PolicyDefinition};

/// Trait to extend `PolicyBuilder` validation for policy definition.
pub trait PolicyValidator {
    /// This method is being called by `PolicyBuilder` for policy definition
    /// while `Policy` is being constructed.
    ///
    /// If a policy definitions fails the validation, the error is returned.
    fn validate(&self, definition: &PolicyDefinition) -> Result<()>;
}

#[derive(Debug)]
pub struct DefaultValidator;

impl PolicyValidator for DefaultValidator {
    fn validate(&self, _definition: &PolicyDefinition) -> Result<()> {
        Ok(())
    }
}
