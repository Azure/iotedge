/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// SwarmInfo : Represents generic information about swarm.
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct SwarmInfo {
    /// Unique identifier of for this node in the swarm.
    #[serde(rename = "NodeID", skip_serializing_if = "Option::is_none")]
    node_id: Option<String>,
    /// IP address at which this node can be reached by other nodes in the swarm.
    #[serde(rename = "NodeAddr", skip_serializing_if = "Option::is_none")]
    node_addr: Option<String>,
    //TODO: This change was due to SwarmInfo, Local Node Stat returning String, instead of STATE. So the Swagger is not matching with api.
    //If Auto generate tool is run again, make sure this is working.
    #[serde(rename = "LocalNodeState", skip_serializing_if = "Option::is_none")]
    local_node_state: Option<String>,
    #[serde(rename = "ControlAvailable", skip_serializing_if = "Option::is_none")]
    control_available: Option<bool>,
    #[serde(rename = "Error", skip_serializing_if = "Option::is_none")]
    error: Option<String>,
    /// List of ID's and addresses of other managers in the swarm.
    #[serde(rename = "RemoteManagers", skip_serializing_if = "Option::is_none")]
    remote_managers: Option<Vec<crate::models::PeerNode>>,
    /// Total number of nodes in the swarm.
    #[serde(rename = "Nodes", skip_serializing_if = "Option::is_none")]
    nodes: Option<i32>,
    /// Total number of managers in the swarm.
    #[serde(rename = "Managers", skip_serializing_if = "Option::is_none")]
    managers: Option<i32>,
    #[serde(rename = "Cluster", skip_serializing_if = "Option::is_none")]
    cluster: Option<crate::models::ClusterInfo>,
}

impl SwarmInfo {
    /// Represents generic information about swarm.
    pub fn new() -> Self {
        SwarmInfo {
            node_id: None,
            node_addr: None,
            local_node_state: None,
            control_available: None,
            error: None,
            remote_managers: None,
            nodes: None,
            managers: None,
            cluster: None,
        }
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

    pub fn set_node_addr(&mut self, node_addr: String) {
        self.node_addr = Some(node_addr);
    }

    pub fn with_node_addr(mut self, node_addr: String) -> Self {
        self.node_addr = Some(node_addr);
        self
    }

    pub fn node_addr(&self) -> Option<&str> {
        self.node_addr.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_node_addr(&mut self) {
        self.node_addr = None;
    }

    //TODO: This change was due to SwarmInfo, Local Node Stat returning String, instead of STATE. So the Swagger is not matching with api.
    //If Auto generate tool is run again, make sure this is working.
    pub fn set_local_node_state(&mut self, local_node_state: String) {
        self.local_node_state = Some(local_node_state);
    }

    pub fn with_local_node_state(
        mut self,
        //TODO: This change was due to SwarmInfo, Local Node Stat returning String, instead of STATE. So the Swagger is not matching with api.
        //If Auto generate tool is run again, make sure this is working.
        local_node_state: String,
    ) -> Self {
        self.local_node_state = Some(local_node_state);
        self
    }

    //TODO: This change was due to SwarmInfo, Local Node Stat returning String, instead of STATE. So the Swagger is not matching with api.
    //If Auto generate tool is run again, make sure this is working.
    pub fn local_node_state(&self) -> Option<&str> {
        self.local_node_state.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_local_node_state(&mut self) {
        self.local_node_state = None;
    }

    pub fn set_control_available(&mut self, control_available: bool) {
        self.control_available = Some(control_available);
    }

    pub fn with_control_available(mut self, control_available: bool) -> Self {
        self.control_available = Some(control_available);
        self
    }

    pub fn control_available(&self) -> Option<&bool> {
        self.control_available.as_ref()
    }

    pub fn reset_control_available(&mut self) {
        self.control_available = None;
    }

    pub fn set_error(&mut self, error: String) {
        self.error = Some(error);
    }

    pub fn with_error(mut self, error: String) -> Self {
        self.error = Some(error);
        self
    }

    pub fn error(&self) -> Option<&str> {
        self.error.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_error(&mut self) {
        self.error = None;
    }

    pub fn set_remote_managers(&mut self, remote_managers: Vec<crate::models::PeerNode>) {
        self.remote_managers = Some(remote_managers);
    }

    pub fn with_remote_managers(mut self, remote_managers: Vec<crate::models::PeerNode>) -> Self {
        self.remote_managers = Some(remote_managers);
        self
    }

    pub fn remote_managers(&self) -> Option<&[crate::models::PeerNode]> {
        self.remote_managers.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_remote_managers(&mut self) {
        self.remote_managers = None;
    }

    pub fn set_nodes(&mut self, nodes: i32) {
        self.nodes = Some(nodes);
    }

    pub fn with_nodes(mut self, nodes: i32) -> Self {
        self.nodes = Some(nodes);
        self
    }

    pub fn nodes(&self) -> Option<i32> {
        self.nodes
    }

    pub fn reset_nodes(&mut self) {
        self.nodes = None;
    }

    pub fn set_managers(&mut self, managers: i32) {
        self.managers = Some(managers);
    }

    pub fn with_managers(mut self, managers: i32) -> Self {
        self.managers = Some(managers);
        self
    }

    pub fn managers(&self) -> Option<i32> {
        self.managers
    }

    pub fn reset_managers(&mut self) {
        self.managers = None;
    }

    pub fn set_cluster(&mut self, cluster: crate::models::ClusterInfo) {
        self.cluster = Some(cluster);
    }

    pub fn with_cluster(mut self, cluster: crate::models::ClusterInfo) -> Self {
        self.cluster = Some(cluster);
        self
    }

    pub fn cluster(&self) -> Option<&crate::models::ClusterInfo> {
        self.cluster.as_ref()
    }

    pub fn reset_cluster(&mut self) {
        self.cluster = None;
    }
}
