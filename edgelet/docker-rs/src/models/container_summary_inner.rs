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
pub struct ContainerSummaryInner {
    /// The ID of this container
    #[serde(rename = "Id", skip_serializing_if = "Option::is_none")]
    id: Option<String>,
    /// The names that this container has been given
    #[serde(rename = "Names", skip_serializing_if = "Option::is_none")]
    names: Option<Vec<String>>,
    /// The name of the image used when creating this container
    #[serde(rename = "Image", skip_serializing_if = "Option::is_none")]
    image: Option<String>,
    /// The ID of the image that this container was created from
    #[serde(rename = "ImageID", skip_serializing_if = "Option::is_none")]
    image_id: Option<String>,
    /// Command to run when starting the container
    #[serde(rename = "Command", skip_serializing_if = "Option::is_none")]
    command: Option<String>,
    /// When the container was created
    #[serde(rename = "Created", skip_serializing_if = "Option::is_none")]
    created: Option<i64>,
    /// The ports exposed by this container
    #[serde(rename = "Ports", skip_serializing_if = "Option::is_none")]
    ports: Option<Vec<crate::models::Port>>,
    /// The size of files that have been created or changed by this container
    #[serde(rename = "SizeRw", skip_serializing_if = "Option::is_none")]
    size_rw: Option<i64>,
    /// The total size of all the files in this container
    #[serde(rename = "SizeRootFs", skip_serializing_if = "Option::is_none")]
    size_root_fs: Option<i64>,
    /// User-defined key/value metadata.
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    labels: Option<::std::collections::HashMap<String, String>>,
    /// The state of this container (e.g. `Exited`)
    #[serde(rename = "State", skip_serializing_if = "Option::is_none")]
    state: Option<String>,
    /// Additional human-readable status of this container (e.g. `Exit 0`)
    #[serde(rename = "Status", skip_serializing_if = "Option::is_none")]
    status: Option<String>,
    #[serde(rename = "HostConfig", skip_serializing_if = "Option::is_none")]
    host_config: Option<crate::models::ContainerSummaryInnerHostConfig>,
    #[serde(rename = "NetworkSettings", skip_serializing_if = "Option::is_none")]
    network_settings: Option<crate::models::ContainerSummaryInnerNetworkSettings>,
    #[serde(rename = "Mounts", skip_serializing_if = "Option::is_none")]
    mounts: Option<Vec<crate::models::Mount>>,
}

impl ContainerSummaryInner {
    pub fn new() -> Self {
        ContainerSummaryInner {
            id: None,
            names: None,
            image: None,
            image_id: None,
            command: None,
            created: None,
            ports: None,
            size_rw: None,
            size_root_fs: None,
            labels: None,
            state: None,
            status: None,
            host_config: None,
            network_settings: None,
            mounts: None,
        }
    }

    pub fn set_id(&mut self, id: String) {
        self.id = Some(id);
    }

    pub fn with_id(mut self, id: String) -> Self {
        self.id = Some(id);
        self
    }

    pub fn id(&self) -> Option<&str> {
        self.id.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_id(&mut self) {
        self.id = None;
    }

    pub fn set_names(&mut self, names: Vec<String>) {
        self.names = Some(names);
    }

    pub fn with_names(mut self, names: Vec<String>) -> Self {
        self.names = Some(names);
        self
    }

    pub fn names(&self) -> Option<&[String]> {
        self.names.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_names(&mut self) {
        self.names = None;
    }

    pub fn set_image(&mut self, image: String) {
        self.image = Some(image);
    }

    pub fn with_image(mut self, image: String) -> Self {
        self.image = Some(image);
        self
    }

    pub fn image(&self) -> Option<&str> {
        self.image.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_image(&mut self) {
        self.image = None;
    }

    pub fn set_image_id(&mut self, image_id: String) {
        self.image_id = Some(image_id);
    }

    pub fn with_image_id(mut self, image_id: String) -> Self {
        self.image_id = Some(image_id);
        self
    }

    pub fn image_id(&self) -> Option<&str> {
        self.image_id.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_image_id(&mut self) {
        self.image_id = None;
    }

    pub fn set_command(&mut self, command: String) {
        self.command = Some(command);
    }

    pub fn with_command(mut self, command: String) -> Self {
        self.command = Some(command);
        self
    }

    pub fn command(&self) -> Option<&str> {
        self.command.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_command(&mut self) {
        self.command = None;
    }

    pub fn set_created(&mut self, created: i64) {
        self.created = Some(created);
    }

    pub fn with_created(mut self, created: i64) -> Self {
        self.created = Some(created);
        self
    }

    pub fn created(&self) -> Option<i64> {
        self.created
    }

    pub fn reset_created(&mut self) {
        self.created = None;
    }

    pub fn set_ports(&mut self, ports: Vec<crate::models::Port>) {
        self.ports = Some(ports);
    }

    pub fn with_ports(mut self, ports: Vec<crate::models::Port>) -> Self {
        self.ports = Some(ports);
        self
    }

    pub fn ports(&self) -> Option<&[crate::models::Port]> {
        self.ports.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_ports(&mut self) {
        self.ports = None;
    }

    pub fn set_size_rw(&mut self, size_rw: i64) {
        self.size_rw = Some(size_rw);
    }

    pub fn with_size_rw(mut self, size_rw: i64) -> Self {
        self.size_rw = Some(size_rw);
        self
    }

    pub fn size_rw(&self) -> Option<i64> {
        self.size_rw
    }

    pub fn reset_size_rw(&mut self) {
        self.size_rw = None;
    }

    pub fn set_size_root_fs(&mut self, size_root_fs: i64) {
        self.size_root_fs = Some(size_root_fs);
    }

    pub fn with_size_root_fs(mut self, size_root_fs: i64) -> Self {
        self.size_root_fs = Some(size_root_fs);
        self
    }

    pub fn size_root_fs(&self) -> Option<i64> {
        self.size_root_fs
    }

    pub fn reset_size_root_fs(&mut self) {
        self.size_root_fs = None;
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

    pub fn set_state(&mut self, state: String) {
        self.state = Some(state);
    }

    pub fn with_state(mut self, state: String) -> Self {
        self.state = Some(state);
        self
    }

    pub fn state(&self) -> Option<&str> {
        self.state.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_state(&mut self) {
        self.state = None;
    }

    pub fn set_status(&mut self, status: String) {
        self.status = Some(status);
    }

    pub fn with_status(mut self, status: String) -> Self {
        self.status = Some(status);
        self
    }

    pub fn status(&self) -> Option<&str> {
        self.status.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_status(&mut self) {
        self.status = None;
    }

    pub fn set_host_config(&mut self, host_config: crate::models::ContainerSummaryInnerHostConfig) {
        self.host_config = Some(host_config);
    }

    pub fn with_host_config(
        mut self,
        host_config: crate::models::ContainerSummaryInnerHostConfig,
    ) -> Self {
        self.host_config = Some(host_config);
        self
    }

    pub fn host_config(&self) -> Option<&crate::models::ContainerSummaryInnerHostConfig> {
        self.host_config.as_ref()
    }

    pub fn reset_host_config(&mut self) {
        self.host_config = None;
    }

    pub fn set_network_settings(
        &mut self,
        network_settings: crate::models::ContainerSummaryInnerNetworkSettings,
    ) {
        self.network_settings = Some(network_settings);
    }

    pub fn with_network_settings(
        mut self,
        network_settings: crate::models::ContainerSummaryInnerNetworkSettings,
    ) -> Self {
        self.network_settings = Some(network_settings);
        self
    }

    pub fn network_settings(&self) -> Option<&crate::models::ContainerSummaryInnerNetworkSettings> {
        self.network_settings.as_ref()
    }

    pub fn reset_network_settings(&mut self) {
        self.network_settings = None;
    }

    pub fn set_mounts(&mut self, mounts: Vec<crate::models::Mount>) {
        self.mounts = Some(mounts);
    }

    pub fn with_mounts(mut self, mounts: Vec<crate::models::Mount>) -> Self {
        self.mounts = Some(mounts);
        self
    }

    pub fn mounts(&self) -> Option<&[crate::models::Mount]> {
        self.mounts.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_mounts(&mut self) {
        self.mounts = None;
    }
}
