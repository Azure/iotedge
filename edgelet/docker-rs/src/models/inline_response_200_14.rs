/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct InlineResponse20014 {
    #[serde(rename = "ID", skip_serializing_if = "Option::is_none")]
    ID: Option<String>,
    #[serde(rename = "Running", skip_serializing_if = "Option::is_none")]
    running: Option<bool>,
    #[serde(rename = "ExitCode", skip_serializing_if = "Option::is_none")]
    exit_code: Option<i32>,
    #[serde(rename = "ProcessConfig", skip_serializing_if = "Option::is_none")]
    process_config: Option<crate::models::ProcessConfig>,
    #[serde(rename = "OpenStdin", skip_serializing_if = "Option::is_none")]
    open_stdin: Option<bool>,
    #[serde(rename = "OpenStderr", skip_serializing_if = "Option::is_none")]
    open_stderr: Option<bool>,
    #[serde(rename = "OpenStdout", skip_serializing_if = "Option::is_none")]
    open_stdout: Option<bool>,
    #[serde(rename = "ContainerID", skip_serializing_if = "Option::is_none")]
    container_id: Option<String>,
    /// The system process ID for the exec process.
    #[serde(rename = "Pid", skip_serializing_if = "Option::is_none")]
    pid: Option<i32>,
}

impl InlineResponse20014 {
    pub fn new() -> Self {
        InlineResponse20014 {
            ID: None,
            running: None,
            exit_code: None,
            process_config: None,
            open_stdin: None,
            open_stderr: None,
            open_stdout: None,
            container_id: None,
            pid: None,
        }
    }

    pub fn set_ID(&mut self, ID: String) {
        self.ID = Some(ID);
    }

    pub fn with_ID(mut self, ID: String) -> Self {
        self.ID = Some(ID);
        self
    }

    pub fn ID(&self) -> Option<&str> {
        self.ID.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_ID(&mut self) {
        self.ID = None;
    }

    pub fn set_running(&mut self, running: bool) {
        self.running = Some(running);
    }

    pub fn with_running(mut self, running: bool) -> Self {
        self.running = Some(running);
        self
    }

    pub fn running(&self) -> Option<&bool> {
        self.running.as_ref()
    }

    pub fn reset_running(&mut self) {
        self.running = None;
    }

    pub fn set_exit_code(&mut self, exit_code: i32) {
        self.exit_code = Some(exit_code);
    }

    pub fn with_exit_code(mut self, exit_code: i32) -> Self {
        self.exit_code = Some(exit_code);
        self
    }

    pub fn exit_code(&self) -> Option<i32> {
        self.exit_code
    }

    pub fn reset_exit_code(&mut self) {
        self.exit_code = None;
    }

    pub fn set_process_config(&mut self, process_config: crate::models::ProcessConfig) {
        self.process_config = Some(process_config);
    }

    pub fn with_process_config(mut self, process_config: crate::models::ProcessConfig) -> Self {
        self.process_config = Some(process_config);
        self
    }

    pub fn process_config(&self) -> Option<&crate::models::ProcessConfig> {
        self.process_config.as_ref()
    }

    pub fn reset_process_config(&mut self) {
        self.process_config = None;
    }

    pub fn set_open_stdin(&mut self, open_stdin: bool) {
        self.open_stdin = Some(open_stdin);
    }

    pub fn with_open_stdin(mut self, open_stdin: bool) -> Self {
        self.open_stdin = Some(open_stdin);
        self
    }

    pub fn open_stdin(&self) -> Option<&bool> {
        self.open_stdin.as_ref()
    }

    pub fn reset_open_stdin(&mut self) {
        self.open_stdin = None;
    }

    pub fn set_open_stderr(&mut self, open_stderr: bool) {
        self.open_stderr = Some(open_stderr);
    }

    pub fn with_open_stderr(mut self, open_stderr: bool) -> Self {
        self.open_stderr = Some(open_stderr);
        self
    }

    pub fn open_stderr(&self) -> Option<&bool> {
        self.open_stderr.as_ref()
    }

    pub fn reset_open_stderr(&mut self) {
        self.open_stderr = None;
    }

    pub fn set_open_stdout(&mut self, open_stdout: bool) {
        self.open_stdout = Some(open_stdout);
    }

    pub fn with_open_stdout(mut self, open_stdout: bool) -> Self {
        self.open_stdout = Some(open_stdout);
        self
    }

    pub fn open_stdout(&self) -> Option<&bool> {
        self.open_stdout.as_ref()
    }

    pub fn reset_open_stdout(&mut self) {
        self.open_stdout = None;
    }

    pub fn set_container_id(&mut self, container_id: String) {
        self.container_id = Some(container_id);
    }

    pub fn with_container_id(mut self, container_id: String) -> Self {
        self.container_id = Some(container_id);
        self
    }

    pub fn container_id(&self) -> Option<&str> {
        self.container_id.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_container_id(&mut self) {
        self.container_id = None;
    }

    pub fn set_pid(&mut self, pid: i32) {
        self.pid = Some(pid);
    }

    pub fn with_pid(mut self, pid: i32) -> Self {
        self.pid = Some(pid);
        self
    }

    pub fn pid(&self) -> Option<i32> {
        self.pid
    }

    pub fn reset_pid(&mut self) {
        self.pid = None;
    }
}
