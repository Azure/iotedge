/*
 * IoT Edge Management API
 *
 * No description provided (generated by Swagger Codegen https://github.com/swagger-api/swagger-codegen)
 *
 * OpenAPI spec version: 2018-06-28
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct Status {
    #[serde(rename = "startTime", skip_serializing_if = "Option::is_none")]
    start_time: Option<String>,
    #[serde(rename = "exitStatus", skip_serializing_if = "Option::is_none")]
    exit_status: Option<crate::models::ExitStatus>,
    #[serde(rename = "runtimeStatus")]
    runtime_status: crate::models::RuntimeStatus,
}

impl Status {
    pub fn new(runtime_status: crate::models::RuntimeStatus) -> Self {
        Status {
            start_time: None,
            exit_status: None,
            runtime_status,
        }
    }

    pub fn set_start_time(&mut self, start_time: String) {
        self.start_time = Some(start_time);
    }

    pub fn with_start_time(mut self, start_time: String) -> Self {
        self.start_time = Some(start_time);
        self
    }

    pub fn start_time(&self) -> Option<&str> {
        self.start_time.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_start_time(&mut self) {
        self.start_time = None;
    }

    pub fn set_exit_status(&mut self, exit_status: crate::models::ExitStatus) {
        self.exit_status = Some(exit_status);
    }

    pub fn with_exit_status(mut self, exit_status: crate::models::ExitStatus) -> Self {
        self.exit_status = Some(exit_status);
        self
    }

    pub fn exit_status(&self) -> Option<&crate::models::ExitStatus> {
        self.exit_status.as_ref()
    }

    pub fn reset_exit_status(&mut self) {
        self.exit_status = None;
    }

    pub fn set_runtime_status(&mut self, runtime_status: crate::models::RuntimeStatus) {
        self.runtime_status = runtime_status;
    }

    pub fn with_runtime_status(mut self, runtime_status: crate::models::RuntimeStatus) -> Self {
        self.runtime_status = runtime_status;
        self
    }

    pub fn runtime_status(&self) -> &crate::models::RuntimeStatus {
        &self.runtime_status
    }
}
