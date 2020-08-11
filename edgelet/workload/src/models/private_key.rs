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
pub struct PrivateKey {
  /// Indicates format of the key (present in PEM formatted bytes or a reference)
  #[serde(rename = "type")]
  type_: String,
  /// Reference to private key.
  #[serde(rename = "ref")]
  ref_: Option<String>,
  /// Base64 encoded PEM formatted byte array
  #[serde(rename = "bytes")]
  bytes: Option<String>
}

impl PrivateKey {
  pub fn new(type_: String) -> PrivateKey {
    PrivateKey {
      type_: type_,
      ref_: None,
      bytes: None
    }
  }

  pub fn set_type_(&mut self, type_: String) {
    self.type_ = type_;
  }

  pub fn with_type_(mut self, type_: String) -> PrivateKey {
    self.type_ = type_;
    self
  }

  pub fn type_(&self) -> &String {
    &self.type_
  }


  pub fn set_ref_(&mut self, ref_: String) {
    self.ref_ = Some(ref_);
  }

  pub fn with_ref_(mut self, ref_: String) -> PrivateKey {
    self.ref_ = Some(ref_);
    self
  }

  pub fn ref_(&self) -> Option<&String> {
    self.ref_.as_ref()
  }

  pub fn reset_ref_(&mut self) {
    self.ref_ = None;
  }

  pub fn set_bytes(&mut self, bytes: String) {
    self.bytes = Some(bytes);
  }

  pub fn with_bytes(mut self, bytes: String) -> PrivateKey {
    self.bytes = Some(bytes);
    self
  }

  pub fn bytes(&self) -> Option<&String> {
    self.bytes.as_ref()
  }

  pub fn reset_bytes(&mut self) {
    self.bytes = None;
  }

}



