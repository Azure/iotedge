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
pub struct InlineResponse200 {
    /// The ID of the container
    #[serde(rename = "Id", skip_serializing_if = "Option::is_none")]
    id: Option<String>,
    /// The time the container was created
    #[serde(rename = "Created", skip_serializing_if = "Option::is_none")]
    created: Option<String>,
    /// The path to the command being run
    #[serde(rename = "Path", skip_serializing_if = "Option::is_none")]
    path: Option<String>,
    /// The arguments to the command being run
    #[serde(rename = "Args", skip_serializing_if = "Option::is_none")]
    args: Option<Vec<String>>,
    #[serde(rename = "State", skip_serializing_if = "Option::is_none")]
    state: Option<crate::models::InlineResponse200State>,
    /// The container's image
    #[serde(rename = "Image", skip_serializing_if = "Option::is_none")]
    image: Option<String>,
    #[serde(rename = "ResolvConfPath", skip_serializing_if = "Option::is_none")]
    resolv_conf_path: Option<String>,
    #[serde(rename = "HostnamePath", skip_serializing_if = "Option::is_none")]
    hostname_path: Option<String>,
    #[serde(rename = "HostsPath", skip_serializing_if = "Option::is_none")]
    hosts_path: Option<String>,
    #[serde(rename = "LogPath", skip_serializing_if = "Option::is_none")]
    log_path: Option<String>,
    /// TODO
    #[serde(rename = "Node", skip_serializing_if = "Option::is_none")]
    node: Option<Value>,
    #[serde(rename = "Name", skip_serializing_if = "Option::is_none")]
    name: Option<String>,
    #[serde(rename = "RestartCount", skip_serializing_if = "Option::is_none")]
    restart_count: Option<i32>,
    #[serde(rename = "Driver", skip_serializing_if = "Option::is_none")]
    driver: Option<String>,
    #[serde(rename = "MountLabel", skip_serializing_if = "Option::is_none")]
    mount_label: Option<String>,
    #[serde(rename = "ProcessLabel", skip_serializing_if = "Option::is_none")]
    process_label: Option<String>,
    #[serde(rename = "AppArmorProfile", skip_serializing_if = "Option::is_none")]
    app_armor_profile: Option<String>,
    #[serde(rename = "ExecIDs", skip_serializing_if = "Option::is_none")]
    exec_i_ds: Option<Vec<String>>,
    #[serde(rename = "HostConfig", skip_serializing_if = "Option::is_none")]
    host_config: Option<crate::models::HostConfig>,
    #[serde(rename = "GraphDriver", skip_serializing_if = "Option::is_none")]
    graph_driver: Option<crate::models::GraphDriverData>,
    /// The size of files that have been created or changed by this container.
    #[serde(rename = "SizeRw", skip_serializing_if = "Option::is_none")]
    size_rw: Option<i64>,
    /// The total size of all the files in this container.
    #[serde(rename = "SizeRootFs", skip_serializing_if = "Option::is_none")]
    size_root_fs: Option<i64>,
    #[serde(rename = "Mounts", skip_serializing_if = "Option::is_none")]
    mounts: Option<Vec<crate::models::MountPoint>>,
    #[serde(rename = "Config", skip_serializing_if = "Option::is_none")]
    config: Option<crate::models::ContainerConfig>,
    #[serde(rename = "NetworkSettings", skip_serializing_if = "Option::is_none")]
    network_settings: Option<crate::models::NetworkSettings>,
}

impl InlineResponse200 {
    pub fn new() -> Self {
        InlineResponse200 {
            id: None,
            created: None,
            path: None,
            args: None,
            state: None,
            image: None,
            resolv_conf_path: None,
            hostname_path: None,
            hosts_path: None,
            log_path: None,
            node: None,
            name: None,
            restart_count: None,
            driver: None,
            mount_label: None,
            process_label: None,
            app_armor_profile: None,
            exec_i_ds: None,
            host_config: None,
            graph_driver: None,
            size_rw: None,
            size_root_fs: None,
            mounts: None,
            config: None,
            network_settings: None,
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

    pub fn set_created(&mut self, created: String) {
        self.created = Some(created);
    }

    pub fn with_created(mut self, created: String) -> Self {
        self.created = Some(created);
        self
    }

    pub fn created(&self) -> Option<&str> {
        self.created.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_created(&mut self) {
        self.created = None;
    }

    pub fn set_path(&mut self, path: String) {
        self.path = Some(path);
    }

    pub fn with_path(mut self, path: String) -> Self {
        self.path = Some(path);
        self
    }

    pub fn path(&self) -> Option<&str> {
        self.path.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_path(&mut self) {
        self.path = None;
    }

    pub fn set_args(&mut self, args: Vec<String>) {
        self.args = Some(args);
    }

    pub fn with_args(mut self, args: Vec<String>) -> Self {
        self.args = Some(args);
        self
    }

    pub fn args(&self) -> Option<&[String]> {
        self.args.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_args(&mut self) {
        self.args = None;
    }

    pub fn set_state(&mut self, state: crate::models::InlineResponse200State) {
        self.state = Some(state);
    }

    pub fn with_state(mut self, state: crate::models::InlineResponse200State) -> Self {
        self.state = Some(state);
        self
    }

    pub fn state(&self) -> Option<&crate::models::InlineResponse200State> {
        self.state.as_ref()
    }

    pub fn reset_state(&mut self) {
        self.state = None;
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

    pub fn set_resolv_conf_path(&mut self, resolv_conf_path: String) {
        self.resolv_conf_path = Some(resolv_conf_path);
    }

    pub fn with_resolv_conf_path(mut self, resolv_conf_path: String) -> Self {
        self.resolv_conf_path = Some(resolv_conf_path);
        self
    }

    pub fn resolv_conf_path(&self) -> Option<&str> {
        self.resolv_conf_path.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_resolv_conf_path(&mut self) {
        self.resolv_conf_path = None;
    }

    pub fn set_hostname_path(&mut self, hostname_path: String) {
        self.hostname_path = Some(hostname_path);
    }

    pub fn with_hostname_path(mut self, hostname_path: String) -> Self {
        self.hostname_path = Some(hostname_path);
        self
    }

    pub fn hostname_path(&self) -> Option<&str> {
        self.hostname_path.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_hostname_path(&mut self) {
        self.hostname_path = None;
    }

    pub fn set_hosts_path(&mut self, hosts_path: String) {
        self.hosts_path = Some(hosts_path);
    }

    pub fn with_hosts_path(mut self, hosts_path: String) -> Self {
        self.hosts_path = Some(hosts_path);
        self
    }

    pub fn hosts_path(&self) -> Option<&str> {
        self.hosts_path.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_hosts_path(&mut self) {
        self.hosts_path = None;
    }

    pub fn set_log_path(&mut self, log_path: String) {
        self.log_path = Some(log_path);
    }

    pub fn with_log_path(mut self, log_path: String) -> Self {
        self.log_path = Some(log_path);
        self
    }

    pub fn log_path(&self) -> Option<&str> {
        self.log_path.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_log_path(&mut self) {
        self.log_path = None;
    }

    pub fn set_node(&mut self, node: Value) {
        self.node = Some(node);
    }

    pub fn with_node(mut self, node: Value) -> Self {
        self.node = Some(node);
        self
    }

    pub fn node(&self) -> Option<&Value> {
        self.node.as_ref()
    }

    pub fn reset_node(&mut self) {
        self.node = None;
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

    pub fn set_restart_count(&mut self, restart_count: i32) {
        self.restart_count = Some(restart_count);
    }

    pub fn with_restart_count(mut self, restart_count: i32) -> Self {
        self.restart_count = Some(restart_count);
        self
    }

    pub fn restart_count(&self) -> Option<i32> {
        self.restart_count
    }

    pub fn reset_restart_count(&mut self) {
        self.restart_count = None;
    }

    pub fn set_driver(&mut self, driver: String) {
        self.driver = Some(driver);
    }

    pub fn with_driver(mut self, driver: String) -> Self {
        self.driver = Some(driver);
        self
    }

    pub fn driver(&self) -> Option<&str> {
        self.driver.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_driver(&mut self) {
        self.driver = None;
    }

    pub fn set_mount_label(&mut self, mount_label: String) {
        self.mount_label = Some(mount_label);
    }

    pub fn with_mount_label(mut self, mount_label: String) -> Self {
        self.mount_label = Some(mount_label);
        self
    }

    pub fn mount_label(&self) -> Option<&str> {
        self.mount_label.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_mount_label(&mut self) {
        self.mount_label = None;
    }

    pub fn set_process_label(&mut self, process_label: String) {
        self.process_label = Some(process_label);
    }

    pub fn with_process_label(mut self, process_label: String) -> Self {
        self.process_label = Some(process_label);
        self
    }

    pub fn process_label(&self) -> Option<&str> {
        self.process_label.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_process_label(&mut self) {
        self.process_label = None;
    }

    pub fn set_app_armor_profile(&mut self, app_armor_profile: String) {
        self.app_armor_profile = Some(app_armor_profile);
    }

    pub fn with_app_armor_profile(mut self, app_armor_profile: String) -> Self {
        self.app_armor_profile = Some(app_armor_profile);
        self
    }

    pub fn app_armor_profile(&self) -> Option<&str> {
        self.app_armor_profile.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_app_armor_profile(&mut self) {
        self.app_armor_profile = None;
    }

    pub fn set_exec_i_ds(&mut self, exec_i_ds: Vec<String>) {
        self.exec_i_ds = Some(exec_i_ds);
    }

    pub fn with_exec_i_ds(mut self, exec_i_ds: Vec<String>) -> Self {
        self.exec_i_ds = Some(exec_i_ds);
        self
    }

    pub fn exec_i_ds(&self) -> Option<&[String]> {
        self.exec_i_ds.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_exec_i_ds(&mut self) {
        self.exec_i_ds = None;
    }

    pub fn set_host_config(&mut self, host_config: crate::models::HostConfig) {
        self.host_config = Some(host_config);
    }

    pub fn with_host_config(mut self, host_config: crate::models::HostConfig) -> Self {
        self.host_config = Some(host_config);
        self
    }

    pub fn host_config(&self) -> Option<&crate::models::HostConfig> {
        self.host_config.as_ref()
    }

    pub fn reset_host_config(&mut self) {
        self.host_config = None;
    }

    pub fn set_graph_driver(&mut self, graph_driver: crate::models::GraphDriverData) {
        self.graph_driver = Some(graph_driver);
    }

    pub fn with_graph_driver(mut self, graph_driver: crate::models::GraphDriverData) -> Self {
        self.graph_driver = Some(graph_driver);
        self
    }

    pub fn graph_driver(&self) -> Option<&crate::models::GraphDriverData> {
        self.graph_driver.as_ref()
    }

    pub fn reset_graph_driver(&mut self) {
        self.graph_driver = None;
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

    pub fn set_mounts(&mut self, mounts: Vec<crate::models::MountPoint>) {
        self.mounts = Some(mounts);
    }

    pub fn with_mounts(mut self, mounts: Vec<crate::models::MountPoint>) -> Self {
        self.mounts = Some(mounts);
        self
    }

    pub fn mounts(&self) -> Option<&[crate::models::MountPoint]> {
        self.mounts.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_mounts(&mut self) {
        self.mounts = None;
    }

    pub fn set_config(&mut self, config: crate::models::ContainerConfig) {
        self.config = Some(config);
    }

    pub fn with_config(mut self, config: crate::models::ContainerConfig) -> Self {
        self.config = Some(config);
        self
    }

    pub fn config(&self) -> Option<&crate::models::ContainerConfig> {
        self.config.as_ref()
    }

    pub fn reset_config(&mut self) {
        self.config = None;
    }

    pub fn set_network_settings(&mut self, network_settings: crate::models::NetworkSettings) {
        self.network_settings = Some(network_settings);
    }

    pub fn with_network_settings(
        mut self,
        network_settings: crate::models::NetworkSettings,
    ) -> Self {
        self.network_settings = Some(network_settings);
        self
    }

    pub fn network_settings(&self) -> Option<&crate::models::NetworkSettings> {
        self.network_settings.as_ref()
    }

    pub fn reset_network_settings(&mut self) {
        self.network_settings = None;
    }
}
