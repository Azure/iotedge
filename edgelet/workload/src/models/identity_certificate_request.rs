/* 
 * IoT Edge Module Workload API
 *
 * No description provided (generated by Swagger Codegen https://github.com/swagger-api/swagger-codegen)
 *
 * OpenAPI spec version: 2020-07-22
 * 
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */


#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct IdentityCertificateRequest {
  /// Certificate expiration date-time (ISO 8601)
  #[serde(rename = "expiration")]
  expiration: Option<String>
}

impl IdentityCertificateRequest {
  pub fn new() -> IdentityCertificateRequest {
    IdentityCertificateRequest {
      expiration: None
    }
  }

  pub fn set_expiration(&mut self, expiration: String) {
    self.expiration = Some(expiration);
  }

  pub fn with_expiration(mut self, expiration: String) -> IdentityCertificateRequest {
    self.expiration = Some(expiration);
    self
  }

  pub fn expiration(&self) -> Option<&String> {
    self.expiration.as_ref()
  }

  pub fn reset_expiration(&mut self) {
    self.expiration = None;
  }

}



