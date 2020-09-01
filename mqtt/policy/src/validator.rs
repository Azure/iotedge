use crate::errors::Result;

/// Trait to extend `PolicyBuilder` validation for policy definition.
pub trait PolicyValidator {
    /// This method is being called by `PolicyBuilder` on every filed in policy definition.
    fn validate(&self, field: Field, value: &str) -> Result<()>;
}

#[derive(Debug)]
pub enum Field {
    Identities,
    Operations,
    Resources,
    Description,
}

#[derive(Debug)]
pub struct DefaultValidator;

impl PolicyValidator for DefaultValidator {
    fn validate(&self, _field: Field, _value: &str) -> Result<()> {
        Ok(())
    }
}
