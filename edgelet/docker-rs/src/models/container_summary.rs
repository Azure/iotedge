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
pub struct ContainerSummary {
    #[serde(rename = "Id")]
    id: String,
    #[serde(rename = "Names")]
    names: Vec<String>,
    #[serde(rename = "Image")]
    image: String,
    #[serde(rename = "ImageID")]
    image_id: String,
    #[serde(rename = "Command")]
    command: String,
    #[serde(rename = "Created")]
    created: i64,
    #[serde(rename = "Ports")]
    ports: Vec<crate::models::Port>,
    #[serde(rename = "SizeRw", default = "i64::default")]
    size_rw: i64,
    #[serde(rename = "SizeRootFs", default = "i64::default")]
    size_root_fs: i64,
    #[serde(rename = "Labels")]
    labels: ::std::collections::HashMap<String, String>,
    #[serde(rename = "State")]
    state: String,
    #[serde(rename = "Status")]
    status: String,
    #[serde(rename = "HostConfig")]
    host_config: ContainerHostConfig,
    #[serde(rename = "NetworkSettings")]
    network_settings: ContainerNetworkSettings,
    #[serde(rename = "Mounts")]
    mounts: Vec<crate::models::Mount>,
}

impl ContainerSummary {
    pub fn new(
        id: String,
        names: Vec<String>,
        image: String,
        image_id: String,
        command: String,
        created: i64,
        ports: Vec<crate::models::Port>,
        size_rw: i64,
        size_root_fs: i64,
        labels: ::std::collections::HashMap<String, String>,
        state: String,
        status: String,
        host_config: ContainerHostConfig,
        network_settings: ContainerNetworkSettings,
        mounts: Vec<crate::models::Mount>,
    ) -> Self {
        ContainerSummary {
            id,
            names,
            image,
            image_id,
            command,
            created,
            ports,
            size_rw,
            size_root_fs,
            labels,
            state,
            status,
            host_config,
            network_settings,
            mounts,
        }
    }

    pub fn set_id(&mut self, id: String) {
        self.id = id;
    }
    pub fn with_id(mut self, id: String) -> Self {
        self.id = id;
        self
    }
    pub fn id(&self) -> &String {
        &self.id
    }

    pub fn set_names(&mut self, names: Vec<String>) {
        self.names = names;
    }
    pub fn with_names(mut self, names: Vec<String>) -> Self {
        self.names = names;
        self
    }
    pub fn names(&self) -> &[String] {
        &self.names
    }

    pub fn set_image(&mut self, image: String) {
        self.image = image;
    }
    pub fn with_image(mut self, image: String) -> Self {
        self.image = image;
        self
    }
    pub fn image(&self) -> &String {
        &self.image
    }

    pub fn set_image_id(&mut self, image_id: String) {
        self.image_id = image_id;
    }
    pub fn with_image_id(mut self, image_id: String) -> Self {
        self.image_id = image_id;
        self
    }
    pub fn image_id(&self) -> &String {
        &self.image_id
    }

    pub fn set_command(&mut self, command: String) {
        self.command = command;
    }
    pub fn with_command(mut self, command: String) -> Self {
        self.command = command;
        self
    }
    pub fn command(&self) -> &String {
        &self.command
    }

    pub fn set_created(&mut self, created: i64) {
        self.created = created;
    }
    pub fn with_created(mut self, created: i64) -> Self {
        self.created = created;
        self
    }
    pub fn created(&self) -> &i64 {
        &self.created
    }

    pub fn set_ports(&mut self, ports: Vec<crate::models::Port>) {
        self.ports = ports;
    }
    pub fn with_ports(mut self, ports: Vec<crate::models::Port>) -> Self {
        self.ports = ports;
        self
    }
    pub fn ports(&self) -> &[crate::models::Port] {
        &self.ports
    }

    pub fn set_size_rw(&mut self, size_rw: i64) {
        self.size_rw = size_rw;
    }
    pub fn with_size_rw(mut self, size_rw: i64) -> Self {
        self.size_rw = size_rw;
        self
    }
    pub fn size_rw(&self) -> &i64 {
        &self.size_rw
    }

    pub fn set_size_root_fs(&mut self, size_root_fs: i64) {
        self.size_root_fs = size_root_fs;
    }
    pub fn with_size_root_fs(mut self, size_root_fs: i64) -> Self {
        self.size_root_fs = size_root_fs;
        self
    }
    pub fn size_root_fs(&self) -> &i64 {
        &self.size_root_fs
    }

    pub fn set_labels(&mut self, labels: ::std::collections::HashMap<String, String>) {
        self.labels = labels;
    }
    pub fn with_labels(mut self, labels: ::std::collections::HashMap<String, String>) -> Self {
        self.labels = labels;
        self
    }
    pub fn labels(&self) -> &::std::collections::HashMap<String, String> {
        &self.labels
    }

    pub fn set_state(&mut self, state: String) {
        self.state = state;
    }
    pub fn with_state(mut self, state: String) -> Self {
        self.state = state;
        self
    }
    pub fn state(&self) -> &String {
        &self.state
    }

    pub fn set_status(&mut self, status: String) {
        self.status = status;
    }
    pub fn with_status(mut self, status: String) -> Self {
        self.status = status;
        self
    }
    pub fn status(&self) -> &String {
        &self.status
    }

    pub fn set_host_config(&mut self, host_config: ContainerHostConfig) {
        self.host_config = host_config;
    }
    pub fn with_host_config(mut self, host_config: ContainerHostConfig) -> Self {
        self.host_config = host_config;
        self
    }
    pub fn host_config(&self) -> &ContainerHostConfig {
        &self.host_config
    }

    pub fn set_network_settings(&mut self, network_settings: ContainerNetworkSettings) {
        self.network_settings = network_settings;
    }
    pub fn with_network_settings(mut self, network_settings: ContainerNetworkSettings) -> Self {
        self.network_settings = network_settings;
        self
    }
    pub fn network_settings(&self) -> &ContainerNetworkSettings {
        &self.network_settings
    }

    pub fn set_mounts(&mut self, mounts: Vec<crate::models::Mount>) {
        self.mounts = mounts;
    }
    pub fn with_mounts(mut self, mounts: Vec<crate::models::Mount>) -> Self {
        self.mounts = mounts;
        self
    }
    pub fn mounts(&self) -> &[crate::models::Mount] {
        &self.mounts
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub struct ContainerHostConfig {
    #[serde(rename = "NetworkMode")]
    network_mode: String,
}

impl ContainerHostConfig {
    pub fn new(network_mode: &str) -> Self {
        ContainerHostConfig {
            network_mode: network_mode.to_string(),
        }
    }

    pub fn network_mode(&self) -> &str {
        &self.network_mode
    }

    pub fn with_network_mode(mut self, network_mode: String) -> Self {
        self.network_mode = network_mode;
        self
    }

    pub fn set_network_mode(&mut self, network_mode: String) {
        self.network_mode = network_mode;
    }
}

#[derive(Debug, Serialize, Deserialize)]
pub struct ContainerNetworkSettings {
    #[serde(rename = "Networks")]
    networks: ::std::collections::HashMap<String, crate::models::EndpointSettings>,
}

impl ContainerNetworkSettings {
    pub fn new(
        networks: ::std::collections::HashMap<String, crate::models::EndpointSettings>,
    ) -> Self {
        ContainerNetworkSettings { networks }
    }

    pub fn set_networks(
        &mut self,
        networks: ::std::collections::HashMap<String, crate::models::EndpointSettings>,
    ) {
        self.networks = networks;
    }

    pub fn with_networks(
        mut self,
        networks: ::std::collections::HashMap<String, crate::models::EndpointSettings>,
    ) -> Self {
        self.networks = networks;
        self
    }

    pub fn networks(
        &self,
    ) -> &::std::collections::HashMap<String, crate::models::EndpointSettings> {
        &self.networks
    }
}
