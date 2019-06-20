/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// ServiceSpec : User modifiable configuration for a service.
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct ServiceSpec {
    /// Name of the service.
    #[serde(rename = "Name", skip_serializing_if = "Option::is_none")]
    name: Option<String>,
    /// User-defined key/value metadata.
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    labels: Option<::std::collections::HashMap<String, String>>,
    #[serde(rename = "TaskTemplate", skip_serializing_if = "Option::is_none")]
    task_template: Option<crate::models::TaskSpec>,
    #[serde(rename = "Mode", skip_serializing_if = "Option::is_none")]
    mode: Option<crate::models::ServiceSpecMode>,
    #[serde(rename = "UpdateConfig", skip_serializing_if = "Option::is_none")]
    update_config: Option<crate::models::ServiceSpecUpdateConfig>,
    #[serde(rename = "RollbackConfig", skip_serializing_if = "Option::is_none")]
    rollback_config: Option<crate::models::ServiceSpecRollbackConfig>,
    /// Array of network names or IDs to attach the service to.
    #[serde(rename = "Networks", skip_serializing_if = "Option::is_none")]
    networks: Option<Vec<crate::models::TaskSpecNetworks>>,
    #[serde(rename = "EndpointSpec", skip_serializing_if = "Option::is_none")]
    endpoint_spec: Option<crate::models::EndpointSpec>,
}

impl ServiceSpec {
    /// User modifiable configuration for a service.
    pub fn new() -> Self {
        ServiceSpec {
            name: None,
            labels: None,
            task_template: None,
            mode: None,
            update_config: None,
            rollback_config: None,
            networks: None,
            endpoint_spec: None,
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

    pub fn set_task_template(&mut self, task_template: crate::models::TaskSpec) {
        self.task_template = Some(task_template);
    }

    pub fn with_task_template(mut self, task_template: crate::models::TaskSpec) -> Self {
        self.task_template = Some(task_template);
        self
    }

    pub fn task_template(&self) -> Option<&crate::models::TaskSpec> {
        self.task_template.as_ref()
    }

    pub fn reset_task_template(&mut self) {
        self.task_template = None;
    }

    pub fn set_mode(&mut self, mode: crate::models::ServiceSpecMode) {
        self.mode = Some(mode);
    }

    pub fn with_mode(mut self, mode: crate::models::ServiceSpecMode) -> Self {
        self.mode = Some(mode);
        self
    }

    pub fn mode(&self) -> Option<&crate::models::ServiceSpecMode> {
        self.mode.as_ref()
    }

    pub fn reset_mode(&mut self) {
        self.mode = None;
    }

    pub fn set_update_config(&mut self, update_config: crate::models::ServiceSpecUpdateConfig) {
        self.update_config = Some(update_config);
    }

    pub fn with_update_config(
        mut self,
        update_config: crate::models::ServiceSpecUpdateConfig,
    ) -> Self {
        self.update_config = Some(update_config);
        self
    }

    pub fn update_config(&self) -> Option<&crate::models::ServiceSpecUpdateConfig> {
        self.update_config.as_ref()
    }

    pub fn reset_update_config(&mut self) {
        self.update_config = None;
    }

    pub fn set_rollback_config(
        &mut self,
        rollback_config: crate::models::ServiceSpecRollbackConfig,
    ) {
        self.rollback_config = Some(rollback_config);
    }

    pub fn with_rollback_config(
        mut self,
        rollback_config: crate::models::ServiceSpecRollbackConfig,
    ) -> Self {
        self.rollback_config = Some(rollback_config);
        self
    }

    pub fn rollback_config(&self) -> Option<&crate::models::ServiceSpecRollbackConfig> {
        self.rollback_config.as_ref()
    }

    pub fn reset_rollback_config(&mut self) {
        self.rollback_config = None;
    }

    pub fn set_networks(&mut self, networks: Vec<crate::models::TaskSpecNetworks>) {
        self.networks = Some(networks);
    }

    pub fn with_networks(mut self, networks: Vec<crate::models::TaskSpecNetworks>) -> Self {
        self.networks = Some(networks);
        self
    }

    pub fn networks(&self) -> Option<&[crate::models::TaskSpecNetworks]> {
        self.networks.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_networks(&mut self) {
        self.networks = None;
    }

    pub fn set_endpoint_spec(&mut self, endpoint_spec: crate::models::EndpointSpec) {
        self.endpoint_spec = Some(endpoint_spec);
    }

    pub fn with_endpoint_spec(mut self, endpoint_spec: crate::models::EndpointSpec) -> Self {
        self.endpoint_spec = Some(endpoint_spec);
        self
    }

    pub fn endpoint_spec(&self) -> Option<&crate::models::EndpointSpec> {
        self.endpoint_spec.as_ref()
    }

    pub fn reset_endpoint_spec(&mut self) {
        self.endpoint_spec = None;
    }
}
