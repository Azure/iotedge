use thiserror::Error;

#[derive(Debug, Error, PartialEq)]
pub enum Error {
    #[error("One or several errors occurred validating MQTT broker policy definition: {0:?}.")]
    ValidationSummary(Vec<Error>),

    #[error("Identities list must not be empty")]
    EmptyIdentities,

    #[error("Operations list must not be empty")]
    EmptyOperations,

    #[error("Resources list must not be empty")]
    EmptyResources,

    #[error("Identity name is invalid: {0}")]
    InvalidIdentity(String),

    #[error("Resource (topic filter) is invalid: {0}")]
    InvalidResource(String),

    #[error("Unknown mqtt operation: {0}. List of supported operations: mqtt:publish, mqtt:subscribe, mqtt:connect")]
    InvalidOperation(String),

    #[error("Invalid identity variable name: {0}")]
    InvalidIdentityVariable(String),

    #[error("Invalid resource variable name: {0}")]
    InvalidResourceVariable(String),
}
