/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// PluginConfig : The config of a plugin.

#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct PluginConfig {
    /// Docker Version used to create the plugin
    #[serde(
        rename = "DockerVersion",
        skip_serializing_if = "Option::is_none"
    )]
    docker_version: Option<String>,
    #[serde(rename = "Description")]
    description: String,
    #[serde(rename = "Documentation")]
    documentation: String,
    #[serde(rename = "Interface")]
    interface: ::models::PluginConfigInterface,
    #[serde(rename = "Entrypoint")]
    entrypoint: Vec<String>,
    #[serde(rename = "WorkDir")]
    work_dir: String,
    #[serde(rename = "User", skip_serializing_if = "Option::is_none")]
    user: Option<::models::PluginConfigUser>,
    #[serde(rename = "Network")]
    network: ::models::PluginConfigNetwork,
    #[serde(rename = "Linux")]
    linux: ::models::PluginConfigLinux,
    #[serde(rename = "PropagatedMount")]
    propagated_mount: String,
    #[serde(rename = "IpcHost")]
    ipc_host: bool,
    #[serde(rename = "PidHost")]
    pid_host: bool,
    #[serde(rename = "Mounts")]
    mounts: Vec<::models::PluginMount>,
    #[serde(rename = "Env")]
    env: Vec<::models::PluginEnv>,
    #[serde(rename = "Args")]
    args: ::models::PluginConfigArgs,
    #[serde(rename = "rootfs", skip_serializing_if = "Option::is_none")]
    rootfs: Option<::models::PluginConfigRootfs>,
}

impl PluginConfig {
    /// The config of a plugin.
    pub fn new(
        description: String,
        documentation: String,
        interface: ::models::PluginConfigInterface,
        entrypoint: Vec<String>,
        work_dir: String,
        network: ::models::PluginConfigNetwork,
        linux: ::models::PluginConfigLinux,
        propagated_mount: String,
        ipc_host: bool,
        pid_host: bool,
        mounts: Vec<::models::PluginMount>,
        env: Vec<::models::PluginEnv>,
        args: ::models::PluginConfigArgs,
    ) -> PluginConfig {
        PluginConfig {
            docker_version: None,
            description: description,
            documentation: documentation,
            interface: interface,
            entrypoint: entrypoint,
            work_dir: work_dir,
            user: None,
            network: network,
            linux: linux,
            propagated_mount: propagated_mount,
            ipc_host: ipc_host,
            pid_host: pid_host,
            mounts: mounts,
            env: env,
            args: args,
            rootfs: None,
        }
    }

    pub fn set_docker_version(&mut self, docker_version: String) {
        self.docker_version = Some(docker_version);
    }

    pub fn with_docker_version(mut self, docker_version: String) -> PluginConfig {
        self.docker_version = Some(docker_version);
        self
    }

    pub fn docker_version(&self) -> Option<&String> {
        self.docker_version.as_ref()
    }

    pub fn reset_docker_version(&mut self) {
        self.docker_version = None;
    }

    pub fn set_description(&mut self, description: String) {
        self.description = description;
    }

    pub fn with_description(mut self, description: String) -> PluginConfig {
        self.description = description;
        self
    }

    pub fn description(&self) -> &String {
        &self.description
    }

    pub fn set_documentation(&mut self, documentation: String) {
        self.documentation = documentation;
    }

    pub fn with_documentation(mut self, documentation: String) -> PluginConfig {
        self.documentation = documentation;
        self
    }

    pub fn documentation(&self) -> &String {
        &self.documentation
    }

    pub fn set_interface(&mut self, interface: ::models::PluginConfigInterface) {
        self.interface = interface;
    }

    pub fn with_interface(mut self, interface: ::models::PluginConfigInterface) -> PluginConfig {
        self.interface = interface;
        self
    }

    pub fn interface(&self) -> &::models::PluginConfigInterface {
        &self.interface
    }

    pub fn set_entrypoint(&mut self, entrypoint: Vec<String>) {
        self.entrypoint = entrypoint;
    }

    pub fn with_entrypoint(mut self, entrypoint: Vec<String>) -> PluginConfig {
        self.entrypoint = entrypoint;
        self
    }

    pub fn entrypoint(&self) -> &Vec<String> {
        &self.entrypoint
    }

    pub fn set_work_dir(&mut self, work_dir: String) {
        self.work_dir = work_dir;
    }

    pub fn with_work_dir(mut self, work_dir: String) -> PluginConfig {
        self.work_dir = work_dir;
        self
    }

    pub fn work_dir(&self) -> &String {
        &self.work_dir
    }

    pub fn set_user(&mut self, user: ::models::PluginConfigUser) {
        self.user = Some(user);
    }

    pub fn with_user(mut self, user: ::models::PluginConfigUser) -> PluginConfig {
        self.user = Some(user);
        self
    }

    pub fn user(&self) -> Option<&::models::PluginConfigUser> {
        self.user.as_ref()
    }

    pub fn reset_user(&mut self) {
        self.user = None;
    }

    pub fn set_network(&mut self, network: ::models::PluginConfigNetwork) {
        self.network = network;
    }

    pub fn with_network(mut self, network: ::models::PluginConfigNetwork) -> PluginConfig {
        self.network = network;
        self
    }

    pub fn network(&self) -> &::models::PluginConfigNetwork {
        &self.network
    }

    pub fn set_linux(&mut self, linux: ::models::PluginConfigLinux) {
        self.linux = linux;
    }

    pub fn with_linux(mut self, linux: ::models::PluginConfigLinux) -> PluginConfig {
        self.linux = linux;
        self
    }

    pub fn linux(&self) -> &::models::PluginConfigLinux {
        &self.linux
    }

    pub fn set_propagated_mount(&mut self, propagated_mount: String) {
        self.propagated_mount = propagated_mount;
    }

    pub fn with_propagated_mount(mut self, propagated_mount: String) -> PluginConfig {
        self.propagated_mount = propagated_mount;
        self
    }

    pub fn propagated_mount(&self) -> &String {
        &self.propagated_mount
    }

    pub fn set_ipc_host(&mut self, ipc_host: bool) {
        self.ipc_host = ipc_host;
    }

    pub fn with_ipc_host(mut self, ipc_host: bool) -> PluginConfig {
        self.ipc_host = ipc_host;
        self
    }

    pub fn ipc_host(&self) -> &bool {
        &self.ipc_host
    }

    pub fn set_pid_host(&mut self, pid_host: bool) {
        self.pid_host = pid_host;
    }

    pub fn with_pid_host(mut self, pid_host: bool) -> PluginConfig {
        self.pid_host = pid_host;
        self
    }

    pub fn pid_host(&self) -> &bool {
        &self.pid_host
    }

    pub fn set_mounts(&mut self, mounts: Vec<::models::PluginMount>) {
        self.mounts = mounts;
    }

    pub fn with_mounts(mut self, mounts: Vec<::models::PluginMount>) -> PluginConfig {
        self.mounts = mounts;
        self
    }

    pub fn mounts(&self) -> &Vec<::models::PluginMount> {
        &self.mounts
    }

    pub fn set_env(&mut self, env: Vec<::models::PluginEnv>) {
        self.env = env;
    }

    pub fn with_env(mut self, env: Vec<::models::PluginEnv>) -> PluginConfig {
        self.env = env;
        self
    }

    pub fn env(&self) -> &Vec<::models::PluginEnv> {
        &self.env
    }

    pub fn set_args(&mut self, args: ::models::PluginConfigArgs) {
        self.args = args;
    }

    pub fn with_args(mut self, args: ::models::PluginConfigArgs) -> PluginConfig {
        self.args = args;
        self
    }

    pub fn args(&self) -> &::models::PluginConfigArgs {
        &self.args
    }

    pub fn set_rootfs(&mut self, rootfs: ::models::PluginConfigRootfs) {
        self.rootfs = Some(rootfs);
    }

    pub fn with_rootfs(mut self, rootfs: ::models::PluginConfigRootfs) -> PluginConfig {
        self.rootfs = Some(rootfs);
        self
    }

    pub fn rootfs(&self) -> Option<&::models::PluginConfigRootfs> {
        self.rootfs.as_ref()
    }

    pub fn reset_rootfs(&mut self) {
        self.rootfs = None;
    }
}
