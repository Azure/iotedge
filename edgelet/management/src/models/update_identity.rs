/*
 * IoT Edge Module Management API
 *
 * No description provided (generated by Swagger Codegen https://github.com/swagger-api/swagger-codegen)
 *
 * OpenAPI spec version: 2018-06-28
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct UpdateIdentity {
    #[serde(rename = "generationId")]
    generation_id: String,
    #[serde(rename = "managedBy", skip_serializing_if = "Option::is_none")]
    managed_by: Option<String>,
}

impl UpdateIdentity {
    pub fn new(generation_id: String) -> Self {
        UpdateIdentity {
            generation_id,
            managed_by: None,
        }
    }

    pub fn set_generation_id(&mut self, generation_id: String) {
        self.generation_id = generation_id;
    }

    pub fn with_generation_id(mut self, generation_id: String) -> Self {
        self.generation_id = generation_id;
        self
    }

    pub fn generation_id(&self) -> &String {
        &self.generation_id
    }

    pub fn set_managed_by(&mut self, managed_by: String) {
        self.managed_by = Some(managed_by);
    }

    pub fn with_managed_by(mut self, managed_by: String) -> Self {
        self.managed_by = Some(managed_by);
        self
    }

    pub fn managed_by(&self) -> Option<&str> {
        self.managed_by.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_managed_by(&mut self) {
        self.managed_by = None;
    }
}
