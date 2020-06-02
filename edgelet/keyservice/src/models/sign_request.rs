/* 
 * Key Service API
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
pub struct SignRequest {
  /// Key handle returned by Identity Service.
  #[serde(rename = "keyHandle")]
  key_handle: String,
  /// Sign algorithm to be used.
  #[serde(rename = "algorithm")]
  algorithm: String,
  #[serde(rename = "parameters")]
  parameters: crate::models::SignParameters
}

impl SignRequest {
  pub fn new(key_handle: String, algorithm: String, parameters: crate::models::SignParameters) -> SignRequest {
    SignRequest {
      key_handle,
      algorithm,
      parameters
    }
  }

  pub fn set_key_handle(&mut self, key_handle: String) {
    self.key_handle = key_handle;
  }

  pub fn with_key_handle(mut self, key_handle: String) -> SignRequest {
    self.key_handle = key_handle;
    self
  }

  pub fn key_handle(&self) -> &String {
    &self.key_handle
  }


  pub fn set_algorithm(&mut self, algorithm: String) {
    self.algorithm = algorithm;
  }

  pub fn with_algorithm(mut self, algorithm: String) -> SignRequest {
    self.algorithm = algorithm;
    self
  }

  pub fn algorithm(&self) -> &String {
    &self.algorithm
  }


  pub fn set_parameters(&mut self, parameters: crate::models::SignParameters) {
    self.parameters = parameters;
  }

  pub fn with_parameters(mut self, parameters: crate::models::SignParameters) -> SignRequest {
    self.parameters = parameters;
    self
  }

  pub fn parameters(&self) -> &crate::models::SignParameters {
    &self.parameters
  }


}



