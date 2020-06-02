/* 
 * Identity Service API
 *
 * No description provided (generated by Swagger Codegen https://github.com/swagger-api/swagger-codegen)
 *
 * OpenAPI spec version: 2020-06-01
 * 
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */


#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct Credentials {
  /// Indicates the type of authentication credential used.
  #[serde(rename = "authType")]
  auth_type: String,
  /// Key handle used for Key Service requests.
  #[serde(rename = "keyHandle")]
  key_handle: String
}

impl Credentials {
  pub fn new(auth_type: String, key_handle: String) -> Credentials {
    Credentials {
      auth_type,
      key_handle
    }
  }

  pub fn set_auth_type(&mut self, auth_type: String) {
    self.auth_type = auth_type;
  }

  pub fn with_auth_type(mut self, auth_type: String) -> Credentials {
    self.auth_type = auth_type;
    self
  }

  pub fn auth_type(&self) -> &String {
    &self.auth_type
  }


  pub fn set_key_handle(&mut self, key_handle: String) {
    self.key_handle = key_handle;
  }

  pub fn with_key_handle(mut self, key_handle: String) -> Credentials {
    self.key_handle = key_handle;
    self
  }

  pub fn key_handle(&self) -> &String {
    &self.key_handle
  }


}



