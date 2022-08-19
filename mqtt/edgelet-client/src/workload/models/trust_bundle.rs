use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize)]
pub struct TrustBundleResponse {
    /// Base64 encoded PEM formatted byte array containing the trusted certificates.
    #[serde(rename = "certificate")]
    certificate: String,
}

impl TrustBundleResponse {
    pub fn new(certificate: String) -> Self {
        TrustBundleResponse { certificate }
    }

    pub fn set_certificate(&mut self, certificate: String) {
        self.certificate = certificate;
    }

    #[must_use]
    pub fn with_certificate(mut self, certificate: String) -> Self {
        self.certificate = certificate;
        self
    }

    pub fn certificate(&self) -> &String {
        &self.certificate
    }
}
