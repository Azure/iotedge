use serde::{Deserialize, Serialize};

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
            key_id: "device_key".to_string(),
            algorithm: Algorithm::HMACSHA256,
            data,
        }
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
    HMACSHA256,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct SignResponse {
     /// Signature of the data.
     #[serde(rename = "digest")]
     digest: String,
}

impl SignResponse {
    pub fn new(digest: String) -> Self {
        SignResponse {
            digest,
        }
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
