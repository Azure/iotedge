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
pub struct ModuleDetails {
  /// System generated unique identitier.
  #[serde(rename = "id")]
  id: String,
  /// The name of the module.
  #[serde(rename = "name")]
  name: String,
  /// The type of a module.
  #[serde(rename = "type")]
  type_: String,
  #[serde(rename = "config")]
  config: ::models::Config,
  #[serde(rename = "status")]
  status: ::models::Status
}

impl ModuleDetails {
  pub fn new(id: String, name: String, type_: String, config: ::models::Config, status: ::models::Status) -> ModuleDetails {
    ModuleDetails {
      id: id,
      name: name,
      type_: type_,
      config: config,
      status: status
    }
  }

  pub fn set_id(&mut self, id: String) {
    self.id = id;
  }

  pub fn with_id(mut self, id: String) -> ModuleDetails {
    self.id = id;
    self
  }

  pub fn id(&self) -> &String {
    &self.id
  }


  pub fn set_name(&mut self, name: String) {
    self.name = name;
  }

  pub fn with_name(mut self, name: String) -> ModuleDetails {
    self.name = name;
    self
  }

  pub fn name(&self) -> &String {
    &self.name
  }


  pub fn set_type_(&mut self, type_: String) {
    self.type_ = type_;
  }

  pub fn with_type_(mut self, type_: String) -> ModuleDetails {
    self.type_ = type_;
    self
  }

  pub fn type_(&self) -> &String {
    &self.type_
  }


  pub fn set_config(&mut self, config: ::models::Config) {
    self.config = config;
  }

  pub fn with_config(mut self, config: ::models::Config) -> ModuleDetails {
    self.config = config;
    self
  }

  pub fn config(&self) -> &::models::Config {
    &self.config
  }


  pub fn set_status(&mut self, status: ::models::Status) {
    self.status = status;
  }

  pub fn with_status(mut self, status: ::models::Status) -> ModuleDetails {
    self.status = status;
    self
  }

  pub fn status(&self) -> &::models::Status {
    &self.status
  }


}



