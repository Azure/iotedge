use serde::{Deserialize, Serialize};
#[derive(Debug, Serialize, Deserialize)]
pub struct CertificateResponse {
    #[serde(rename = "privateKey")]
    private_key: PrivateKey,

    /// Base64 encoded PEM formatted byte array containing the certificate and its chain.
    #[serde(rename = "certificate")]
    certificate: String,

    /// Certificate expiration date-time (ISO 8601)
    #[serde(rename = "expiration")]
    expiration: String,
}

impl CertificateResponse {
    pub fn new(private_key: PrivateKey, certificate: String, expiration: String) -> Self {
        CertificateResponse {
            private_key,
            certificate,
            expiration,
        }
    }

    pub fn set_private_key(&mut self, private_key: PrivateKey) {
        self.private_key = private_key;
    }

    #[must_use]
    pub fn with_private_key(mut self, private_key: PrivateKey) -> Self {
        self.private_key = private_key;
        self
    }

    pub fn private_key(&self) -> &PrivateKey {
        &self.private_key
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

#[derive(Debug, Serialize, Deserialize)]
pub struct PrivateKey {
    /// Indicates format of the key (present in PEM formatted bytes or a reference)
    #[serde(rename = "type")]
    type_: String,

    /// Reference to private key.
    #[serde(rename = "ref", skip_serializing_if = "Option::is_none")]
    ref_: Option<String>,

    /// Base64 encoded PEM formatted byte array
    #[serde(rename = "bytes", skip_serializing_if = "Option::is_none")]
    bytes: Option<String>,
}

impl PrivateKey {
    pub fn new(type_: String) -> Self {
        PrivateKey {
            type_,
            ref_: None,
            bytes: None,
        }
    }

    pub fn set_type(&mut self, type_: String) {
        self.type_ = type_;
    }

    #[must_use]
    pub fn with_type(mut self, type_: String) -> Self {
        self.type_ = type_;
        self
    }

    pub fn type_(&self) -> &String {
        &self.type_
    }

    pub fn set_ref(&mut self, ref_: String) {
        self.ref_ = Some(ref_);
    }

    #[must_use]
    pub fn with_ref(mut self, ref_: String) -> Self {
        self.ref_ = Some(ref_);
        self
    }

    pub fn ref_(&self) -> Option<&str> {
        self.ref_.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_ref(&mut self) {
        self.ref_ = None;
    }

    pub fn set_bytes(&mut self, bytes: String) {
        self.bytes = Some(bytes);
    }

    #[must_use]
    pub fn with_bytes(mut self, bytes: String) -> Self {
        self.bytes = Some(bytes);
        self
    }

    pub fn bytes(&self) -> Option<&str> {
        self.bytes.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_bytes(&mut self) {
        self.bytes = None;
    }
}
