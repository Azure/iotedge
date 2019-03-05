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
pub struct SystemInfo {
    /// Unique identifier of the daemon.  <p><br /></p>  > **Note**: The format of the ID itself is not part of the API, and > should not be considered stable.
    #[serde(rename = "ID", skip_serializing_if = "Option::is_none")]
    ID: Option<String>,
    /// Total number of containers on the host.
    #[serde(rename = "Containers", skip_serializing_if = "Option::is_none")]
    containers: Option<i32>,
    /// Number of containers with status `\"running\"`.
    #[serde(rename = "ContainersRunning", skip_serializing_if = "Option::is_none")]
    containers_running: Option<i32>,
    /// Number of containers with status `\"paused\"`.
    #[serde(rename = "ContainersPaused", skip_serializing_if = "Option::is_none")]
    containers_paused: Option<i32>,
    /// Number of containers with status `\"stopped\"`.
    #[serde(rename = "ContainersStopped", skip_serializing_if = "Option::is_none")]
    containers_stopped: Option<i32>,
    /// Total number of images on the host.  Both _tagged_ and _untagged_ (dangling) images are counted.
    #[serde(rename = "Images", skip_serializing_if = "Option::is_none")]
    images: Option<i32>,
    /// Name of the storage driver in use.
    #[serde(rename = "Driver", skip_serializing_if = "Option::is_none")]
    driver: Option<String>,
    /// Information specific to the storage driver, provided as \"label\" / \"value\" pairs.  This information is provided by the storage driver, and formatted in a way consistent with the output of `docker info` on the command line.  <p><br /></p>  > **Note**: The information returned in this field, including the > formatting of values and labels, should not be considered stable, > and may change without notice.
    #[serde(rename = "DriverStatus", skip_serializing_if = "Option::is_none")]
    driver_status: Option<Vec<Vec<String>>>,
    /// Root directory of persistent Docker state.  Defaults to `/var/lib/docker` on Linux, and `C:\\ProgramData\\docker` on Windows.
    #[serde(rename = "DockerRootDir", skip_serializing_if = "Option::is_none")]
    docker_root_dir: Option<String>,
    /// Status information about this node (standalone Swarm API).  <p><br /></p>  > **Note**: The information returned in this field is only propagated > by the Swarm standalone API, and is empty (`null`) when using > built-in swarm mode.
    #[serde(rename = "SystemStatus", skip_serializing_if = "Option::is_none")]
    system_status: Option<Vec<Vec<String>>>,
    #[serde(rename = "Plugins", skip_serializing_if = "Option::is_none")]
    plugins: Option<crate::models::PluginsInfo>,
    /// Indicates if the host has memory limit support enabled.
    #[serde(rename = "MemoryLimit", skip_serializing_if = "Option::is_none")]
    memory_limit: Option<bool>,
    /// Indicates if the host has memory swap limit support enabled.
    #[serde(rename = "SwapLimit", skip_serializing_if = "Option::is_none")]
    swap_limit: Option<bool>,
    /// Indicates if the host has kernel memory limit support enabled.
    #[serde(rename = "KernelMemory", skip_serializing_if = "Option::is_none")]
    kernel_memory: Option<bool>,
    /// Indicates if CPU CFS(Completely Fair Scheduler) period is supported by the host.
    #[serde(rename = "CpuCfsPeriod", skip_serializing_if = "Option::is_none")]
    cpu_cfs_period: Option<bool>,
    /// Indicates if CPU CFS(Completely Fair Scheduler) quota is supported by the host.
    #[serde(rename = "CpuCfsQuota", skip_serializing_if = "Option::is_none")]
    cpu_cfs_quota: Option<bool>,
    /// Indicates if CPU Shares limiting is supported by the host.
    #[serde(rename = "CPUShares", skip_serializing_if = "Option::is_none")]
    cpu_shares: Option<bool>,
    /// Indicates if CPUsets (cpuset.cpus, cpuset.mems) are supported by the host.  See [cpuset(7)](https://www.kernel.org/doc/Documentation/cgroup-v1/cpusets.txt)
    #[serde(rename = "CPUSet", skip_serializing_if = "Option::is_none")]
    cpu_set: Option<bool>,
    /// Indicates if OOM killer disable is supported on the host.
    #[serde(rename = "OomKillDisable", skip_serializing_if = "Option::is_none")]
    oom_kill_disable: Option<bool>,
    /// Indicates IPv4 forwarding is enabled.
    #[serde(rename = "IPv4Forwarding", skip_serializing_if = "Option::is_none")]
    i_pv4_forwarding: Option<bool>,
    /// Indicates if `bridge-nf-call-iptables` is available on the host.
    #[serde(rename = "BridgeNfIptables", skip_serializing_if = "Option::is_none")]
    bridge_nf_iptables: Option<bool>,
    /// Indicates if `bridge-nf-call-ip6tables` is available on the host.
    #[serde(rename = "BridgeNfIp6tables", skip_serializing_if = "Option::is_none")]
    bridge_nf_ip6tables: Option<bool>,
    /// Indicates if the daemon is running in debug-mode / with debug-level logging enabled.
    #[serde(rename = "Debug", skip_serializing_if = "Option::is_none")]
    debug: Option<bool>,
    /// The total number of file Descriptors in use by the daemon process.  This information is only returned if debug-mode is enabled.
    #[serde(rename = "NFd", skip_serializing_if = "Option::is_none")]
    n_fd: Option<i32>,
    /// The  number of goroutines that currently exist.  This information is only returned if debug-mode is enabled.
    #[serde(rename = "NGoroutines", skip_serializing_if = "Option::is_none")]
    n_goroutines: Option<i32>,
    /// Current system-time in [RFC 3339](https://www.ietf.org/rfc/rfc3339.txt) format with nano-seconds.
    #[serde(rename = "SystemTime", skip_serializing_if = "Option::is_none")]
    system_time: Option<String>,
    /// The logging driver to use as a default for new containers.
    #[serde(rename = "LoggingDriver", skip_serializing_if = "Option::is_none")]
    logging_driver: Option<String>,
    /// The driver to use for managing cgroups.
    #[serde(rename = "CgroupDriver", skip_serializing_if = "Option::is_none")]
    cgroup_driver: Option<String>,
    /// Number of event listeners subscribed.
    #[serde(rename = "NEventsListener", skip_serializing_if = "Option::is_none")]
    n_events_listener: Option<i32>,
    /// Kernel version of the host.  On Linux, this information obtained from `uname`. On Windows this information is queried from the <kbd>HKEY_LOCAL_MACHINE\\\\SOFTWARE\\\\Microsoft\\\\Windows NT\\\\CurrentVersion\\\\</kbd> registry value, for example _\"10.0 14393 (14393.1198.amd64fre.rs1_release_sec.170427-1353)\"_.
    #[serde(rename = "KernelVersion", skip_serializing_if = "Option::is_none")]
    kernel_version: Option<String>,
    /// Name of the host's operating system, for example: \"Ubuntu 16.04.2 LTS\" or \"Windows Server 2016 Datacenter\"
    #[serde(rename = "OperatingSystem", skip_serializing_if = "Option::is_none")]
    operating_system: Option<String>,
    /// Generic type of the operating system of the host, as returned by the Go runtime (`GOOS`).  Currently returned values are \"linux\" and \"windows\". A full list of possible values can be found in the [Go documentation](https://golang.org/doc/install/source#environment).
    #[serde(rename = "OSType", skip_serializing_if = "Option::is_none")]
    os_type: Option<String>,
    /// Hardware architecture of the host, as returned by the Go runtime (`GOARCH`).  A full list of possible values can be found in the [Go documentation](https://golang.org/doc/install/source#environment).
    #[serde(rename = "Architecture", skip_serializing_if = "Option::is_none")]
    architecture: Option<String>,
    /// The number of logical CPUs usable by the daemon.  The number of available CPUs is checked by querying the operating system when the daemon starts. Changes to operating system CPU allocation after the daemon is started are not reflected.
    #[serde(rename = "NCPU", skip_serializing_if = "Option::is_none")]
    NCPU: Option<i32>,
    /// Total amount of physical memory available on the host, in kilobytes (kB).
    #[serde(rename = "MemTotal", skip_serializing_if = "Option::is_none")]
    mem_total: Option<i64>,
    /// Address / URL of the index server that is used for image search, and as a default for user authentication for Docker Hub and Docker Cloud.
    #[serde(rename = "IndexServerAddress", skip_serializing_if = "Option::is_none")]
    index_server_address: Option<String>,
    #[serde(rename = "RegistryConfig", skip_serializing_if = "Option::is_none")]
    registry_config: Option<crate::models::RegistryServiceConfig>,
    #[serde(rename = "GenericResources", skip_serializing_if = "Option::is_none")]
    generic_resources: Option<crate::models::GenericResources>,
    /// HTTP-proxy configured for the daemon. This value is obtained from the [`HTTP_PROXY`](https://www.gnu.org/software/wget/manual/html_node/Proxies.html) environment variable.  Containers do not automatically inherit this configuration.
    #[serde(rename = "HttpProxy", skip_serializing_if = "Option::is_none")]
    http_proxy: Option<String>,
    /// HTTPS-proxy configured for the daemon. This value is obtained from the [`HTTPS_PROXY`](https://www.gnu.org/software/wget/manual/html_node/Proxies.html) environment variable.  Containers do not automatically inherit this configuration.
    #[serde(rename = "HttpsProxy", skip_serializing_if = "Option::is_none")]
    https_proxy: Option<String>,
    /// Comma-separated list of domain extensions for which no proxy should be used. This value is obtained from the [`NO_PROXY`](https://www.gnu.org/software/wget/manual/html_node/Proxies.html) environment variable.  Containers do not automatically inherit this configuration.
    #[serde(rename = "NoProxy", skip_serializing_if = "Option::is_none")]
    no_proxy: Option<String>,
    /// Hostname of the host.
    #[serde(rename = "Name", skip_serializing_if = "Option::is_none")]
    name: Option<String>,
    /// User-defined labels (key/value metadata) as set on the daemon.  <p><br /></p>  > **Note**: When part of a Swarm, nodes can both have _daemon_ labels, > set through the daemon configuration, and _node_ labels, set from a > manager node in the Swarm. Node labels are not included in this > field. Node labels can be retrieved using the `/nodes/(id)` endpoint > on a manager node in the Swarm.
    #[serde(rename = "Labels", skip_serializing_if = "Option::is_none")]
    labels: Option<Vec<String>>,
    /// Indicates if experimental features are enabled on the daemon.
    #[serde(rename = "ExperimentalBuild", skip_serializing_if = "Option::is_none")]
    experimental_build: Option<bool>,
    /// Version string of the daemon.  > **Note**: the [standalone Swarm API](https://docs.docker.com/swarm/swarm-api/) > returns the Swarm version instead of the daemon  version, for example > `swarm/1.2.8`.
    #[serde(rename = "ServerVersion", skip_serializing_if = "Option::is_none")]
    server_version: Option<String>,
    /// URL of the distributed storage backend.   The storage backend is used for multihost networking (to store network and endpoint information) and by the node discovery mechanism.  <p><br /></p>  > **Note**: This field is only propagated when using standalone Swarm > mode, and overlay networking using an external k/v store. Overlay > networks with Swarm mode enabled use the built-in raft store, and > this field will be empty.
    #[serde(rename = "ClusterStore", skip_serializing_if = "Option::is_none")]
    cluster_store: Option<String>,
    /// The network endpoint that the Engine advertises for the purpose of node discovery. ClusterAdvertise is a `host:port` combination on which the daemon is reachable by other hosts.  <p><br /></p>  > **Note**: This field is only propagated when using standalone Swarm > mode, and overlay networking using an external k/v store. Overlay > networks with Swarm mode enabled use the built-in raft store, and > this field will be empty.
    #[serde(rename = "ClusterAdvertise", skip_serializing_if = "Option::is_none")]
    cluster_advertise: Option<String>,
    /// List of [OCI compliant](https://github.com/opencontainers/runtime-spec) runtimes configured on the daemon. Keys hold the \"name\" used to reference the runtime.  The Docker daemon relies on an OCI compliant runtime (invoked via the `containerd` daemon) as its interface to the Linux kernel namespaces, cgroups, and SELinux.  The default runtime is `runc`, and automatically configured. Additional runtimes can be configured by the user and will be listed here.
    #[serde(rename = "Runtimes", skip_serializing_if = "Option::is_none")]
    runtimes: Option<::std::collections::HashMap<String, crate::models::Runtime>>,
    /// Name of the default OCI runtime that is used when starting containers.  The default can be overridden per-container at create time.
    #[serde(rename = "DefaultRuntime", skip_serializing_if = "Option::is_none")]
    default_runtime: Option<String>,
    #[serde(rename = "Swarm", skip_serializing_if = "Option::is_none")]
    swarm: Option<crate::models::SwarmInfo>,
    /// Indicates if live restore is enabled.  If enabled, containers are kept running when the daemon is shutdown or upon daemon start if running containers are detected.
    #[serde(rename = "LiveRestoreEnabled", skip_serializing_if = "Option::is_none")]
    live_restore_enabled: Option<bool>,
    /// Represents the isolation technology to use as a default for containers. The supported values are platform-specific.  If no isolation value is specified on daemon start, on Windows client, the default is `hyperv`, and on Windows server, the default is `process`.  This option is currently not used on other platforms.
    #[serde(rename = "Isolation", skip_serializing_if = "Option::is_none")]
    isolation: Option<String>,
    /// Name and, optional, path of the the `docker-init` binary.  If the path is omitted, the daemon searches the host's `$PATH` for the binary and uses the first result.
    #[serde(rename = "InitBinary", skip_serializing_if = "Option::is_none")]
    init_binary: Option<String>,
    #[serde(rename = "ContainerdCommit", skip_serializing_if = "Option::is_none")]
    containerd_commit: Option<crate::models::Commit>,
    #[serde(rename = "RuncCommit", skip_serializing_if = "Option::is_none")]
    runc_commit: Option<crate::models::Commit>,
    #[serde(rename = "InitCommit", skip_serializing_if = "Option::is_none")]
    init_commit: Option<crate::models::Commit>,
    /// List of security features that are enabled on the daemon, such as apparmor, seccomp, SELinux, and user-namespaces (userns).  Additional configuration options for each security feature may be present, and are included as a comma-separated list of key/value pairs.
    #[serde(rename = "SecurityOptions", skip_serializing_if = "Option::is_none")]
    security_options: Option<Vec<String>>,
}

impl SystemInfo {
    pub fn new() -> Self {
        SystemInfo {
            ID: None,
            containers: None,
            containers_running: None,
            containers_paused: None,
            containers_stopped: None,
            images: None,
            driver: None,
            driver_status: None,
            docker_root_dir: None,
            system_status: None,
            plugins: None,
            memory_limit: None,
            swap_limit: None,
            kernel_memory: None,
            cpu_cfs_period: None,
            cpu_cfs_quota: None,
            cpu_shares: None,
            cpu_set: None,
            oom_kill_disable: None,
            i_pv4_forwarding: None,
            bridge_nf_iptables: None,
            bridge_nf_ip6tables: None,
            debug: None,
            n_fd: None,
            n_goroutines: None,
            system_time: None,
            logging_driver: None,
            cgroup_driver: None,
            n_events_listener: None,
            kernel_version: None,
            operating_system: None,
            os_type: None,
            architecture: None,
            NCPU: None,
            mem_total: None,
            index_server_address: None,
            registry_config: None,
            generic_resources: None,
            http_proxy: None,
            https_proxy: None,
            no_proxy: None,
            name: None,
            labels: None,
            experimental_build: None,
            server_version: None,
            cluster_store: None,
            cluster_advertise: None,
            runtimes: None,
            default_runtime: None,
            swarm: None,
            live_restore_enabled: None,
            isolation: None,
            init_binary: None,
            containerd_commit: None,
            runc_commit: None,
            init_commit: None,
            security_options: None,
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

    pub fn set_containers(&mut self, containers: i32) {
        self.containers = Some(containers);
    }

    pub fn with_containers(mut self, containers: i32) -> Self {
        self.containers = Some(containers);
        self
    }

    pub fn containers(&self) -> Option<i32> {
        self.containers
    }

    pub fn reset_containers(&mut self) {
        self.containers = None;
    }

    pub fn set_containers_running(&mut self, containers_running: i32) {
        self.containers_running = Some(containers_running);
    }

    pub fn with_containers_running(mut self, containers_running: i32) -> Self {
        self.containers_running = Some(containers_running);
        self
    }

    pub fn containers_running(&self) -> Option<i32> {
        self.containers_running
    }

    pub fn reset_containers_running(&mut self) {
        self.containers_running = None;
    }

    pub fn set_containers_paused(&mut self, containers_paused: i32) {
        self.containers_paused = Some(containers_paused);
    }

    pub fn with_containers_paused(mut self, containers_paused: i32) -> Self {
        self.containers_paused = Some(containers_paused);
        self
    }

    pub fn containers_paused(&self) -> Option<i32> {
        self.containers_paused
    }

    pub fn reset_containers_paused(&mut self) {
        self.containers_paused = None;
    }

    pub fn set_containers_stopped(&mut self, containers_stopped: i32) {
        self.containers_stopped = Some(containers_stopped);
    }

    pub fn with_containers_stopped(mut self, containers_stopped: i32) -> Self {
        self.containers_stopped = Some(containers_stopped);
        self
    }

    pub fn containers_stopped(&self) -> Option<i32> {
        self.containers_stopped
    }

    pub fn reset_containers_stopped(&mut self) {
        self.containers_stopped = None;
    }

    pub fn set_images(&mut self, images: i32) {
        self.images = Some(images);
    }

    pub fn with_images(mut self, images: i32) -> Self {
        self.images = Some(images);
        self
    }

    pub fn images(&self) -> Option<i32> {
        self.images
    }

    pub fn reset_images(&mut self) {
        self.images = None;
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

    pub fn set_driver_status(&mut self, driver_status: Vec<Vec<String>>) {
        self.driver_status = Some(driver_status);
    }

    pub fn with_driver_status(mut self, driver_status: Vec<Vec<String>>) -> Self {
        self.driver_status = Some(driver_status);
        self
    }

    pub fn driver_status(&self) -> Option<&[Vec<String>]> {
        self.driver_status.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_driver_status(&mut self) {
        self.driver_status = None;
    }

    pub fn set_docker_root_dir(&mut self, docker_root_dir: String) {
        self.docker_root_dir = Some(docker_root_dir);
    }

    pub fn with_docker_root_dir(mut self, docker_root_dir: String) -> Self {
        self.docker_root_dir = Some(docker_root_dir);
        self
    }

    pub fn docker_root_dir(&self) -> Option<&str> {
        self.docker_root_dir.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_docker_root_dir(&mut self) {
        self.docker_root_dir = None;
    }

    pub fn set_system_status(&mut self, system_status: Vec<Vec<String>>) {
        self.system_status = Some(system_status);
    }

    pub fn with_system_status(mut self, system_status: Vec<Vec<String>>) -> Self {
        self.system_status = Some(system_status);
        self
    }

    pub fn system_status(&self) -> Option<&[Vec<String>]> {
        self.system_status.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_system_status(&mut self) {
        self.system_status = None;
    }

    pub fn set_plugins(&mut self, plugins: crate::models::PluginsInfo) {
        self.plugins = Some(plugins);
    }

    pub fn with_plugins(mut self, plugins: crate::models::PluginsInfo) -> Self {
        self.plugins = Some(plugins);
        self
    }

    pub fn plugins(&self) -> Option<&crate::models::PluginsInfo> {
        self.plugins.as_ref()
    }

    pub fn reset_plugins(&mut self) {
        self.plugins = None;
    }

    pub fn set_memory_limit(&mut self, memory_limit: bool) {
        self.memory_limit = Some(memory_limit);
    }

    pub fn with_memory_limit(mut self, memory_limit: bool) -> Self {
        self.memory_limit = Some(memory_limit);
        self
    }

    pub fn memory_limit(&self) -> Option<&bool> {
        self.memory_limit.as_ref()
    }

    pub fn reset_memory_limit(&mut self) {
        self.memory_limit = None;
    }

    pub fn set_swap_limit(&mut self, swap_limit: bool) {
        self.swap_limit = Some(swap_limit);
    }

    pub fn with_swap_limit(mut self, swap_limit: bool) -> Self {
        self.swap_limit = Some(swap_limit);
        self
    }

    pub fn swap_limit(&self) -> Option<&bool> {
        self.swap_limit.as_ref()
    }

    pub fn reset_swap_limit(&mut self) {
        self.swap_limit = None;
    }

    pub fn set_kernel_memory(&mut self, kernel_memory: bool) {
        self.kernel_memory = Some(kernel_memory);
    }

    pub fn with_kernel_memory(mut self, kernel_memory: bool) -> Self {
        self.kernel_memory = Some(kernel_memory);
        self
    }

    pub fn kernel_memory(&self) -> Option<&bool> {
        self.kernel_memory.as_ref()
    }

    pub fn reset_kernel_memory(&mut self) {
        self.kernel_memory = None;
    }

    pub fn set_cpu_cfs_period(&mut self, cpu_cfs_period: bool) {
        self.cpu_cfs_period = Some(cpu_cfs_period);
    }

    pub fn with_cpu_cfs_period(mut self, cpu_cfs_period: bool) -> Self {
        self.cpu_cfs_period = Some(cpu_cfs_period);
        self
    }

    pub fn cpu_cfs_period(&self) -> Option<&bool> {
        self.cpu_cfs_period.as_ref()
    }

    pub fn reset_cpu_cfs_period(&mut self) {
        self.cpu_cfs_period = None;
    }

    pub fn set_cpu_cfs_quota(&mut self, cpu_cfs_quota: bool) {
        self.cpu_cfs_quota = Some(cpu_cfs_quota);
    }

    pub fn with_cpu_cfs_quota(mut self, cpu_cfs_quota: bool) -> Self {
        self.cpu_cfs_quota = Some(cpu_cfs_quota);
        self
    }

    pub fn cpu_cfs_quota(&self) -> Option<&bool> {
        self.cpu_cfs_quota.as_ref()
    }

    pub fn reset_cpu_cfs_quota(&mut self) {
        self.cpu_cfs_quota = None;
    }

    pub fn set_cpu_shares(&mut self, cpu_shares: bool) {
        self.cpu_shares = Some(cpu_shares);
    }

    pub fn with_cpu_shares(mut self, cpu_shares: bool) -> Self {
        self.cpu_shares = Some(cpu_shares);
        self
    }

    pub fn cpu_shares(&self) -> Option<&bool> {
        self.cpu_shares.as_ref()
    }

    pub fn reset_cpu_shares(&mut self) {
        self.cpu_shares = None;
    }

    pub fn set_cpu_set(&mut self, cpu_set: bool) {
        self.cpu_set = Some(cpu_set);
    }

    pub fn with_cpu_set(mut self, cpu_set: bool) -> Self {
        self.cpu_set = Some(cpu_set);
        self
    }

    pub fn cpu_set(&self) -> Option<&bool> {
        self.cpu_set.as_ref()
    }

    pub fn reset_cpu_set(&mut self) {
        self.cpu_set = None;
    }

    pub fn set_oom_kill_disable(&mut self, oom_kill_disable: bool) {
        self.oom_kill_disable = Some(oom_kill_disable);
    }

    pub fn with_oom_kill_disable(mut self, oom_kill_disable: bool) -> Self {
        self.oom_kill_disable = Some(oom_kill_disable);
        self
    }

    pub fn oom_kill_disable(&self) -> Option<&bool> {
        self.oom_kill_disable.as_ref()
    }

    pub fn reset_oom_kill_disable(&mut self) {
        self.oom_kill_disable = None;
    }

    pub fn set_i_pv4_forwarding(&mut self, i_pv4_forwarding: bool) {
        self.i_pv4_forwarding = Some(i_pv4_forwarding);
    }

    pub fn with_i_pv4_forwarding(mut self, i_pv4_forwarding: bool) -> Self {
        self.i_pv4_forwarding = Some(i_pv4_forwarding);
        self
    }

    pub fn i_pv4_forwarding(&self) -> Option<&bool> {
        self.i_pv4_forwarding.as_ref()
    }

    pub fn reset_i_pv4_forwarding(&mut self) {
        self.i_pv4_forwarding = None;
    }

    pub fn set_bridge_nf_iptables(&mut self, bridge_nf_iptables: bool) {
        self.bridge_nf_iptables = Some(bridge_nf_iptables);
    }

    pub fn with_bridge_nf_iptables(mut self, bridge_nf_iptables: bool) -> Self {
        self.bridge_nf_iptables = Some(bridge_nf_iptables);
        self
    }

    pub fn bridge_nf_iptables(&self) -> Option<&bool> {
        self.bridge_nf_iptables.as_ref()
    }

    pub fn reset_bridge_nf_iptables(&mut self) {
        self.bridge_nf_iptables = None;
    }

    pub fn set_bridge_nf_ip6tables(&mut self, bridge_nf_ip6tables: bool) {
        self.bridge_nf_ip6tables = Some(bridge_nf_ip6tables);
    }

    pub fn with_bridge_nf_ip6tables(mut self, bridge_nf_ip6tables: bool) -> Self {
        self.bridge_nf_ip6tables = Some(bridge_nf_ip6tables);
        self
    }

    pub fn bridge_nf_ip6tables(&self) -> Option<&bool> {
        self.bridge_nf_ip6tables.as_ref()
    }

    pub fn reset_bridge_nf_ip6tables(&mut self) {
        self.bridge_nf_ip6tables = None;
    }

    pub fn set_debug(&mut self, debug: bool) {
        self.debug = Some(debug);
    }

    pub fn with_debug(mut self, debug: bool) -> Self {
        self.debug = Some(debug);
        self
    }

    pub fn debug(&self) -> Option<&bool> {
        self.debug.as_ref()
    }

    pub fn reset_debug(&mut self) {
        self.debug = None;
    }

    pub fn set_n_fd(&mut self, n_fd: i32) {
        self.n_fd = Some(n_fd);
    }

    pub fn with_n_fd(mut self, n_fd: i32) -> Self {
        self.n_fd = Some(n_fd);
        self
    }

    pub fn n_fd(&self) -> Option<i32> {
        self.n_fd
    }

    pub fn reset_n_fd(&mut self) {
        self.n_fd = None;
    }

    pub fn set_n_goroutines(&mut self, n_goroutines: i32) {
        self.n_goroutines = Some(n_goroutines);
    }

    pub fn with_n_goroutines(mut self, n_goroutines: i32) -> Self {
        self.n_goroutines = Some(n_goroutines);
        self
    }

    pub fn n_goroutines(&self) -> Option<i32> {
        self.n_goroutines
    }

    pub fn reset_n_goroutines(&mut self) {
        self.n_goroutines = None;
    }

    pub fn set_system_time(&mut self, system_time: String) {
        self.system_time = Some(system_time);
    }

    pub fn with_system_time(mut self, system_time: String) -> Self {
        self.system_time = Some(system_time);
        self
    }

    pub fn system_time(&self) -> Option<&str> {
        self.system_time.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_system_time(&mut self) {
        self.system_time = None;
    }

    pub fn set_logging_driver(&mut self, logging_driver: String) {
        self.logging_driver = Some(logging_driver);
    }

    pub fn with_logging_driver(mut self, logging_driver: String) -> Self {
        self.logging_driver = Some(logging_driver);
        self
    }

    pub fn logging_driver(&self) -> Option<&str> {
        self.logging_driver.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_logging_driver(&mut self) {
        self.logging_driver = None;
    }

    pub fn set_cgroup_driver(&mut self, cgroup_driver: String) {
        self.cgroup_driver = Some(cgroup_driver);
    }

    pub fn with_cgroup_driver(mut self, cgroup_driver: String) -> Self {
        self.cgroup_driver = Some(cgroup_driver);
        self
    }

    pub fn cgroup_driver(&self) -> Option<&str> {
        self.cgroup_driver.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_cgroup_driver(&mut self) {
        self.cgroup_driver = None;
    }

    pub fn set_n_events_listener(&mut self, n_events_listener: i32) {
        self.n_events_listener = Some(n_events_listener);
    }

    pub fn with_n_events_listener(mut self, n_events_listener: i32) -> Self {
        self.n_events_listener = Some(n_events_listener);
        self
    }

    pub fn n_events_listener(&self) -> Option<i32> {
        self.n_events_listener
    }

    pub fn reset_n_events_listener(&mut self) {
        self.n_events_listener = None;
    }

    pub fn set_kernel_version(&mut self, kernel_version: String) {
        self.kernel_version = Some(kernel_version);
    }

    pub fn with_kernel_version(mut self, kernel_version: String) -> Self {
        self.kernel_version = Some(kernel_version);
        self
    }

    pub fn kernel_version(&self) -> Option<&str> {
        self.kernel_version.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_kernel_version(&mut self) {
        self.kernel_version = None;
    }

    pub fn set_operating_system(&mut self, operating_system: String) {
        self.operating_system = Some(operating_system);
    }

    pub fn with_operating_system(mut self, operating_system: String) -> Self {
        self.operating_system = Some(operating_system);
        self
    }

    pub fn operating_system(&self) -> Option<&str> {
        self.operating_system.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_operating_system(&mut self) {
        self.operating_system = None;
    }

    pub fn set_os_type(&mut self, os_type: String) {
        self.os_type = Some(os_type);
    }

    pub fn with_os_type(mut self, os_type: String) -> Self {
        self.os_type = Some(os_type);
        self
    }

    pub fn os_type(&self) -> Option<&str> {
        self.os_type.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_os_type(&mut self) {
        self.os_type = None;
    }

    pub fn set_architecture(&mut self, architecture: String) {
        self.architecture = Some(architecture);
    }

    pub fn with_architecture(mut self, architecture: String) -> Self {
        self.architecture = Some(architecture);
        self
    }

    pub fn architecture(&self) -> Option<&str> {
        self.architecture.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_architecture(&mut self) {
        self.architecture = None;
    }

    pub fn set_NCPU(&mut self, NCPU: i32) {
        self.NCPU = Some(NCPU);
    }

    pub fn with_NCPU(mut self, NCPU: i32) -> Self {
        self.NCPU = Some(NCPU);
        self
    }

    pub fn NCPU(&self) -> Option<i32> {
        self.NCPU
    }

    pub fn reset_NCPU(&mut self) {
        self.NCPU = None;
    }

    pub fn set_mem_total(&mut self, mem_total: i64) {
        self.mem_total = Some(mem_total);
    }

    pub fn with_mem_total(mut self, mem_total: i64) -> Self {
        self.mem_total = Some(mem_total);
        self
    }

    pub fn mem_total(&self) -> Option<i64> {
        self.mem_total
    }

    pub fn reset_mem_total(&mut self) {
        self.mem_total = None;
    }

    pub fn set_index_server_address(&mut self, index_server_address: String) {
        self.index_server_address = Some(index_server_address);
    }

    pub fn with_index_server_address(mut self, index_server_address: String) -> Self {
        self.index_server_address = Some(index_server_address);
        self
    }

    pub fn index_server_address(&self) -> Option<&str> {
        self.index_server_address.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_index_server_address(&mut self) {
        self.index_server_address = None;
    }

    pub fn set_registry_config(&mut self, registry_config: crate::models::RegistryServiceConfig) {
        self.registry_config = Some(registry_config);
    }

    pub fn with_registry_config(
        mut self,
        registry_config: crate::models::RegistryServiceConfig,
    ) -> Self {
        self.registry_config = Some(registry_config);
        self
    }

    pub fn registry_config(&self) -> Option<&crate::models::RegistryServiceConfig> {
        self.registry_config.as_ref()
    }

    pub fn reset_registry_config(&mut self) {
        self.registry_config = None;
    }

    pub fn set_generic_resources(&mut self, generic_resources: crate::models::GenericResources) {
        self.generic_resources = Some(generic_resources);
    }

    pub fn with_generic_resources(
        mut self,
        generic_resources: crate::models::GenericResources,
    ) -> Self {
        self.generic_resources = Some(generic_resources);
        self
    }

    pub fn generic_resources(&self) -> Option<&crate::models::GenericResources> {
        self.generic_resources.as_ref()
    }

    pub fn reset_generic_resources(&mut self) {
        self.generic_resources = None;
    }

    pub fn set_http_proxy(&mut self, http_proxy: String) {
        self.http_proxy = Some(http_proxy);
    }

    pub fn with_http_proxy(mut self, http_proxy: String) -> Self {
        self.http_proxy = Some(http_proxy);
        self
    }

    pub fn http_proxy(&self) -> Option<&str> {
        self.http_proxy.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_http_proxy(&mut self) {
        self.http_proxy = None;
    }

    pub fn set_https_proxy(&mut self, https_proxy: String) {
        self.https_proxy = Some(https_proxy);
    }

    pub fn with_https_proxy(mut self, https_proxy: String) -> Self {
        self.https_proxy = Some(https_proxy);
        self
    }

    pub fn https_proxy(&self) -> Option<&str> {
        self.https_proxy.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_https_proxy(&mut self) {
        self.https_proxy = None;
    }

    pub fn set_no_proxy(&mut self, no_proxy: String) {
        self.no_proxy = Some(no_proxy);
    }

    pub fn with_no_proxy(mut self, no_proxy: String) -> Self {
        self.no_proxy = Some(no_proxy);
        self
    }

    pub fn no_proxy(&self) -> Option<&str> {
        self.no_proxy.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_no_proxy(&mut self) {
        self.no_proxy = None;
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

    pub fn set_labels(&mut self, labels: Vec<String>) {
        self.labels = Some(labels);
    }

    pub fn with_labels(mut self, labels: Vec<String>) -> Self {
        self.labels = Some(labels);
        self
    }

    pub fn labels(&self) -> Option<&[String]> {
        self.labels.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_labels(&mut self) {
        self.labels = None;
    }

    pub fn set_experimental_build(&mut self, experimental_build: bool) {
        self.experimental_build = Some(experimental_build);
    }

    pub fn with_experimental_build(mut self, experimental_build: bool) -> Self {
        self.experimental_build = Some(experimental_build);
        self
    }

    pub fn experimental_build(&self) -> Option<&bool> {
        self.experimental_build.as_ref()
    }

    pub fn reset_experimental_build(&mut self) {
        self.experimental_build = None;
    }

    pub fn set_server_version(&mut self, server_version: String) {
        self.server_version = Some(server_version);
    }

    pub fn with_server_version(mut self, server_version: String) -> Self {
        self.server_version = Some(server_version);
        self
    }

    pub fn server_version(&self) -> Option<&str> {
        self.server_version.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_server_version(&mut self) {
        self.server_version = None;
    }

    pub fn set_cluster_store(&mut self, cluster_store: String) {
        self.cluster_store = Some(cluster_store);
    }

    pub fn with_cluster_store(mut self, cluster_store: String) -> Self {
        self.cluster_store = Some(cluster_store);
        self
    }

    pub fn cluster_store(&self) -> Option<&str> {
        self.cluster_store.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_cluster_store(&mut self) {
        self.cluster_store = None;
    }

    pub fn set_cluster_advertise(&mut self, cluster_advertise: String) {
        self.cluster_advertise = Some(cluster_advertise);
    }

    pub fn with_cluster_advertise(mut self, cluster_advertise: String) -> Self {
        self.cluster_advertise = Some(cluster_advertise);
        self
    }

    pub fn cluster_advertise(&self) -> Option<&str> {
        self.cluster_advertise.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_cluster_advertise(&mut self) {
        self.cluster_advertise = None;
    }

    pub fn set_runtimes(
        &mut self,
        runtimes: ::std::collections::HashMap<String, crate::models::Runtime>,
    ) {
        self.runtimes = Some(runtimes);
    }

    pub fn with_runtimes(
        mut self,
        runtimes: ::std::collections::HashMap<String, crate::models::Runtime>,
    ) -> Self {
        self.runtimes = Some(runtimes);
        self
    }

    pub fn runtimes(&self) -> Option<&::std::collections::HashMap<String, crate::models::Runtime>> {
        self.runtimes.as_ref()
    }

    pub fn reset_runtimes(&mut self) {
        self.runtimes = None;
    }

    pub fn set_default_runtime(&mut self, default_runtime: String) {
        self.default_runtime = Some(default_runtime);
    }

    pub fn with_default_runtime(mut self, default_runtime: String) -> Self {
        self.default_runtime = Some(default_runtime);
        self
    }

    pub fn default_runtime(&self) -> Option<&str> {
        self.default_runtime.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_default_runtime(&mut self) {
        self.default_runtime = None;
    }

    pub fn set_swarm(&mut self, swarm: crate::models::SwarmInfo) {
        self.swarm = Some(swarm);
    }

    pub fn with_swarm(mut self, swarm: crate::models::SwarmInfo) -> Self {
        self.swarm = Some(swarm);
        self
    }

    pub fn swarm(&self) -> Option<&crate::models::SwarmInfo> {
        self.swarm.as_ref()
    }

    pub fn reset_swarm(&mut self) {
        self.swarm = None;
    }

    pub fn set_live_restore_enabled(&mut self, live_restore_enabled: bool) {
        self.live_restore_enabled = Some(live_restore_enabled);
    }

    pub fn with_live_restore_enabled(mut self, live_restore_enabled: bool) -> Self {
        self.live_restore_enabled = Some(live_restore_enabled);
        self
    }

    pub fn live_restore_enabled(&self) -> Option<&bool> {
        self.live_restore_enabled.as_ref()
    }

    pub fn reset_live_restore_enabled(&mut self) {
        self.live_restore_enabled = None;
    }

    pub fn set_isolation(&mut self, isolation: String) {
        self.isolation = Some(isolation);
    }

    pub fn with_isolation(mut self, isolation: String) -> Self {
        self.isolation = Some(isolation);
        self
    }

    pub fn isolation(&self) -> Option<&str> {
        self.isolation.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_isolation(&mut self) {
        self.isolation = None;
    }

    pub fn set_init_binary(&mut self, init_binary: String) {
        self.init_binary = Some(init_binary);
    }

    pub fn with_init_binary(mut self, init_binary: String) -> Self {
        self.init_binary = Some(init_binary);
        self
    }

    pub fn init_binary(&self) -> Option<&str> {
        self.init_binary.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_init_binary(&mut self) {
        self.init_binary = None;
    }

    pub fn set_containerd_commit(&mut self, containerd_commit: crate::models::Commit) {
        self.containerd_commit = Some(containerd_commit);
    }

    pub fn with_containerd_commit(mut self, containerd_commit: crate::models::Commit) -> Self {
        self.containerd_commit = Some(containerd_commit);
        self
    }

    pub fn containerd_commit(&self) -> Option<&crate::models::Commit> {
        self.containerd_commit.as_ref()
    }

    pub fn reset_containerd_commit(&mut self) {
        self.containerd_commit = None;
    }

    pub fn set_runc_commit(&mut self, runc_commit: crate::models::Commit) {
        self.runc_commit = Some(runc_commit);
    }

    pub fn with_runc_commit(mut self, runc_commit: crate::models::Commit) -> Self {
        self.runc_commit = Some(runc_commit);
        self
    }

    pub fn runc_commit(&self) -> Option<&crate::models::Commit> {
        self.runc_commit.as_ref()
    }

    pub fn reset_runc_commit(&mut self) {
        self.runc_commit = None;
    }

    pub fn set_init_commit(&mut self, init_commit: crate::models::Commit) {
        self.init_commit = Some(init_commit);
    }

    pub fn with_init_commit(mut self, init_commit: crate::models::Commit) -> Self {
        self.init_commit = Some(init_commit);
        self
    }

    pub fn init_commit(&self) -> Option<&crate::models::Commit> {
        self.init_commit.as_ref()
    }

    pub fn reset_init_commit(&mut self) {
        self.init_commit = None;
    }

    pub fn set_security_options(&mut self, security_options: Vec<String>) {
        self.security_options = Some(security_options);
    }

    pub fn with_security_options(mut self, security_options: Vec<String>) -> Self {
        self.security_options = Some(security_options);
        self
    }

    pub fn security_options(&self) -> Option<&[String]> {
        self.security_options.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_security_options(&mut self) {
        self.security_options = None;
    }
}
