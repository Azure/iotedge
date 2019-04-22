/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// TaskSpecRestartPolicy : Specification for the restart policy which applies to containers created as part of this service.
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct TaskSpecRestartPolicy {
    /// Condition for restart.
    #[serde(rename = "Condition", skip_serializing_if = "Option::is_none")]
    condition: Option<String>,
    /// Delay between restart attempts.
    #[serde(rename = "Delay", skip_serializing_if = "Option::is_none")]
    delay: Option<i64>,
    /// Maximum attempts to restart a given container before giving up (default value is 0, which is ignored).
    #[serde(rename = "MaxAttempts", skip_serializing_if = "Option::is_none")]
    max_attempts: Option<i64>,
    /// Windows is the time window used to evaluate the restart policy (default value is 0, which is unbounded).
    #[serde(rename = "Window", skip_serializing_if = "Option::is_none")]
    window: Option<i64>,
}

impl TaskSpecRestartPolicy {
    /// Specification for the restart policy which applies to containers created as part of this service.
    pub fn new() -> Self {
        TaskSpecRestartPolicy {
            condition: None,
            delay: None,
            max_attempts: None,
            window: None,
        }
    }

    pub fn set_condition(&mut self, condition: String) {
        self.condition = Some(condition);
    }

    pub fn with_condition(mut self, condition: String) -> Self {
        self.condition = Some(condition);
        self
    }

    pub fn condition(&self) -> Option<&str> {
        self.condition.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_condition(&mut self) {
        self.condition = None;
    }

    pub fn set_delay(&mut self, delay: i64) {
        self.delay = Some(delay);
    }

    pub fn with_delay(mut self, delay: i64) -> Self {
        self.delay = Some(delay);
        self
    }

    pub fn delay(&self) -> Option<i64> {
        self.delay
    }

    pub fn reset_delay(&mut self) {
        self.delay = None;
    }

    pub fn set_max_attempts(&mut self, max_attempts: i64) {
        self.max_attempts = Some(max_attempts);
    }

    pub fn with_max_attempts(mut self, max_attempts: i64) -> Self {
        self.max_attempts = Some(max_attempts);
        self
    }

    pub fn max_attempts(&self) -> Option<i64> {
        self.max_attempts
    }

    pub fn reset_max_attempts(&mut self) {
        self.max_attempts = None;
    }

    pub fn set_window(&mut self, window: i64) {
        self.window = Some(window);
    }

    pub fn with_window(mut self, window: i64) -> Self {
        self.window = Some(window);
        self
    }

    pub fn window(&self) -> Option<i64> {
        self.window
    }

    pub fn reset_window(&mut self) {
        self.window = None;
    }
}
