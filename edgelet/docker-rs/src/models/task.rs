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
pub struct Task {
    /// The ID of the task.
    #[serde(rename = "ID", skip_serializing_if = "Option::is_none")]
    ID: Option<String>,
    #[serde(rename = "Version", skip_serializing_if = "Option::is_none")]
    version: Option<crate::models::ObjectVersion>,
    #[serde(rename = "CreatedAt", skip_serializing_if = "Option::is_none")]
    created_at: Option<String>,
    #[serde(rename = "UpdatedAt", skip_serializing_if = "Option::is_none")]
    updated_at: Option<String>,
    /// Name of the task.
    #[serde(rename = "Name", skip_serializing_if = "Option::is_none")]
    name: Option<String>,
    /// User-defined key/value metadata.
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    labels: Option<::std::collections::HashMap<String, String>>,
    #[serde(rename = "Spec", skip_serializing_if = "Option::is_none")]
    spec: Option<crate::models::TaskSpec>,
    /// The ID of the service this task is part of.
    #[serde(rename = "ServiceID", skip_serializing_if = "Option::is_none")]
    service_id: Option<String>,
    #[serde(rename = "Slot", skip_serializing_if = "Option::is_none")]
    slot: Option<i32>,
    /// The ID of the node that this task is on.
    #[serde(rename = "NodeID", skip_serializing_if = "Option::is_none")]
    node_id: Option<String>,
    #[serde(
        rename = "AssignedGenericResources",
        skip_serializing_if = "Option::is_none"
    )]
    assigned_generic_resources: Option<crate::models::GenericResources>,
    #[serde(rename = "Status", skip_serializing_if = "Option::is_none")]
    status: Option<crate::models::TaskStatus>,
    #[serde(rename = "DesiredState", skip_serializing_if = "Option::is_none")]
    desired_state: Option<crate::models::TaskState>,
}

impl Task {
    pub fn new() -> Self {
        Task {
            ID: None,
            version: None,
            created_at: None,
            updated_at: None,
            name: None,
            labels: None,
            spec: None,
            service_id: None,
            slot: None,
            node_id: None,
            assigned_generic_resources: None,
            status: None,
            desired_state: None,
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

    pub fn set_version(&mut self, version: crate::models::ObjectVersion) {
        self.version = Some(version);
    }

    pub fn with_version(mut self, version: crate::models::ObjectVersion) -> Self {
        self.version = Some(version);
        self
    }

    pub fn version(&self) -> Option<&crate::models::ObjectVersion> {
        self.version.as_ref()
    }

    pub fn reset_version(&mut self) {
        self.version = None;
    }

    pub fn set_created_at(&mut self, created_at: String) {
        self.created_at = Some(created_at);
    }

    pub fn with_created_at(mut self, created_at: String) -> Self {
        self.created_at = Some(created_at);
        self
    }

    pub fn created_at(&self) -> Option<&str> {
        self.created_at.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_created_at(&mut self) {
        self.created_at = None;
    }

    pub fn set_updated_at(&mut self, updated_at: String) {
        self.updated_at = Some(updated_at);
    }

    pub fn with_updated_at(mut self, updated_at: String) -> Self {
        self.updated_at = Some(updated_at);
        self
    }

    pub fn updated_at(&self) -> Option<&str> {
        self.updated_at.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_updated_at(&mut self) {
        self.updated_at = None;
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

    pub fn set_spec(&mut self, spec: crate::models::TaskSpec) {
        self.spec = Some(spec);
    }

    pub fn with_spec(mut self, spec: crate::models::TaskSpec) -> Self {
        self.spec = Some(spec);
        self
    }

    pub fn spec(&self) -> Option<&crate::models::TaskSpec> {
        self.spec.as_ref()
    }

    pub fn reset_spec(&mut self) {
        self.spec = None;
    }

    pub fn set_service_id(&mut self, service_id: String) {
        self.service_id = Some(service_id);
    }

    pub fn with_service_id(mut self, service_id: String) -> Self {
        self.service_id = Some(service_id);
        self
    }

    pub fn service_id(&self) -> Option<&str> {
        self.service_id.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_service_id(&mut self) {
        self.service_id = None;
    }

    pub fn set_slot(&mut self, slot: i32) {
        self.slot = Some(slot);
    }

    pub fn with_slot(mut self, slot: i32) -> Self {
        self.slot = Some(slot);
        self
    }

    pub fn slot(&self) -> Option<i32> {
        self.slot
    }

    pub fn reset_slot(&mut self) {
        self.slot = None;
    }

    pub fn set_node_id(&mut self, node_id: String) {
        self.node_id = Some(node_id);
    }

    pub fn with_node_id(mut self, node_id: String) -> Self {
        self.node_id = Some(node_id);
        self
    }

    pub fn node_id(&self) -> Option<&str> {
        self.node_id.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_node_id(&mut self) {
        self.node_id = None;
    }

    pub fn set_assigned_generic_resources(
        &mut self,
        assigned_generic_resources: crate::models::GenericResources,
    ) {
        self.assigned_generic_resources = Some(assigned_generic_resources);
    }

    pub fn with_assigned_generic_resources(
        mut self,
        assigned_generic_resources: crate::models::GenericResources,
    ) -> Self {
        self.assigned_generic_resources = Some(assigned_generic_resources);
        self
    }

    pub fn assigned_generic_resources(&self) -> Option<&crate::models::GenericResources> {
        self.assigned_generic_resources.as_ref()
    }

    pub fn reset_assigned_generic_resources(&mut self) {
        self.assigned_generic_resources = None;
    }

    pub fn set_status(&mut self, status: crate::models::TaskStatus) {
        self.status = Some(status);
    }

    pub fn with_status(mut self, status: crate::models::TaskStatus) -> Self {
        self.status = Some(status);
        self
    }

    pub fn status(&self) -> Option<&crate::models::TaskStatus> {
        self.status.as_ref()
    }

    pub fn reset_status(&mut self) {
        self.status = None;
    }

    pub fn set_desired_state(&mut self, desired_state: crate::models::TaskState) {
        self.desired_state = Some(desired_state);
    }

    pub fn with_desired_state(mut self, desired_state: crate::models::TaskState) -> Self {
        self.desired_state = Some(desired_state);
        self
    }

    pub fn desired_state(&self) -> Option<&crate::models::TaskState> {
        self.desired_state.as_ref()
    }

    pub fn reset_desired_state(&mut self) {
        self.desired_state = None;
    }
}
