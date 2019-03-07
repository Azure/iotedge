/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// TaskSpec : User modifiable task configuration.
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct TaskSpec {
    #[serde(rename = "PluginSpec", skip_serializing_if = "Option::is_none")]
    plugin_spec: Option<crate::models::TaskSpecPluginSpec>,
    #[serde(rename = "ContainerSpec", skip_serializing_if = "Option::is_none")]
    container_spec: Option<crate::models::TaskSpecContainerSpec>,
    #[serde(rename = "Resources", skip_serializing_if = "Option::is_none")]
    resources: Option<crate::models::TaskSpecResources>,
    #[serde(rename = "RestartPolicy", skip_serializing_if = "Option::is_none")]
    restart_policy: Option<crate::models::TaskSpecRestartPolicy>,
    #[serde(rename = "Placement", skip_serializing_if = "Option::is_none")]
    placement: Option<crate::models::TaskSpecPlacement>,
    /// A counter that triggers an update even if no relevant parameters have been changed.
    #[serde(rename = "ForceUpdate", skip_serializing_if = "Option::is_none")]
    force_update: Option<i32>,
    /// Runtime is the type of runtime specified for the task executor.
    #[serde(rename = "Runtime", skip_serializing_if = "Option::is_none")]
    runtime: Option<String>,
    #[serde(rename = "Networks", skip_serializing_if = "Option::is_none")]
    networks: Option<Vec<crate::models::TaskSpecNetworks>>,
    #[serde(rename = "LogDriver", skip_serializing_if = "Option::is_none")]
    log_driver: Option<crate::models::TaskSpecLogDriver>,
}

impl TaskSpec {
    /// User modifiable task configuration.
    pub fn new() -> Self {
        TaskSpec {
            plugin_spec: None,
            container_spec: None,
            resources: None,
            restart_policy: None,
            placement: None,
            force_update: None,
            runtime: None,
            networks: None,
            log_driver: None,
        }
    }

    pub fn set_plugin_spec(&mut self, plugin_spec: crate::models::TaskSpecPluginSpec) {
        self.plugin_spec = Some(plugin_spec);
    }

    pub fn with_plugin_spec(mut self, plugin_spec: crate::models::TaskSpecPluginSpec) -> Self {
        self.plugin_spec = Some(plugin_spec);
        self
    }

    pub fn plugin_spec(&self) -> Option<&crate::models::TaskSpecPluginSpec> {
        self.plugin_spec.as_ref()
    }

    pub fn reset_plugin_spec(&mut self) {
        self.plugin_spec = None;
    }

    pub fn set_container_spec(&mut self, container_spec: crate::models::TaskSpecContainerSpec) {
        self.container_spec = Some(container_spec);
    }

    pub fn with_container_spec(
        mut self,
        container_spec: crate::models::TaskSpecContainerSpec,
    ) -> Self {
        self.container_spec = Some(container_spec);
        self
    }

    pub fn container_spec(&self) -> Option<&crate::models::TaskSpecContainerSpec> {
        self.container_spec.as_ref()
    }

    pub fn reset_container_spec(&mut self) {
        self.container_spec = None;
    }

    pub fn set_resources(&mut self, resources: crate::models::TaskSpecResources) {
        self.resources = Some(resources);
    }

    pub fn with_resources(mut self, resources: crate::models::TaskSpecResources) -> Self {
        self.resources = Some(resources);
        self
    }

    pub fn resources(&self) -> Option<&crate::models::TaskSpecResources> {
        self.resources.as_ref()
    }

    pub fn reset_resources(&mut self) {
        self.resources = None;
    }

    pub fn set_restart_policy(&mut self, restart_policy: crate::models::TaskSpecRestartPolicy) {
        self.restart_policy = Some(restart_policy);
    }

    pub fn with_restart_policy(
        mut self,
        restart_policy: crate::models::TaskSpecRestartPolicy,
    ) -> Self {
        self.restart_policy = Some(restart_policy);
        self
    }

    pub fn restart_policy(&self) -> Option<&crate::models::TaskSpecRestartPolicy> {
        self.restart_policy.as_ref()
    }

    pub fn reset_restart_policy(&mut self) {
        self.restart_policy = None;
    }

    pub fn set_placement(&mut self, placement: crate::models::TaskSpecPlacement) {
        self.placement = Some(placement);
    }

    pub fn with_placement(mut self, placement: crate::models::TaskSpecPlacement) -> Self {
        self.placement = Some(placement);
        self
    }

    pub fn placement(&self) -> Option<&crate::models::TaskSpecPlacement> {
        self.placement.as_ref()
    }

    pub fn reset_placement(&mut self) {
        self.placement = None;
    }

    pub fn set_force_update(&mut self, force_update: i32) {
        self.force_update = Some(force_update);
    }

    pub fn with_force_update(mut self, force_update: i32) -> Self {
        self.force_update = Some(force_update);
        self
    }

    pub fn force_update(&self) -> Option<i32> {
        self.force_update
    }

    pub fn reset_force_update(&mut self) {
        self.force_update = None;
    }

    pub fn set_runtime(&mut self, runtime: String) {
        self.runtime = Some(runtime);
    }

    pub fn with_runtime(mut self, runtime: String) -> Self {
        self.runtime = Some(runtime);
        self
    }

    pub fn runtime(&self) -> Option<&str> {
        self.runtime.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_runtime(&mut self) {
        self.runtime = None;
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

    pub fn set_log_driver(&mut self, log_driver: crate::models::TaskSpecLogDriver) {
        self.log_driver = Some(log_driver);
    }

    pub fn with_log_driver(mut self, log_driver: crate::models::TaskSpecLogDriver) -> Self {
        self.log_driver = Some(log_driver);
        self
    }

    pub fn log_driver(&self) -> Option<&crate::models::TaskSpecLogDriver> {
        self.log_driver.as_ref()
    }

    pub fn reset_log_driver(&mut self) {
        self.log_driver = None;
    }
}
