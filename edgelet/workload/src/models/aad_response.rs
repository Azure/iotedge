use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct AADResponse {
    /// AAD access token in JWT form
    #[serde(rename = "token")]
    token: String,
}

impl AADResponse {
    pub fn new(token: String) -> Self {
        AADResponse { token }
    }

    pub fn set_token(&mut self, token: String) {
        self.token = token;
    }

    pub fn with_token(mut self, token: String) -> Self {
        self.token = token;
        self
    }

    pub fn token(&self) -> &String {
        &self.token
    }
}
