/* 
 * IoT Edge Management API
 *
 * No description provided (generated by Swagger Codegen https://github.com/swagger-api/swagger-codegen)
 *
 * OpenAPI spec version: 2020-07-22
 * 
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */


#[allow(unused_imports)]
use serde_json::Value;

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UpdateIdentity {
  #[serde(rename = "generationId")]
  generation_id: String,
  #[serde(rename = "managedBy")]
  managed_by: Option<String>
}

impl UpdateIdentity {
  pub fn new(generation_id: String) -> UpdateIdentity {
    UpdateIdentity {
      generation_id: generation_id,
      managed_by: None
    }
  }

  pub fn set_generation_id(&mut self, generation_id: String) {
    self.generation_id = generation_id;
  }

  pub fn with_generation_id(mut self, generation_id: String) -> UpdateIdentity {
    self.generation_id = generation_id;
    self
  }

  pub fn generation_id(&self) -> &String {
    &self.generation_id
  }


  pub fn set_managed_by(&mut self, managed_by: String) {
    self.managed_by = Some(managed_by);
  }

  pub fn with_managed_by(mut self, managed_by: String) -> UpdateIdentity {
    self.managed_by = Some(managed_by);
    self
  }

  pub fn managed_by(&self) -> Option<&String> {
    self.managed_by.as_ref()
  }

  pub fn reset_managed_by(&mut self) {
    self.managed_by = None;
  }

}



