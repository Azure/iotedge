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

#[derive(Debug, Serialize, Deserialize)]
pub struct Status {
  #[serde(rename = "startTime")]
  start_time: Option<String>,
  #[serde(rename = "exitStatus")]
  exit_status: Option<::models::ExitStatus>,
  #[serde(rename = "runtimeStatus")]
  runtime_status: ::models::RuntimeStatus
}

impl Status {
  pub fn new(runtime_status: ::models::RuntimeStatus) -> Status {
    Status {
      start_time: None,
      exit_status: None,
      runtime_status: runtime_status
    }
  }

  pub fn set_start_time(&mut self, start_time: String) {
    self.start_time = Some(start_time);
  }

  pub fn with_start_time(mut self, start_time: String) -> Status {
    self.start_time = Some(start_time);
    self
  }

  pub fn start_time(&self) -> Option<&String> {
    self.start_time.as_ref()
  }

  pub fn reset_start_time(&mut self) {
    self.start_time = None;
  }

  pub fn set_exit_status(&mut self, exit_status: ::models::ExitStatus) {
    self.exit_status = Some(exit_status);
  }

  pub fn with_exit_status(mut self, exit_status: ::models::ExitStatus) -> Status {
    self.exit_status = Some(exit_status);
    self
  }

  pub fn exit_status(&self) -> Option<&::models::ExitStatus> {
    self.exit_status.as_ref()
  }

  pub fn reset_exit_status(&mut self) {
    self.exit_status = None;
  }

  pub fn set_runtime_status(&mut self, runtime_status: ::models::RuntimeStatus) {
    self.runtime_status = runtime_status;
  }

  pub fn with_runtime_status(mut self, runtime_status: ::models::RuntimeStatus) -> Status {
    self.runtime_status = runtime_status;
    self
  }

  pub fn runtime_status(&self) -> &::models::RuntimeStatus {
    &self.runtime_status
  }


}



