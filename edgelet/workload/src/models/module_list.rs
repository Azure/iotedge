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
pub struct ModuleList {
  #[serde(rename = "modules")]
  modules: Vec<::models::ModuleDetails>
}

impl ModuleList {
  pub fn new(modules: Vec<::models::ModuleDetails>) -> ModuleList {
    ModuleList {
      modules: modules
    }
  }

  pub fn set_modules(&mut self, modules: Vec<::models::ModuleDetails>) {
    self.modules = modules;
  }

  pub fn with_modules(mut self, modules: Vec<::models::ModuleDetails>) -> ModuleList {
    self.modules = modules;
    self
  }

  pub fn modules(&self) -> &Vec<::models::ModuleDetails> {
    &self.modules
  }


}



