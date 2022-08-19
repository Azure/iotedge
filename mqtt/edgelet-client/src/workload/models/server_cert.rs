use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize)]
pub struct ServerCertificateRequest {
    /// Subject common name
    #[serde(rename = "commonName")]
    common_name: String,

    /// Certificate expiration date-time (ISO 8601)
    #[serde(rename = "expiration")]
    expiration: String,
}

impl ServerCertificateRequest {
    pub fn new(common_name: String, expiration: String) -> Self {
        ServerCertificateRequest {
            common_name,
            expiration,
        }
    }

    pub fn set_common_name(&mut self, common_name: String) {
        self.common_name = common_name;
    }

    #[must_use]
    pub fn with_common_name(mut self, common_name: String) -> Self {
        self.common_name = common_name;
        self
    }

    pub fn common_name(&self) -> &String {
        &self.common_name
    }

    pub fn set_expiration(&mut self, expiration: String) {
        self.expiration = expiration;
    }

    #[must_use]
    pub fn with_expiration(mut self, expiration: String) -> Self {
        self.expiration = expiration;
        self
    }

    pub fn expiration(&self) -> &String {
        &self.expiration
    }
}
