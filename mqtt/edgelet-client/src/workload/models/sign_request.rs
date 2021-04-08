use serde::{Deserialize, Serialize};

pub const DEFAULT_KEY_ID: &str = "primary";

#[derive(Debug, Serialize, Deserialize)]
pub struct SignRequest {
    /// Name of key to perform sign operation.
    #[serde(rename = "keyId")]
    key_id: String,
    /// Sign algorithm to be used.
    #[serde(rename = "algo")]
    algorithm: Algorithm,
    /// Data to be signed.
    #[serde(rename = "data")]
    data: String,
}

impl SignRequest {
    pub fn new(data: String) -> Self {
        SignRequest {
            key_id: DEFAULT_KEY_ID.into(),
            algorithm: Algorithm::HmacSha256,
            data,
        }
    }

    pub fn set_key_id(&mut self, key_id: String) {
        self.key_id = key_id;
    }

    pub fn with_key_id(mut self, key_id: String) -> Self {
        self.key_id = key_id;
        self
    }

    pub fn key_id(&self) -> &String {
        &self.key_id
    }

    pub fn set_algorithm(&mut self, algorithm: Algorithm) {
        self.algorithm = algorithm;
    }

    pub fn with_algorithm(mut self, algorithm: Algorithm) -> Self {
        self.algorithm = algorithm;
        self
    }

    pub fn algorithm(&self) -> &Algorithm {
        &self.algorithm
    }

    pub fn set_data(&mut self, data: String) {
        self.data = data;
    }

    pub fn with_data(mut self, data: String) -> Self {
        self.data = data;
        self
    }

    pub fn data(&self) -> &String {
        &self.data
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub enum Algorithm {
    #[serde(rename = "HMACSHA256")]
    HmacSha256,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct SignResponse {
    /// Signature of the data.
    #[serde(rename = "digest")]
    digest: String,
}

impl SignResponse {
    pub fn new(digest: String) -> Self {
        SignResponse { digest }
    }

    pub fn set_digest(&mut self, digest: String) {
        self.digest = digest;
    }

    pub fn with_digest(mut self, digest: String) -> Self {
        self.digest = digest;
        self
    }

    pub fn digest(&self) -> &String {
        &self.digest
    }
}
