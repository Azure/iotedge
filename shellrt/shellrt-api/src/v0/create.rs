use serde::{Deserialize, Serialize};

/// Create a new module
#[derive(Debug, Serialize, Deserialize)]
pub struct CreateRequest {
    // TODO: fill in CreateRequest!
}

/// Returned once a CreateRequest completes successfully
#[derive(Debug, Serialize, Deserialize)]
pub struct CreateResponse {}
