/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

#[allow(unused_imports)]
use serde_json::Value;

// DEVNOTE: Why is most of this type commented out?
//
// We do not want to restrict the properties that the user can set in their create options, because future versions of Docker can add new properties
// that we don't define here.
//
// So this type has a `#[serde(flatten)] HashMap` field to collect all the extra properties that we don't have a struct field for.
//
// But if an existing field references another type under `crate::models::`, then that would still be parsed lossily, so we would have to also add
// a `#[serde(flatten)] HashMap` field there. And if that type has fields that reference types under `crate::models::` ...
//
// To avoid having to do this for effectively the whole crate, instead we've just commented out the fields we don't use in our code.
//
// ---
//
// If you need to access a commented out field, uncomment it.
//
// - If it's a simple built-in type, then that is all you need to do.
//
// - Otherwise if it references another type under `crate::models::`, then ensure that that type also has a `#[serde(flatten)] HashMap` property
//   and is commented out as much as possible. Also copy this devnote there for future readers.

#[derive(Debug, serde_derive::Serialize, serde_derive::Deserialize, Clone)]
pub struct ContainerCreateBody {
    /// The hostname to use for the container, as a valid RFC 1123 hostname.
    #[serde(rename = "Hostname", skip_serializing_if = "Option::is_none")]
    hostname: Option<String>,
    // /// The domain name to use for the container.
    // #[serde(rename = "Domainname", skip_serializing_if = "Option::is_none")]
    // domainname: Option<String>,
    // /// The user that commands are run as inside the container.
    // #[serde(rename = "User", skip_serializing_if = "Option::is_none")]
    // user: Option<String>,
    // /// Whether to attach to `stdin`.
    // #[serde(rename = "AttachStdin", skip_serializing_if = "Option::is_none")]
    // attach_stdin: Option<bool>,
    // /// Whether to attach to `stdout`.
    // #[serde(rename = "AttachStdout", skip_serializing_if = "Option::is_none")]
    // attach_stdout: Option<bool>,
    // /// Whether to attach to `stderr`.
    // #[serde(rename = "AttachStderr", skip_serializing_if = "Option::is_none")]
    // attach_stderr: Option<bool>,
    // /// An object mapping ports to an empty object in the form:  `{\"<port>/<tcp|udp>\": {}}`
    // #[serde(rename = "ExposedPorts", skip_serializing_if = "Option::is_none")]
    // exposed_ports: Option<::std::collections::HashMap<String, Value>>,
    // /// Attach standard streams to a TTY, including `stdin` if it is not closed.
    // #[serde(rename = "Tty", skip_serializing_if = "Option::is_none")]
    // tty: Option<bool>,
    // /// Open `stdin`
    // #[serde(rename = "OpenStdin", skip_serializing_if = "Option::is_none")]
    // open_stdin: Option<bool>,
    // /// Close `stdin` after one attached client disconnects
    // #[serde(rename = "StdinOnce", skip_serializing_if = "Option::is_none")]
    // stdin_once: Option<bool>,
    /// A list of environment variables to set inside the container in the form `[\"VAR=value\", ...]`. A variable without `=` is removed from the environment, rather than to have an empty value.
    #[serde(rename = "Env", skip_serializing_if = "Option::is_none")]
    env: Option<Vec<String>>,
    /// Command to run specified as a string or an array of strings.
    #[serde(rename = "Cmd", skip_serializing_if = "Option::is_none")]
    cmd: Option<Vec<String>>,
    // #[serde(rename = "Healthcheck", skip_serializing_if = "Option::is_none")]
    // healthcheck: Option<crate::models::HealthConfig>,
    // /// Command is already escaped (Windows only)
    // #[serde(rename = "ArgsEscaped", skip_serializing_if = "Option::is_none")]
    // args_escaped: Option<bool>,
    /// The name of the image to use when creating the container
    #[serde(rename = "Image", skip_serializing_if = "Option::is_none")]
    image: Option<String>,
    #[serde(rename = "Volumes", skip_serializing_if = "Option::is_none")]
    volumes: Option<::std::collections::HashMap<String, Value>>,
    // /// The working directory for commands to run in.
    // #[serde(rename = "WorkingDir", skip_serializing_if = "Option::is_none")]
    // working_dir: Option<String>,
    /// The entry point for the container as a string or an array of strings.
    /// If the array consists of exactly one empty string ([""]) then the entry
    /// point is reset to system default (i.e., the entry point used by docker
    /// when there is no ENTRYPOINT instruction in the Dockerfile).
    #[serde(rename = "Entrypoint", skip_serializing_if = "Option::is_none")]
    entrypoint: Option<Vec<String>>,
    // /// Disable networking for the container.
    // #[serde(rename = "NetworkDisabled", skip_serializing_if = "Option::is_none")]
    // network_disabled: Option<bool>,
    // /// MAC address of the container.
    // #[serde(rename = "MacAddress", skip_serializing_if = "Option::is_none")]
    // mac_address: Option<String>,
    // /// `ONBUILD` metadata that were defined in the image's `Dockerfile`.
    // #[serde(rename = "OnBuild", skip_serializing_if = "Option::is_none")]
    // on_build: Option<Vec<String>>,
    /// User-defined key/value metadata.
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    labels: Option<::std::collections::HashMap<String, String>>,
    // /// Signal to stop a container as a string or unsigned integer.
    // #[serde(rename = "StopSignal", skip_serializing_if = "Option::is_none")]
    // stop_signal: Option<String>,
    // /// Timeout to stop a container in seconds.
    // #[serde(rename = "StopTimeout", skip_serializing_if = "Option::is_none")]
    // stop_timeout: Option<i32>,
    // /// Shell for when `RUN`, `CMD`, and `ENTRYPOINT` uses a shell.
    // #[serde(rename = "Shell", skip_serializing_if = "Option::is_none")]
    // shell: Option<Vec<String>>,
    #[serde(rename = "HostConfig", skip_serializing_if = "Option::is_none")]
    host_config: Option<crate::models::HostConfig>,
    // #[serde(rename = "NetworkingConfig", skip_serializing_if = "Option::is_none")]
    // networking_config: Option<crate::models::ContainerCreateBodyNetworkingConfig>,
    #[serde(flatten)]
    other_properties: std::collections::HashMap<String, serde_json::Value>,
}

impl ContainerCreateBody {
    pub fn new() -> Self {
        ContainerCreateBody {
            hostname: None,
            // domainname: None,
            // user: None,
            // attach_stdin: None,
            // attach_stdout: None,
            // attach_stderr: None,
            // exposed_ports: None,
            // tty: None,
            // open_stdin: None,
            // stdin_once: None,
            env: None,
            cmd: None,
            // healthcheck: None,
            // args_escaped: None,
            image: None,
            volumes: None,
            // working_dir: None,
            entrypoint: None,
            // network_disabled: None,
            // mac_address: None,
            // on_build: None,
            labels: None,
            // stop_signal: None,
            // stop_timeout: None,
            // shell: None,
            host_config: None,
            // networking_config: None,
            other_properties: Default::default(),
        }
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

    // pub fn set_domainname(&mut self, domainname: String) {
    //     self.domainname = Some(domainname);
    // }

    // pub fn with_domainname(mut self, domainname: String) -> Self {
    //     self.domainname = Some(domainname);
    //     self
    // }

    // pub fn domainname(&self) -> Option<&str> {
    //     self.domainname.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_domainname(&mut self) {
    //     self.domainname = None;
    // }

    // pub fn set_user(&mut self, user: String) {
    //     self.user = Some(user);
    // }

    // pub fn with_user(mut self, user: String) -> Self {
    //     self.user = Some(user);
    //     self
    // }

    // pub fn user(&self) -> Option<&str> {
    //     self.user.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_user(&mut self) {
    //     self.user = None;
    // }

    // pub fn set_attach_stdin(&mut self, attach_stdin: bool) {
    //     self.attach_stdin = Some(attach_stdin);
    // }

    // pub fn with_attach_stdin(mut self, attach_stdin: bool) -> Self {
    //     self.attach_stdin = Some(attach_stdin);
    //     self
    // }

    // pub fn attach_stdin(&self) -> Option<&bool> {
    //     self.attach_stdin.as_ref()
    // }

    // pub fn reset_attach_stdin(&mut self) {
    //     self.attach_stdin = None;
    // }

    // pub fn set_attach_stdout(&mut self, attach_stdout: bool) {
    //     self.attach_stdout = Some(attach_stdout);
    // }

    // pub fn with_attach_stdout(mut self, attach_stdout: bool) -> Self {
    //     self.attach_stdout = Some(attach_stdout);
    //     self
    // }

    // pub fn attach_stdout(&self) -> Option<&bool> {
    //     self.attach_stdout.as_ref()
    // }

    // pub fn reset_attach_stdout(&mut self) {
    //     self.attach_stdout = None;
    // }

    // pub fn set_attach_stderr(&mut self, attach_stderr: bool) {
    //     self.attach_stderr = Some(attach_stderr);
    // }

    // pub fn with_attach_stderr(mut self, attach_stderr: bool) -> Self {
    //     self.attach_stderr = Some(attach_stderr);
    //     self
    // }

    // pub fn attach_stderr(&self) -> Option<&bool> {
    //     self.attach_stderr.as_ref()
    // }

    // pub fn reset_attach_stderr(&mut self) {
    //     self.attach_stderr = None;
    // }

    // pub fn set_exposed_ports(&mut self, exposed_ports: ::std::collections::HashMap<String, Value>) {
    //     self.exposed_ports = Some(exposed_ports);
    // }

    // pub fn with_exposed_ports(
    //     mut self,
    //     exposed_ports: ::std::collections::HashMap<String, Value>,
    // ) -> Self {
    //     self.exposed_ports = Some(exposed_ports);
    //     self
    // }

    // pub fn exposed_ports(&self) -> Option<&::std::collections::HashMap<String, Value>> {
    //     self.exposed_ports.as_ref()
    // }

    // pub fn reset_exposed_ports(&mut self) {
    //     self.exposed_ports = None;
    // }

    // pub fn set_tty(&mut self, tty: bool) {
    //     self.tty = Some(tty);
    // }

    // pub fn with_tty(mut self, tty: bool) -> Self {
    //     self.tty = Some(tty);
    //     self
    // }

    // pub fn tty(&self) -> Option<&bool> {
    //     self.tty.as_ref()
    // }

    // pub fn reset_tty(&mut self) {
    //     self.tty = None;
    // }

    // pub fn set_open_stdin(&mut self, open_stdin: bool) {
    //     self.open_stdin = Some(open_stdin);
    // }

    // pub fn with_open_stdin(mut self, open_stdin: bool) -> Self {
    //     self.open_stdin = Some(open_stdin);
    //     self
    // }

    // pub fn open_stdin(&self) -> Option<&bool> {
    //     self.open_stdin.as_ref()
    // }

    // pub fn reset_open_stdin(&mut self) {
    //     self.open_stdin = None;
    // }

    // pub fn set_stdin_once(&mut self, stdin_once: bool) {
    //     self.stdin_once = Some(stdin_once);
    // }

    // pub fn with_stdin_once(mut self, stdin_once: bool) -> Self {
    //     self.stdin_once = Some(stdin_once);
    //     self
    // }

    // pub fn stdin_once(&self) -> Option<&bool> {
    //     self.stdin_once.as_ref()
    // }

    // pub fn reset_stdin_once(&mut self) {
    //     self.stdin_once = None;
    // }

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

    pub fn set_cmd(&mut self, cmd: Vec<String>) {
        self.cmd = Some(cmd);
    }

    pub fn with_cmd(mut self, cmd: Vec<String>) -> Self {
        self.cmd = Some(cmd);
        self
    }

    pub fn cmd(&self) -> Option<&[String]> {
        self.cmd.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_cmd(&mut self) {
        self.cmd = None;
    }

    // pub fn set_healthcheck(&mut self, healthcheck: crate::models::HealthConfig) {
    //     self.healthcheck = Some(healthcheck);
    // }

    // pub fn with_healthcheck(mut self, healthcheck: crate::models::HealthConfig) -> Self {
    //     self.healthcheck = Some(healthcheck);
    //     self
    // }

    // pub fn healthcheck(&self) -> Option<&crate::models::HealthConfig> {
    //     self.healthcheck.as_ref()
    // }

    // pub fn reset_healthcheck(&mut self) {
    //     self.healthcheck = None;
    // }

    // pub fn set_args_escaped(&mut self, args_escaped: bool) {
    //     self.args_escaped = Some(args_escaped);
    // }

    // pub fn with_args_escaped(mut self, args_escaped: bool) -> Self {
    //     self.args_escaped = Some(args_escaped);
    //     self
    // }

    // pub fn args_escaped(&self) -> Option<&bool> {
    //     self.args_escaped.as_ref()
    // }

    // pub fn reset_args_escaped(&mut self) {
    //     self.args_escaped = None;
    // }

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

    pub fn set_volumes(&mut self, volumes: ::std::collections::HashMap<String, Value>) {
        self.volumes = Some(volumes);
    }

    pub fn with_volumes(mut self, volumes: ::std::collections::HashMap<String, Value>) -> Self {
        self.volumes = Some(volumes);
        self
    }

    pub fn volumes(&self) -> Option<&::std::collections::HashMap<String, Value>> {
        self.volumes.as_ref()
    }

    pub fn reset_volumes(&mut self) {
        self.volumes = None;
    }

    // pub fn set_working_dir(&mut self, working_dir: String) {
    //     self.working_dir = Some(working_dir);
    // }

    // pub fn with_working_dir(mut self, working_dir: String) -> Self {
    //     self.working_dir = Some(working_dir);
    //     self
    // }

    // pub fn working_dir(&self) -> Option<&str> {
    //     self.working_dir.as_ref().map(AsRef::as_ref)
    // }

    pub fn set_entrypoint(&mut self, entrypoint: Vec<String>) {
        self.entrypoint = Some(entrypoint);
    }

    pub fn with_entrypoint(mut self, entrypoint: Vec<String>) -> Self {
        self.entrypoint = Some(entrypoint);
        self
    }

    pub fn entrypoint(&self) -> Option<&[String]> {
        self.entrypoint.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_entrypoint(&mut self) {
        self.entrypoint = None;
    }

    // pub fn reset_working_dir(&mut self) {
    //     self.working_dir = None;
    // }

    // pub fn set_network_disabled(&mut self, network_disabled: bool) {
    //     self.network_disabled = Some(network_disabled);
    // }

    // pub fn with_network_disabled(mut self, network_disabled: bool) -> Self {
    //     self.network_disabled = Some(network_disabled);
    //     self
    // }

    // pub fn network_disabled(&self) -> Option<&bool> {
    //     self.network_disabled.as_ref()
    // }

    // pub fn reset_network_disabled(&mut self) {
    //     self.network_disabled = None;
    // }

    // pub fn set_mac_address(&mut self, mac_address: String) {
    //     self.mac_address = Some(mac_address);
    // }

    // pub fn with_mac_address(mut self, mac_address: String) -> Self {
    //     self.mac_address = Some(mac_address);
    //     self
    // }

    // pub fn mac_address(&self) -> Option<&str> {
    //     self.mac_address.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_mac_address(&mut self) {
    //     self.mac_address = None;
    // }

    // pub fn set_on_build(&mut self, on_build: Vec<String>) {
    //     self.on_build = Some(on_build);
    // }

    // pub fn with_on_build(mut self, on_build: Vec<String>) -> Self {
    //     self.on_build = Some(on_build);
    //     self
    // }

    // pub fn on_build(&self) -> Option<&[String]> {
    //     self.on_build.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_on_build(&mut self) {
    //     self.on_build = None;
    // }

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

    // pub fn set_stop_signal(&mut self, stop_signal: String) {
    //     self.stop_signal = Some(stop_signal);
    // }

    // pub fn with_stop_signal(mut self, stop_signal: String) -> Self {
    //     self.stop_signal = Some(stop_signal);
    //     self
    // }

    // pub fn stop_signal(&self) -> Option<&str> {
    //     self.stop_signal.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_stop_signal(&mut self) {
    //     self.stop_signal = None;
    // }

    // pub fn set_stop_timeout(&mut self, stop_timeout: i32) {
    //     self.stop_timeout = Some(stop_timeout);
    // }

    // pub fn with_stop_timeout(mut self, stop_timeout: i32) -> Self {
    //     self.stop_timeout = Some(stop_timeout);
    //     self
    // }

    // pub fn stop_timeout(&self) -> Option<i32> {
    //     self.stop_timeout
    // }

    // pub fn reset_stop_timeout(&mut self) {
    //     self.stop_timeout = None;
    // }

    // pub fn set_shell(&mut self, shell: Vec<String>) {
    //     self.shell = Some(shell);
    // }

    // pub fn with_shell(mut self, shell: Vec<String>) -> Self {
    //     self.shell = Some(shell);
    //     self
    // }

    // pub fn shell(&self) -> Option<&[String]> {
    //     self.shell.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_shell(&mut self) {
    //     self.shell = None;
    // }

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

    // pub fn set_networking_config(
    //     &mut self,
    //     networking_config: crate::models::ContainerCreateBodyNetworkingConfig,
    // ) {
    //     self.networking_config = Some(networking_config);
    // }

    // pub fn with_networking_config(
    //     mut self,
    //     networking_config: crate::models::ContainerCreateBodyNetworkingConfig,
    // ) -> Self {
    //     self.networking_config = Some(networking_config);
    //     self
    // }

    // pub fn networking_config(&self) -> Option<&crate::models::ContainerCreateBodyNetworkingConfig> {
    //     self.networking_config.as_ref()
    // }

    // pub fn reset_networking_config(&mut self) {
    //     self.networking_config = None;
    // }
}
