/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// TaskSpecContainerSpec : Invalid when specified with `PluginSpec`.
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct TaskSpecContainerSpec {
    /// The image name to use for the container
    #[serde(rename = "Image", skip_serializing_if = "Option::is_none")]
    image: Option<String>,
    /// User-defined key/value data.
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    labels: Option<::std::collections::HashMap<String, String>>,
    /// The command to be run in the image.
    #[serde(rename = "Command", skip_serializing_if = "Option::is_none")]
    command: Option<Vec<String>>,
    /// Arguments to the command.
    #[serde(rename = "Args", skip_serializing_if = "Option::is_none")]
    args: Option<Vec<String>>,
    /// The hostname to use for the container, as a valid RFC 1123 hostname.
    #[serde(rename = "Hostname", skip_serializing_if = "Option::is_none")]
    hostname: Option<String>,
    /// A list of environment variables in the form `VAR=value`.
    #[serde(rename = "Env", skip_serializing_if = "Option::is_none")]
    env: Option<Vec<String>>,
    /// The working directory for commands to run in.
    #[serde(rename = "Dir", skip_serializing_if = "Option::is_none")]
    dir: Option<String>,
    /// The user inside the container.
    #[serde(rename = "User", skip_serializing_if = "Option::is_none")]
    user: Option<String>,
    /// A list of additional groups that the container process will run as.
    #[serde(rename = "Groups", skip_serializing_if = "Option::is_none")]
    groups: Option<Vec<String>>,
    #[serde(rename = "Privileges", skip_serializing_if = "Option::is_none")]
    privileges: Option<crate::models::TaskSpecContainerSpecPrivileges>,
    /// Whether a pseudo-TTY should be allocated.
    #[serde(rename = "TTY", skip_serializing_if = "Option::is_none")]
    TTY: Option<bool>,
    /// Open `stdin`
    #[serde(rename = "OpenStdin", skip_serializing_if = "Option::is_none")]
    open_stdin: Option<bool>,
    /// Mount the container's root filesystem as read only.
    #[serde(rename = "ReadOnly", skip_serializing_if = "Option::is_none")]
    read_only: Option<bool>,
    /// Specification for mounts to be added to containers created as part of the service.
    #[serde(rename = "Mounts", skip_serializing_if = "Option::is_none")]
    mounts: Option<Vec<crate::models::Mount>>,
    /// Signal to stop the container.
    #[serde(rename = "StopSignal", skip_serializing_if = "Option::is_none")]
    stop_signal: Option<String>,
    /// Amount of time to wait for the container to terminate before forcefully killing it.
    #[serde(rename = "StopGracePeriod", skip_serializing_if = "Option::is_none")]
    stop_grace_period: Option<i64>,
    #[serde(rename = "HealthCheck", skip_serializing_if = "Option::is_none")]
    health_check: Option<crate::models::HealthConfig>,
    /// A list of hostname/IP mappings to add to the container's `hosts` file. The format of extra hosts is specified in the [hosts(5)](http://man7.org/linux/man-pages/man5/hosts.5.html) man page:      IP_address canonical_hostname [aliases...]
    #[serde(rename = "Hosts", skip_serializing_if = "Option::is_none")]
    hosts: Option<Vec<String>>,
    #[serde(rename = "DNSConfig", skip_serializing_if = "Option::is_none")]
    dns_config: Option<crate::models::TaskSpecContainerSpecDnsConfig>,
    /// Secrets contains references to zero or more secrets that will be exposed to the service.
    #[serde(rename = "Secrets", skip_serializing_if = "Option::is_none")]
    secrets: Option<Vec<crate::models::TaskSpecContainerSpecSecrets>>,
    /// Configs contains references to zero or more configs that will be exposed to the service.
    #[serde(rename = "Configs", skip_serializing_if = "Option::is_none")]
    configs: Option<Vec<crate::models::TaskSpecContainerSpecConfigs>>,
}

impl TaskSpecContainerSpec {
    /// Invalid when specified with `PluginSpec`.
    pub fn new() -> Self {
        TaskSpecContainerSpec {
            image: None,
            labels: None,
            command: None,
            args: None,
            hostname: None,
            env: None,
            dir: None,
            user: None,
            groups: None,
            privileges: None,
            TTY: None,
            open_stdin: None,
            read_only: None,
            mounts: None,
            stop_signal: None,
            stop_grace_period: None,
            health_check: None,
            hosts: None,
            dns_config: None,
            secrets: None,
            configs: None,
        }
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

    pub fn set_command(&mut self, command: Vec<String>) {
        self.command = Some(command);
    }

    pub fn with_command(mut self, command: Vec<String>) -> Self {
        self.command = Some(command);
        self
    }

    pub fn command(&self) -> Option<&[String]> {
        self.command.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_command(&mut self) {
        self.command = None;
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

    pub fn set_hostname(&mut self, hostname: String) {
        self.hostname = Some(hostname);
    }

    pub fn with_hostname(mut self, hostname: String) -> Self {
        self.hostname = Some(hostname);
        self
    }

    pub fn hostname(&self) -> Option<&str> {
        self.hostname.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_hostname(&mut self) {
        self.hostname = None;
    }

    pub fn set_env(&mut self, env: Vec<String>) {
        self.env = Some(env);
    }

    pub fn with_env(mut self, env: Vec<String>) -> Self {
        self.env = Some(env);
        self
    }

    pub fn env(&self) -> Option<&[String]> {
        self.env.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_env(&mut self) {
        self.env = None;
    }

    pub fn set_dir(&mut self, dir: String) {
        self.dir = Some(dir);
    }

    pub fn with_dir(mut self, dir: String) -> Self {
        self.dir = Some(dir);
        self
    }

    pub fn dir(&self) -> Option<&str> {
        self.dir.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_dir(&mut self) {
        self.dir = None;
    }

    pub fn set_user(&mut self, user: String) {
        self.user = Some(user);
    }

    pub fn with_user(mut self, user: String) -> Self {
        self.user = Some(user);
        self
    }

    pub fn user(&self) -> Option<&str> {
        self.user.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_user(&mut self) {
        self.user = None;
    }

    pub fn set_groups(&mut self, groups: Vec<String>) {
        self.groups = Some(groups);
    }

    pub fn with_groups(mut self, groups: Vec<String>) -> Self {
        self.groups = Some(groups);
        self
    }

    pub fn groups(&self) -> Option<&[String]> {
        self.groups.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_groups(&mut self) {
        self.groups = None;
    }

    pub fn set_privileges(&mut self, privileges: crate::models::TaskSpecContainerSpecPrivileges) {
        self.privileges = Some(privileges);
    }

    pub fn with_privileges(
        mut self,
        privileges: crate::models::TaskSpecContainerSpecPrivileges,
    ) -> Self {
        self.privileges = Some(privileges);
        self
    }

    pub fn privileges(&self) -> Option<&crate::models::TaskSpecContainerSpecPrivileges> {
        self.privileges.as_ref()
    }

    pub fn reset_privileges(&mut self) {
        self.privileges = None;
    }

    pub fn set_TTY(&mut self, TTY: bool) {
        self.TTY = Some(TTY);
    }

    pub fn with_TTY(mut self, TTY: bool) -> Self {
        self.TTY = Some(TTY);
        self
    }

    pub fn TTY(&self) -> Option<&bool> {
        self.TTY.as_ref()
    }

    pub fn reset_TTY(&mut self) {
        self.TTY = None;
    }

    pub fn set_open_stdin(&mut self, open_stdin: bool) {
        self.open_stdin = Some(open_stdin);
    }

    pub fn with_open_stdin(mut self, open_stdin: bool) -> Self {
        self.open_stdin = Some(open_stdin);
        self
    }

    pub fn open_stdin(&self) -> Option<&bool> {
        self.open_stdin.as_ref()
    }

    pub fn reset_open_stdin(&mut self) {
        self.open_stdin = None;
    }

    pub fn set_read_only(&mut self, read_only: bool) {
        self.read_only = Some(read_only);
    }

    pub fn with_read_only(mut self, read_only: bool) -> Self {
        self.read_only = Some(read_only);
        self
    }

    pub fn read_only(&self) -> Option<&bool> {
        self.read_only.as_ref()
    }

    pub fn reset_read_only(&mut self) {
        self.read_only = None;
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

    pub fn set_stop_signal(&mut self, stop_signal: String) {
        self.stop_signal = Some(stop_signal);
    }

    pub fn with_stop_signal(mut self, stop_signal: String) -> Self {
        self.stop_signal = Some(stop_signal);
        self
    }

    pub fn stop_signal(&self) -> Option<&str> {
        self.stop_signal.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_stop_signal(&mut self) {
        self.stop_signal = None;
    }

    pub fn set_stop_grace_period(&mut self, stop_grace_period: i64) {
        self.stop_grace_period = Some(stop_grace_period);
    }

    pub fn with_stop_grace_period(mut self, stop_grace_period: i64) -> Self {
        self.stop_grace_period = Some(stop_grace_period);
        self
    }

    pub fn stop_grace_period(&self) -> Option<i64> {
        self.stop_grace_period
    }

    pub fn reset_stop_grace_period(&mut self) {
        self.stop_grace_period = None;
    }

    pub fn set_health_check(&mut self, health_check: crate::models::HealthConfig) {
        self.health_check = Some(health_check);
    }

    pub fn with_health_check(mut self, health_check: crate::models::HealthConfig) -> Self {
        self.health_check = Some(health_check);
        self
    }

    pub fn health_check(&self) -> Option<&crate::models::HealthConfig> {
        self.health_check.as_ref()
    }

    pub fn reset_health_check(&mut self) {
        self.health_check = None;
    }

    pub fn set_hosts(&mut self, hosts: Vec<String>) {
        self.hosts = Some(hosts);
    }

    pub fn with_hosts(mut self, hosts: Vec<String>) -> Self {
        self.hosts = Some(hosts);
        self
    }

    pub fn hosts(&self) -> Option<&[String]> {
        self.hosts.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_hosts(&mut self) {
        self.hosts = None;
    }

    pub fn set_dns_config(&mut self, dns_config: crate::models::TaskSpecContainerSpecDnsConfig) {
        self.dns_config = Some(dns_config);
    }

    pub fn with_dns_config(
        mut self,
        dns_config: crate::models::TaskSpecContainerSpecDnsConfig,
    ) -> Self {
        self.dns_config = Some(dns_config);
        self
    }

    pub fn dns_config(&self) -> Option<&crate::models::TaskSpecContainerSpecDnsConfig> {
        self.dns_config.as_ref()
    }

    pub fn reset_dns_config(&mut self) {
        self.dns_config = None;
    }

    pub fn set_secrets(&mut self, secrets: Vec<crate::models::TaskSpecContainerSpecSecrets>) {
        self.secrets = Some(secrets);
    }

    pub fn with_secrets(
        mut self,
        secrets: Vec<crate::models::TaskSpecContainerSpecSecrets>,
    ) -> Self {
        self.secrets = Some(secrets);
        self
    }

    pub fn secrets(&self) -> Option<&[crate::models::TaskSpecContainerSpecSecrets]> {
        self.secrets.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_secrets(&mut self) {
        self.secrets = None;
    }

    pub fn set_configs(&mut self, configs: Vec<crate::models::TaskSpecContainerSpecConfigs>) {
        self.configs = Some(configs);
    }

    pub fn with_configs(
        mut self,
        configs: Vec<crate::models::TaskSpecContainerSpecConfigs>,
    ) -> Self {
        self.configs = Some(configs);
        self
    }

    pub fn configs(&self) -> Option<&[crate::models::TaskSpecContainerSpecConfigs]> {
        self.configs.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_configs(&mut self) {
        self.configs = None;
    }
}
