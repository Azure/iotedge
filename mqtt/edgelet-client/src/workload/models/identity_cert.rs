use serde::{Deserialize, Serialize};

#[derive(Debug, Default, Serialize, Deserialize)]
pub struct IdentityCertificateRequest {
    /// Certificate expiration date-time (ISO 8601)
    #[serde(rename = "expiration", skip_serializing_if = "Option::is_none")]
    expiration: Option<String>,
}

impl IdentityCertificateRequest {
    pub fn new(expiration: Option<String>) -> Self {
        IdentityCertificateRequest { expiration }
    }

    pub fn set_expiration(&mut self, expiration: String) {
        self.expiration = Some(expiration);
    }

    #[must_use]
    pub fn with_expiration(mut self, expiration: String) -> Self {
        self.expiration = Some(expiration);
        self
    }

    pub fn expiration(&self) -> Option<&str> {
        self.expiration.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_expiration(&mut self) {
        self.expiration = None;
    }
}
