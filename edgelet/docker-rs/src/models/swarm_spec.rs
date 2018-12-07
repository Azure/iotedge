/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// SwarmSpec : User modifiable swarm configuration.

#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct SwarmSpec {
    /// Name of the swarm.
    #[serde(rename = "Name", skip_serializing_if = "Option::is_none")]
    name: Option<String>,
    /// User-defined key/value metadata.
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    labels: Option<::std::collections::HashMap<String, String>>,
    #[serde(rename = "Orchestration", skip_serializing_if = "Option::is_none")]
    orchestration: Option<::models::SwarmSpecOrchestration>,
    #[serde(rename = "Raft", skip_serializing_if = "Option::is_none")]
    raft: Option<::models::SwarmSpecRaft>,
    #[serde(rename = "Dispatcher", skip_serializing_if = "Option::is_none")]
    dispatcher: Option<::models::SwarmSpecDispatcher>,
    #[serde(rename = "CAConfig", skip_serializing_if = "Option::is_none")]
    ca_config: Option<::models::SwarmSpecCaConfig>,
    #[serde(rename = "EncryptionConfig", skip_serializing_if = "Option::is_none")]
    encryption_config: Option<::models::SwarmSpecEncryptionConfig>,
    #[serde(rename = "TaskDefaults", skip_serializing_if = "Option::is_none")]
    task_defaults: Option<::models::SwarmSpecTaskDefaults>,
}

impl SwarmSpec {
    /// User modifiable swarm configuration.
    pub fn new() -> Self {
        SwarmSpec {
            name: None,
            labels: None,
            orchestration: None,
            raft: None,
            dispatcher: None,
            ca_config: None,
            encryption_config: None,
            task_defaults: None,
        }
    }

    pub fn set_name(&mut self, name: String) {
        self.name = Some(name);
    }

    pub fn with_name(mut self, name: String) -> Self {
        self.name = Some(name);
        self
    }

    pub fn name(&self) -> Option<&str> {
        self.name.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_name(&mut self) {
        self.name = None;
    }

    pub fn set_labels(&mut self, labels: ::std::collections::HashMap<String, String>) {
        self.labels = Some(labels);
    }

    pub fn with_labels(mut self, labels: ::std::collections::HashMap<String, String>) -> Self {
        self.labels = Some(labels);
        self
    }

    pub fn labels(&self) -> Option<&::std::collections::HashMap<String, String>> {
        self.labels.as_ref()
    }

    pub fn reset_labels(&mut self) {
        self.labels = None;
    }

    pub fn set_orchestration(&mut self, orchestration: ::models::SwarmSpecOrchestration) {
        self.orchestration = Some(orchestration);
    }

    pub fn with_orchestration(mut self, orchestration: ::models::SwarmSpecOrchestration) -> Self {
        self.orchestration = Some(orchestration);
        self
    }

    pub fn orchestration(&self) -> Option<&::models::SwarmSpecOrchestration> {
        self.orchestration.as_ref()
    }

    pub fn reset_orchestration(&mut self) {
        self.orchestration = None;
    }

    pub fn set_raft(&mut self, raft: ::models::SwarmSpecRaft) {
        self.raft = Some(raft);
    }

    pub fn with_raft(mut self, raft: ::models::SwarmSpecRaft) -> Self {
        self.raft = Some(raft);
        self
    }

    pub fn raft(&self) -> Option<&::models::SwarmSpecRaft> {
        self.raft.as_ref()
    }

    pub fn reset_raft(&mut self) {
        self.raft = None;
    }

    pub fn set_dispatcher(&mut self, dispatcher: ::models::SwarmSpecDispatcher) {
        self.dispatcher = Some(dispatcher);
    }

    pub fn with_dispatcher(mut self, dispatcher: ::models::SwarmSpecDispatcher) -> Self {
        self.dispatcher = Some(dispatcher);
        self
    }

    pub fn dispatcher(&self) -> Option<&::models::SwarmSpecDispatcher> {
        self.dispatcher.as_ref()
    }

    pub fn reset_dispatcher(&mut self) {
        self.dispatcher = None;
    }

    pub fn set_ca_config(&mut self, ca_config: ::models::SwarmSpecCaConfig) {
        self.ca_config = Some(ca_config);
    }

    pub fn with_ca_config(mut self, ca_config: ::models::SwarmSpecCaConfig) -> Self {
        self.ca_config = Some(ca_config);
        self
    }

    pub fn ca_config(&self) -> Option<&::models::SwarmSpecCaConfig> {
        self.ca_config.as_ref()
    }

    pub fn reset_ca_config(&mut self) {
        self.ca_config = None;
    }

    pub fn set_encryption_config(
        &mut self,
        encryption_config: ::models::SwarmSpecEncryptionConfig,
    ) {
        self.encryption_config = Some(encryption_config);
    }

    pub fn with_encryption_config(
        mut self,
        encryption_config: ::models::SwarmSpecEncryptionConfig,
    ) -> Self {
        self.encryption_config = Some(encryption_config);
        self
    }

    pub fn encryption_config(&self) -> Option<&::models::SwarmSpecEncryptionConfig> {
        self.encryption_config.as_ref()
    }

    pub fn reset_encryption_config(&mut self) {
        self.encryption_config = None;
    }

    pub fn set_task_defaults(&mut self, task_defaults: ::models::SwarmSpecTaskDefaults) {
        self.task_defaults = Some(task_defaults);
    }

    pub fn with_task_defaults(mut self, task_defaults: ::models::SwarmSpecTaskDefaults) -> Self {
        self.task_defaults = Some(task_defaults);
        self
    }

    pub fn task_defaults(&self) -> Option<&::models::SwarmSpecTaskDefaults> {
        self.task_defaults.as_ref()
    }

    pub fn reset_task_defaults(&mut self) {
        self.task_defaults = None;
    }
}
