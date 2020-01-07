/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// HostConfig : Container configuration that depends on the host we are running on

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
pub struct HostConfig {
    // /// An integer value representing this container's relative CPU weight versus other containers.
    // #[serde(rename = "CpuShares", skip_serializing_if = "Option::is_none")]
    // cpu_shares: Option<i32>,
    /// Memory limit in bytes.
    #[serde(rename = "Memory", skip_serializing_if = "Option::is_none")]
    memory: Option<i64>,
    // /// Path to `cgroups` under which the container's `cgroup` is created. If the path is not absolute, the path is considered to be relative to the `cgroups` path of the init process. Cgroups are created if they do not already exist.
    // #[serde(rename = "CgroupParent", skip_serializing_if = "Option::is_none")]
    // cgroup_parent: Option<String>,
    // /// Block IO weight (relative weight).
    // #[serde(rename = "BlkioWeight", skip_serializing_if = "Option::is_none")]
    // blkio_weight: Option<i32>,
    // /// Block IO weight (relative device weight) in the form `[{\"Path\": \"device_path\", \"Weight\": weight}]`.
    // #[serde(rename = "BlkioWeightDevice", skip_serializing_if = "Option::is_none")]
    // blkio_weight_device: Option<Vec<crate::models::ResourcesBlkioWeightDevice>>,
    // /// Limit read rate (bytes per second) from a device, in the form `[{\"Path\": \"device_path\", \"Rate\": rate}]`.
    // #[serde(rename = "BlkioDeviceReadBps", skip_serializing_if = "Option::is_none")]
    // blkio_device_read_bps: Option<Vec<crate::models::ThrottleDevice>>,
    // /// Limit write rate (bytes per second) to a device, in the form `[{\"Path\": \"device_path\", \"Rate\": rate}]`.
    // #[serde(
    //     rename = "BlkioDeviceWriteBps",
    //     skip_serializing_if = "Option::is_none"
    // )]
    // blkio_device_write_bps: Option<Vec<crate::models::ThrottleDevice>>,
    // /// Limit read rate (IO per second) from a device, in the form `[{\"Path\": \"device_path\", \"Rate\": rate}]`.
    // #[serde(
    //     rename = "BlkioDeviceReadIOps",
    //     skip_serializing_if = "Option::is_none"
    // )]
    // blkio_device_read_i_ops: Option<Vec<crate::models::ThrottleDevice>>,
    // /// Limit write rate (IO per second) to a device, in the form `[{\"Path\": \"device_path\", \"Rate\": rate}]`.
    // #[serde(
    //     rename = "BlkioDeviceWriteIOps",
    //     skip_serializing_if = "Option::is_none"
    // )]
    // blkio_device_write_i_ops: Option<Vec<crate::models::ThrottleDevice>>,
    // /// The length of a CPU period in microseconds.
    // #[serde(rename = "CpuPeriod", skip_serializing_if = "Option::is_none")]
    // cpu_period: Option<i64>,
    // /// Microseconds of CPU time that the container can get in a CPU period.
    // #[serde(rename = "CpuQuota", skip_serializing_if = "Option::is_none")]
    // cpu_quota: Option<i64>,
    // /// The length of a CPU real-time period in microseconds. Set to 0 to allocate no time allocated to real-time tasks.
    // #[serde(rename = "CpuRealtimePeriod", skip_serializing_if = "Option::is_none")]
    // cpu_realtime_period: Option<i64>,
    // /// The length of a CPU real-time runtime in microseconds. Set to 0 to allocate no time allocated to real-time tasks.
    // #[serde(rename = "CpuRealtimeRuntime", skip_serializing_if = "Option::is_none")]
    // cpu_realtime_runtime: Option<i64>,
    // /// CPUs in which to allow execution (e.g., `0-3`, `0,1`)
    // #[serde(rename = "CpusetCpus", skip_serializing_if = "Option::is_none")]
    // cpuset_cpus: Option<String>,
    // /// Memory nodes (MEMs) in which to allow execution (0-3, 0,1). Only effective on NUMA systems.
    // #[serde(rename = "CpusetMems", skip_serializing_if = "Option::is_none")]
    // cpuset_mems: Option<String>,
    // /// A list of devices to add to the container.
    // #[serde(rename = "Devices", skip_serializing_if = "Option::is_none")]
    // devices: Option<Vec<crate::models::DeviceMapping>>,
    // /// a list of cgroup rules to apply to the container
    // #[serde(rename = "DeviceCgroupRules", skip_serializing_if = "Option::is_none")]
    // device_cgroup_rules: Option<Vec<String>>,
    // /// Disk limit (in bytes).
    // #[serde(rename = "DiskQuota", skip_serializing_if = "Option::is_none")]
    // disk_quota: Option<i64>,
    // /// Kernel memory limit in bytes.
    // #[serde(rename = "KernelMemory", skip_serializing_if = "Option::is_none")]
    // kernel_memory: Option<i64>,
    // /// Memory soft limit in bytes.
    // #[serde(rename = "MemoryReservation", skip_serializing_if = "Option::is_none")]
    // memory_reservation: Option<i64>,
    // /// Total memory limit (memory + swap). Set as `-1` to enable unlimited swap.
    // #[serde(rename = "MemorySwap", skip_serializing_if = "Option::is_none")]
    // memory_swap: Option<i64>,
    // /// Tune a container's memory swappiness behavior. Accepts an integer between 0 and 100.
    // #[serde(rename = "MemorySwappiness", skip_serializing_if = "Option::is_none")]
    // memory_swappiness: Option<i64>,
    // /// CPU quota in units of 10<sup>-9</sup> CPUs.
    // #[serde(rename = "NanoCPUs", skip_serializing_if = "Option::is_none")]
    // nano_cp_us: Option<i64>,
    // /// Disable OOM Killer for the container.
    // #[serde(rename = "OomKillDisable", skip_serializing_if = "Option::is_none")]
    // oom_kill_disable: Option<bool>,
    // /// Tune a container's pids limit. Set -1 for unlimited.
    // #[serde(rename = "PidsLimit", skip_serializing_if = "Option::is_none")]
    // pids_limit: Option<i64>,
    // /// A list of resource limits to set in the container. For example: `{\"Name\": \"nofile\", \"Soft\": 1024, \"Hard\": 2048}`\"
    // #[serde(rename = "Ulimits", skip_serializing_if = "Option::is_none")]
    // ulimits: Option<Vec<crate::models::ResourcesUlimits>>,
    // /// The number of usable CPUs (Windows only).  On Windows Server containers, the processor resource controls are mutually exclusive. The order of precedence is `CPUCount` first, then `CPUShares`, and `CPUPercent` last.
    // #[serde(rename = "CpuCount", skip_serializing_if = "Option::is_none")]
    // cpu_count: Option<i64>,
    // /// The usable percentage of the available CPUs (Windows only).  On Windows Server containers, the processor resource controls are mutually exclusive. The order of precedence is `CPUCount` first, then `CPUShares`, and `CPUPercent` last.
    // #[serde(rename = "CpuPercent", skip_serializing_if = "Option::is_none")]
    // cpu_percent: Option<i64>,
    // /// Maximum IOps for the container system drive (Windows only)
    // #[serde(rename = "IOMaximumIOps", skip_serializing_if = "Option::is_none")]
    // io_maximum_i_ops: Option<i64>,
    // /// Maximum IO in bytes per second for the container system drive (Windows only)
    // #[serde(rename = "IOMaximumBandwidth", skip_serializing_if = "Option::is_none")]
    // io_maximum_bandwidth: Option<i64>,
    /// A list of volume bindings for this container. Each volume binding is a string in one of these forms:  - `host-src:container-dest` to bind-mount a host path into the container. Both `host-src`, and `container-dest` must be an _absolute_ path. - `host-src:container-dest:ro` to make the bind mount read-only inside the container. Both `host-src`, and `container-dest` must be an _absolute_ path. - `volume-name:container-dest` to bind-mount a volume managed by a volume driver into the container. `container-dest` must be an _absolute_ path. - `volume-name:container-dest:ro` to mount the volume read-only inside the container.  `container-dest` must be an _absolute_ path.
    #[serde(rename = "Binds", skip_serializing_if = "Option::is_none")]
    binds: Option<Vec<String>>,
    // /// Path to a file where the container ID is written
    // #[serde(rename = "ContainerIDFile", skip_serializing_if = "Option::is_none")]
    // container_id_file: Option<String>,
    // #[serde(rename = "LogConfig", skip_serializing_if = "Option::is_none")]
    // log_config: Option<crate::models::HostConfigLogConfig>,
    // /// Network mode to use for this container. Supported standard values are: `bridge`, `host`, `none`, and `container:<name|id>`. Any other value is taken as a custom network's name to which this container should connect to.
    // #[serde(rename = "NetworkMode", skip_serializing_if = "Option::is_none")]
    // network_mode: Option<String>,
    /// A map of exposed container ports and the host port they should map to.
    #[serde(rename = "PortBindings", skip_serializing_if = "Option::is_none")]
    port_bindings:
        Option<::std::collections::HashMap<String, Vec<crate::models::HostConfigPortBindings>>>,
    // #[serde(rename = "RestartPolicy", skip_serializing_if = "Option::is_none")]
    // restart_policy: Option<crate::models::RestartPolicy>,
    // /// Automatically remove the container when the container's process exits. This has no effect if `RestartPolicy` is set.
    // #[serde(rename = "AutoRemove", skip_serializing_if = "Option::is_none")]
    // auto_remove: Option<bool>,
    // /// Driver that this container uses to mount volumes.
    // #[serde(rename = "VolumeDriver", skip_serializing_if = "Option::is_none")]
    // volume_driver: Option<String>,
    // /// A list of volumes to inherit from another container, specified in the form `<container name>[:<ro|rw>]`.
    // #[serde(rename = "VolumesFrom", skip_serializing_if = "Option::is_none")]
    // volumes_from: Option<Vec<String>>,
    /// Specification for mounts to be added to the container.
    #[serde(rename = "Mounts", skip_serializing_if = "Option::is_none")]
    mounts: Option<Vec<crate::models::Mount>>,
    // /// A list of kernel capabilities to add to the container.
    // #[serde(rename = "CapAdd", skip_serializing_if = "Option::is_none")]
    // cap_add: Option<Vec<String>>,
    // /// A list of kernel capabilities to drop from the container.
    // #[serde(rename = "CapDrop", skip_serializing_if = "Option::is_none")]
    // cap_drop: Option<Vec<String>>,
    // /// A list of DNS servers for the container to use.
    // #[serde(rename = "Dns", skip_serializing_if = "Option::is_none")]
    // dns: Option<Vec<String>>,
    // /// A list of DNS options.
    // #[serde(rename = "DnsOptions", skip_serializing_if = "Option::is_none")]
    // dns_options: Option<Vec<String>>,
    // /// A list of DNS search domains.
    // #[serde(rename = "DnsSearch", skip_serializing_if = "Option::is_none")]
    // dns_search: Option<Vec<String>>,
    // /// A list of hostnames/IP mappings to add to the container's `/etc/hosts` file. Specified in the form `[\"hostname:IP\"]`.
    // #[serde(rename = "ExtraHosts", skip_serializing_if = "Option::is_none")]
    // extra_hosts: Option<Vec<String>>,
    // /// A list of additional groups that the container process will run as.
    // #[serde(rename = "GroupAdd", skip_serializing_if = "Option::is_none")]
    // group_add: Option<Vec<String>>,
    // /// IPC sharing mode for the container. Possible values are:  - `\"none\"`: own private IPC namespace, with /dev/shm not mounted - `\"private\"`: own private IPC namespace - `\"shareable\"`: own private IPC namespace, with a possibility to share it with other containers - `\"container:<name|id>\"`: join another (shareable) container's IPC namespace - `\"host\"`: use the host system's IPC namespace  If not specified, daemon default is used, which can either be `\"private\"` or `\"shareable\"`, depending on daemon version and configuration.
    // #[serde(rename = "IpcMode", skip_serializing_if = "Option::is_none")]
    // ipc_mode: Option<String>,
    // /// Cgroup to use for the container.
    // #[serde(rename = "Cgroup", skip_serializing_if = "Option::is_none")]
    // cgroup: Option<String>,
    // /// A list of links for the container in the form `container_name:alias`.
    // #[serde(rename = "Links", skip_serializing_if = "Option::is_none")]
    // links: Option<Vec<String>>,
    // /// An integer value containing the score given to the container in order to tune OOM killer preferences.
    // #[serde(rename = "OomScoreAdj", skip_serializing_if = "Option::is_none")]
    // oom_score_adj: Option<i32>,
    // /// Set the PID (Process) Namespace mode for the container. It can be either:  - `\"container:<name|id>\"`: joins another container's PID namespace - `\"host\"`: use the host's PID namespace inside the container
    // #[serde(rename = "PidMode", skip_serializing_if = "Option::is_none")]
    // pid_mode: Option<String>,
    /// Gives the container full access to the host.
    #[serde(rename = "Privileged", skip_serializing_if = "Option::is_none")]
    privileged: Option<bool>,
    // /// Allocates a random host port for all of a container's exposed ports.
    // #[serde(rename = "PublishAllPorts", skip_serializing_if = "Option::is_none")]
    // publish_all_ports: Option<bool>,
    // /// Mount the container's root filesystem as read only.
    // #[serde(rename = "ReadonlyRootfs", skip_serializing_if = "Option::is_none")]
    // readonly_rootfs: Option<bool>,
    // /// A list of string values to customize labels for MLS systems, such as SELinux.
    // #[serde(rename = "SecurityOpt", skip_serializing_if = "Option::is_none")]
    // security_opt: Option<Vec<String>>,
    // /// Storage driver options for this container, in the form `{\"size\": \"120G\"}`.
    // #[serde(rename = "StorageOpt", skip_serializing_if = "Option::is_none")]
    // storage_opt: Option<::std::collections::HashMap<String, String>>,
    // /// A map of container directories which should be replaced by tmpfs mounts, and their corresponding mount options. For example: `{ \"/run\": \"rw,noexec,nosuid,size=65536k\" }`.
    // #[serde(rename = "Tmpfs", skip_serializing_if = "Option::is_none")]
    // tmpfs: Option<::std::collections::HashMap<String, String>>,
    // /// UTS namespace to use for the container.
    // #[serde(rename = "UTSMode", skip_serializing_if = "Option::is_none")]
    // uts_mode: Option<String>,
    // /// Sets the usernamespace mode for the container when usernamespace remapping option is enabled.
    // #[serde(rename = "UsernsMode", skip_serializing_if = "Option::is_none")]
    // userns_mode: Option<String>,
    // /// Size of `/dev/shm` in bytes. If omitted, the system uses 64MB.
    // #[serde(rename = "ShmSize", skip_serializing_if = "Option::is_none")]
    // shm_size: Option<i64>,
    // /// A list of kernel parameters (sysctls) to set in the container. For example: `{\"net.ipv4.ip_forward\": \"1\"}`
    // #[serde(rename = "Sysctls", skip_serializing_if = "Option::is_none")]
    // sysctls: Option<::std::collections::HashMap<String, String>>,
    // /// Runtime to use with this container.
    // #[serde(rename = "Runtime", skip_serializing_if = "Option::is_none")]
    // runtime: Option<String>,
    // /// Initial console size, as an `[height, width]` array. (Windows only)
    // #[serde(rename = "ConsoleSize", skip_serializing_if = "Option::is_none")]
    // console_size: Option<Vec<i32>>,
    // /// Isolation technology of the container. (Windows only)
    // #[serde(rename = "Isolation", skip_serializing_if = "Option::is_none")]
    // isolation: Option<String>,
    #[serde(flatten)]
    other_properties: std::collections::HashMap<String, serde_json::Value>,
}

impl HostConfig {
    /// Container configuration that depends on the host we are running on
    pub fn new() -> Self {
        HostConfig {
            // cpu_shares: None,
            memory: None,
            // cgroup_parent: None,
            // blkio_weight: None,
            // blkio_weight_device: None,
            // blkio_device_read_bps: None,
            // blkio_device_write_bps: None,
            // blkio_device_read_i_ops: None,
            // blkio_device_write_i_ops: None,
            // cpu_period: None,
            // cpu_quota: None,
            // cpu_realtime_period: None,
            // cpu_realtime_runtime: None,
            // cpuset_cpus: None,
            // cpuset_mems: None,
            // devices: None,
            // device_cgroup_rules: None,
            // disk_quota: None,
            // kernel_memory: None,
            // memory_reservation: None,
            // memory_swap: None,
            // memory_swappiness: None,
            // nano_cp_us: None,
            // oom_kill_disable: None,
            // pids_limit: None,
            // ulimits: None,
            // cpu_count: None,
            // cpu_percent: None,
            // io_maximum_i_ops: None,
            // io_maximum_bandwidth: None,
            binds: None,
            // container_id_file: None,
            // log_config: None,
            // network_mode: None,
            port_bindings: None,
            // restart_policy: None,
            // auto_remove: None,
            // volume_driver: None,
            // volumes_from: None,
            mounts: None,
            // cap_add: None,
            // cap_drop: None,
            // dns: None,
            // dns_options: None,
            // dns_search: None,
            // extra_hosts: None,
            // group_add: None,
            // ipc_mode: None,
            // cgroup: None,
            // links: None,
            // oom_score_adj: None,
            // pid_mode: None,
            privileged: None,
            // publish_all_ports: None,
            // readonly_rootfs: None,
            // security_opt: None,
            // storage_opt: None,
            // tmpfs: None,
            // uts_mode: None,
            // userns_mode: None,
            // shm_size: None,
            // sysctls: None,
            // runtime: None,
            // console_size: None,
            // isolation: None,
            other_properties: Default::default(),
        }
    }

    // pub fn set_cpu_shares(&mut self, cpu_shares: i32) {
    //     self.cpu_shares = Some(cpu_shares);
    // }

    // pub fn with_cpu_shares(mut self, cpu_shares: i32) -> Self {
    //     self.cpu_shares = Some(cpu_shares);
    //     self
    // }

    // pub fn cpu_shares(&self) -> Option<i32> {
    //     self.cpu_shares
    // }

    // pub fn reset_cpu_shares(&mut self) {
    //     self.cpu_shares = None;
    // }

    pub fn set_memory(&mut self, memory: i64) {
        self.memory = Some(memory);
    }

    pub fn with_memory(mut self, memory: i64) -> Self {
        self.memory = Some(memory);
        self
    }

    pub fn memory(&self) -> Option<i64> {
        self.memory
    }

    pub fn reset_memory(&mut self) {
        self.memory = None;
    }

    // pub fn set_cgroup_parent(&mut self, cgroup_parent: String) {
    //     self.cgroup_parent = Some(cgroup_parent);
    // }

    // pub fn with_cgroup_parent(mut self, cgroup_parent: String) -> Self {
    //     self.cgroup_parent = Some(cgroup_parent);
    //     self
    // }

    // pub fn cgroup_parent(&self) -> Option<&str> {
    //     self.cgroup_parent.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_cgroup_parent(&mut self) {
    //     self.cgroup_parent = None;
    // }

    // pub fn set_blkio_weight(&mut self, blkio_weight: i32) {
    //     self.blkio_weight = Some(blkio_weight);
    // }

    // pub fn with_blkio_weight(mut self, blkio_weight: i32) -> Self {
    //     self.blkio_weight = Some(blkio_weight);
    //     self
    // }

    // pub fn blkio_weight(&self) -> Option<i32> {
    //     self.blkio_weight
    // }

    // pub fn reset_blkio_weight(&mut self) {
    //     self.blkio_weight = None;
    // }

    // pub fn set_blkio_weight_device(
    //     &mut self,
    //     blkio_weight_device: Vec<crate::models::ResourcesBlkioWeightDevice>,
    // ) {
    //     self.blkio_weight_device = Some(blkio_weight_device);
    // }

    // pub fn with_blkio_weight_device(
    //     mut self,
    //     blkio_weight_device: Vec<crate::models::ResourcesBlkioWeightDevice>,
    // ) -> Self {
    //     self.blkio_weight_device = Some(blkio_weight_device);
    //     self
    // }

    // pub fn blkio_weight_device(&self) -> Option<&[crate::models::ResourcesBlkioWeightDevice]> {
    //     self.blkio_weight_device.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_blkio_weight_device(&mut self) {
    //     self.blkio_weight_device = None;
    // }

    // pub fn set_blkio_device_read_bps(
    //     &mut self,
    //     blkio_device_read_bps: Vec<crate::models::ThrottleDevice>,
    // ) {
    //     self.blkio_device_read_bps = Some(blkio_device_read_bps);
    // }

    // pub fn with_blkio_device_read_bps(
    //     mut self,
    //     blkio_device_read_bps: Vec<crate::models::ThrottleDevice>,
    // ) -> Self {
    //     self.blkio_device_read_bps = Some(blkio_device_read_bps);
    //     self
    // }

    // pub fn blkio_device_read_bps(&self) -> Option<&[crate::models::ThrottleDevice]> {
    //     self.blkio_device_read_bps.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_blkio_device_read_bps(&mut self) {
    //     self.blkio_device_read_bps = None;
    // }

    // pub fn set_blkio_device_write_bps(
    //     &mut self,
    //     blkio_device_write_bps: Vec<crate::models::ThrottleDevice>,
    // ) {
    //     self.blkio_device_write_bps = Some(blkio_device_write_bps);
    // }

    // pub fn with_blkio_device_write_bps(
    //     mut self,
    //     blkio_device_write_bps: Vec<crate::models::ThrottleDevice>,
    // ) -> Self {
    //     self.blkio_device_write_bps = Some(blkio_device_write_bps);
    //     self
    // }

    // pub fn blkio_device_write_bps(&self) -> Option<&[crate::models::ThrottleDevice]> {
    //     self.blkio_device_write_bps.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_blkio_device_write_bps(&mut self) {
    //     self.blkio_device_write_bps = None;
    // }

    // pub fn set_blkio_device_read_i_ops(
    //     &mut self,
    //     blkio_device_read_i_ops: Vec<crate::models::ThrottleDevice>,
    // ) {
    //     self.blkio_device_read_i_ops = Some(blkio_device_read_i_ops);
    // }

    // pub fn with_blkio_device_read_i_ops(
    //     mut self,
    //     blkio_device_read_i_ops: Vec<crate::models::ThrottleDevice>,
    // ) -> Self {
    //     self.blkio_device_read_i_ops = Some(blkio_device_read_i_ops);
    //     self
    // }

    // pub fn blkio_device_read_i_ops(&self) -> Option<&[crate::models::ThrottleDevice]> {
    //     self.blkio_device_read_i_ops.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_blkio_device_read_i_ops(&mut self) {
    //     self.blkio_device_read_i_ops = None;
    // }

    // pub fn set_blkio_device_write_i_ops(
    //     &mut self,
    //     blkio_device_write_i_ops: Vec<crate::models::ThrottleDevice>,
    // ) {
    //     self.blkio_device_write_i_ops = Some(blkio_device_write_i_ops);
    // }

    // pub fn with_blkio_device_write_i_ops(
    //     mut self,
    //     blkio_device_write_i_ops: Vec<crate::models::ThrottleDevice>,
    // ) -> Self {
    //     self.blkio_device_write_i_ops = Some(blkio_device_write_i_ops);
    //     self
    // }

    // pub fn blkio_device_write_i_ops(&self) -> Option<&[crate::models::ThrottleDevice]> {
    //     self.blkio_device_write_i_ops.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_blkio_device_write_i_ops(&mut self) {
    //     self.blkio_device_write_i_ops = None;
    // }

    // pub fn set_cpu_period(&mut self, cpu_period: i64) {
    //     self.cpu_period = Some(cpu_period);
    // }

    // pub fn with_cpu_period(mut self, cpu_period: i64) -> Self {
    //     self.cpu_period = Some(cpu_period);
    //     self
    // }

    // pub fn cpu_period(&self) -> Option<i64> {
    //     self.cpu_period
    // }

    // pub fn reset_cpu_period(&mut self) {
    //     self.cpu_period = None;
    // }

    // pub fn set_cpu_quota(&mut self, cpu_quota: i64) {
    //     self.cpu_quota = Some(cpu_quota);
    // }

    // pub fn with_cpu_quota(mut self, cpu_quota: i64) -> Self {
    //     self.cpu_quota = Some(cpu_quota);
    //     self
    // }

    // pub fn cpu_quota(&self) -> Option<i64> {
    //     self.cpu_quota
    // }

    // pub fn reset_cpu_quota(&mut self) {
    //     self.cpu_quota = None;
    // }

    // pub fn set_cpu_realtime_period(&mut self, cpu_realtime_period: i64) {
    //     self.cpu_realtime_period = Some(cpu_realtime_period);
    // }

    // pub fn with_cpu_realtime_period(mut self, cpu_realtime_period: i64) -> Self {
    //     self.cpu_realtime_period = Some(cpu_realtime_period);
    //     self
    // }

    // pub fn cpu_realtime_period(&self) -> Option<i64> {
    //     self.cpu_realtime_period
    // }

    // pub fn reset_cpu_realtime_period(&mut self) {
    //     self.cpu_realtime_period = None;
    // }

    // pub fn set_cpu_realtime_runtime(&mut self, cpu_realtime_runtime: i64) {
    //     self.cpu_realtime_runtime = Some(cpu_realtime_runtime);
    // }

    // pub fn with_cpu_realtime_runtime(mut self, cpu_realtime_runtime: i64) -> Self {
    //     self.cpu_realtime_runtime = Some(cpu_realtime_runtime);
    //     self
    // }

    // pub fn cpu_realtime_runtime(&self) -> Option<i64> {
    //     self.cpu_realtime_runtime
    // }

    // pub fn reset_cpu_realtime_runtime(&mut self) {
    //     self.cpu_realtime_runtime = None;
    // }

    // pub fn set_cpuset_cpus(&mut self, cpuset_cpus: String) {
    //     self.cpuset_cpus = Some(cpuset_cpus);
    // }

    // pub fn with_cpuset_cpus(mut self, cpuset_cpus: String) -> Self {
    //     self.cpuset_cpus = Some(cpuset_cpus);
    //     self
    // }

    // pub fn cpuset_cpus(&self) -> Option<&str> {
    //     self.cpuset_cpus.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_cpuset_cpus(&mut self) {
    //     self.cpuset_cpus = None;
    // }

    // pub fn set_cpuset_mems(&mut self, cpuset_mems: String) {
    //     self.cpuset_mems = Some(cpuset_mems);
    // }

    // pub fn with_cpuset_mems(mut self, cpuset_mems: String) -> Self {
    //     self.cpuset_mems = Some(cpuset_mems);
    //     self
    // }

    // pub fn cpuset_mems(&self) -> Option<&str> {
    //     self.cpuset_mems.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_cpuset_mems(&mut self) {
    //     self.cpuset_mems = None;
    // }

    // pub fn set_devices(&mut self, devices: Vec<crate::models::DeviceMapping>) {
    //     self.devices = Some(devices);
    // }

    // pub fn with_devices(mut self, devices: Vec<crate::models::DeviceMapping>) -> Self {
    //     self.devices = Some(devices);
    //     self
    // }

    // pub fn devices(&self) -> Option<&[crate::models::DeviceMapping]> {
    //     self.devices.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_devices(&mut self) {
    //     self.devices = None;
    // }

    // pub fn set_device_cgroup_rules(&mut self, device_cgroup_rules: Vec<String>) {
    //     self.device_cgroup_rules = Some(device_cgroup_rules);
    // }

    // pub fn with_device_cgroup_rules(mut self, device_cgroup_rules: Vec<String>) -> Self {
    //     self.device_cgroup_rules = Some(device_cgroup_rules);
    //     self
    // }

    // pub fn device_cgroup_rules(&self) -> Option<&[String]> {
    //     self.device_cgroup_rules.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_device_cgroup_rules(&mut self) {
    //     self.device_cgroup_rules = None;
    // }

    // pub fn set_disk_quota(&mut self, disk_quota: i64) {
    //     self.disk_quota = Some(disk_quota);
    // }

    // pub fn with_disk_quota(mut self, disk_quota: i64) -> Self {
    //     self.disk_quota = Some(disk_quota);
    //     self
    // }

    // pub fn disk_quota(&self) -> Option<i64> {
    //     self.disk_quota
    // }

    // pub fn reset_disk_quota(&mut self) {
    //     self.disk_quota = None;
    // }

    // pub fn set_kernel_memory(&mut self, kernel_memory: i64) {
    //     self.kernel_memory = Some(kernel_memory);
    // }

    // pub fn with_kernel_memory(mut self, kernel_memory: i64) -> Self {
    //     self.kernel_memory = Some(kernel_memory);
    //     self
    // }

    // pub fn kernel_memory(&self) -> Option<i64> {
    //     self.kernel_memory
    // }

    // pub fn reset_kernel_memory(&mut self) {
    //     self.kernel_memory = None;
    // }

    // pub fn set_memory_reservation(&mut self, memory_reservation: i64) {
    //     self.memory_reservation = Some(memory_reservation);
    // }

    // pub fn with_memory_reservation(mut self, memory_reservation: i64) -> Self {
    //     self.memory_reservation = Some(memory_reservation);
    //     self
    // }

    // pub fn memory_reservation(&self) -> Option<i64> {
    //     self.memory_reservation
    // }

    // pub fn reset_memory_reservation(&mut self) {
    //     self.memory_reservation = None;
    // }

    // pub fn set_memory_swap(&mut self, memory_swap: i64) {
    //     self.memory_swap = Some(memory_swap);
    // }

    // pub fn with_memory_swap(mut self, memory_swap: i64) -> Self {
    //     self.memory_swap = Some(memory_swap);
    //     self
    // }

    // pub fn memory_swap(&self) -> Option<i64> {
    //     self.memory_swap
    // }

    // pub fn reset_memory_swap(&mut self) {
    //     self.memory_swap = None;
    // }

    // pub fn set_memory_swappiness(&mut self, memory_swappiness: i64) {
    //     self.memory_swappiness = Some(memory_swappiness);
    // }

    // pub fn with_memory_swappiness(mut self, memory_swappiness: i64) -> Self {
    //     self.memory_swappiness = Some(memory_swappiness);
    //     self
    // }

    // pub fn memory_swappiness(&self) -> Option<i64> {
    //     self.memory_swappiness
    // }

    // pub fn reset_memory_swappiness(&mut self) {
    //     self.memory_swappiness = None;
    // }

    // pub fn set_nano_cp_us(&mut self, nano_cp_us: i64) {
    //     self.nano_cp_us = Some(nano_cp_us);
    // }

    // pub fn with_nano_cp_us(mut self, nano_cp_us: i64) -> Self {
    //     self.nano_cp_us = Some(nano_cp_us);
    //     self
    // }

    // pub fn nano_cp_us(&self) -> Option<i64> {
    //     self.nano_cp_us
    // }

    // pub fn reset_nano_cp_us(&mut self) {
    //     self.nano_cp_us = None;
    // }

    // pub fn set_oom_kill_disable(&mut self, oom_kill_disable: bool) {
    //     self.oom_kill_disable = Some(oom_kill_disable);
    // }

    // pub fn with_oom_kill_disable(mut self, oom_kill_disable: bool) -> Self {
    //     self.oom_kill_disable = Some(oom_kill_disable);
    //     self
    // }

    // pub fn oom_kill_disable(&self) -> Option<&bool> {
    //     self.oom_kill_disable.as_ref()
    // }

    // pub fn reset_oom_kill_disable(&mut self) {
    //     self.oom_kill_disable = None;
    // }

    // pub fn set_pids_limit(&mut self, pids_limit: i64) {
    //     self.pids_limit = Some(pids_limit);
    // }

    // pub fn with_pids_limit(mut self, pids_limit: i64) -> Self {
    //     self.pids_limit = Some(pids_limit);
    //     self
    // }

    // pub fn pids_limit(&self) -> Option<i64> {
    //     self.pids_limit
    // }

    // pub fn reset_pids_limit(&mut self) {
    //     self.pids_limit = None;
    // }

    // pub fn set_ulimits(&mut self, ulimits: Vec<crate::models::ResourcesUlimits>) {
    //     self.ulimits = Some(ulimits);
    // }

    // pub fn with_ulimits(mut self, ulimits: Vec<crate::models::ResourcesUlimits>) -> Self {
    //     self.ulimits = Some(ulimits);
    //     self
    // }

    // pub fn ulimits(&self) -> Option<&[crate::models::ResourcesUlimits]> {
    //     self.ulimits.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_ulimits(&mut self) {
    //     self.ulimits = None;
    // }

    // pub fn set_cpu_count(&mut self, cpu_count: i64) {
    //     self.cpu_count = Some(cpu_count);
    // }

    // pub fn with_cpu_count(mut self, cpu_count: i64) -> Self {
    //     self.cpu_count = Some(cpu_count);
    //     self
    // }

    // pub fn cpu_count(&self) -> Option<i64> {
    //     self.cpu_count
    // }

    // pub fn reset_cpu_count(&mut self) {
    //     self.cpu_count = None;
    // }

    // pub fn set_cpu_percent(&mut self, cpu_percent: i64) {
    //     self.cpu_percent = Some(cpu_percent);
    // }

    // pub fn with_cpu_percent(mut self, cpu_percent: i64) -> Self {
    //     self.cpu_percent = Some(cpu_percent);
    //     self
    // }

    // pub fn cpu_percent(&self) -> Option<i64> {
    //     self.cpu_percent
    // }

    // pub fn reset_cpu_percent(&mut self) {
    //     self.cpu_percent = None;
    // }

    // pub fn set_io_maximum_i_ops(&mut self, io_maximum_i_ops: i64) {
    //     self.io_maximum_i_ops = Some(io_maximum_i_ops);
    // }

    // pub fn with_io_maximum_i_ops(mut self, io_maximum_i_ops: i64) -> Self {
    //     self.io_maximum_i_ops = Some(io_maximum_i_ops);
    //     self
    // }

    // pub fn io_maximum_i_ops(&self) -> Option<i64> {
    //     self.io_maximum_i_ops
    // }

    // pub fn reset_io_maximum_i_ops(&mut self) {
    //     self.io_maximum_i_ops = None;
    // }

    // pub fn set_io_maximum_bandwidth(&mut self, io_maximum_bandwidth: i64) {
    //     self.io_maximum_bandwidth = Some(io_maximum_bandwidth);
    // }

    // pub fn with_io_maximum_bandwidth(mut self, io_maximum_bandwidth: i64) -> Self {
    //     self.io_maximum_bandwidth = Some(io_maximum_bandwidth);
    //     self
    // }

    // pub fn io_maximum_bandwidth(&self) -> Option<i64> {
    //     self.io_maximum_bandwidth
    // }

    // pub fn reset_io_maximum_bandwidth(&mut self) {
    //     self.io_maximum_bandwidth = None;
    // }

    pub fn set_binds(&mut self, binds: Vec<String>) {
        self.binds = Some(binds);
    }

    pub fn with_binds(mut self, binds: Vec<String>) -> Self {
        self.binds = Some(binds);
        self
    }

    pub fn binds(&self) -> Option<&[String]> {
        self.binds.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_binds(&mut self) {
        self.binds = None;
    }

    // pub fn set_container_id_file(&mut self, container_id_file: String) {
    //     self.container_id_file = Some(container_id_file);
    // }

    // pub fn with_container_id_file(mut self, container_id_file: String) -> Self {
    //     self.container_id_file = Some(container_id_file);
    //     self
    // }

    // pub fn container_id_file(&self) -> Option<&str> {
    //     self.container_id_file.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_container_id_file(&mut self) {
    //     self.container_id_file = None;
    // }

    // pub fn set_log_config(&mut self, log_config: crate::models::HostConfigLogConfig) {
    //     self.log_config = Some(log_config);
    // }

    // pub fn with_log_config(mut self, log_config: crate::models::HostConfigLogConfig) -> Self {
    //     self.log_config = Some(log_config);
    //     self
    // }

    // pub fn log_config(&self) -> Option<&crate::models::HostConfigLogConfig> {
    //     self.log_config.as_ref()
    // }

    // pub fn reset_log_config(&mut self) {
    //     self.log_config = None;
    // }

    // pub fn set_network_mode(&mut self, network_mode: String) {
    //     self.network_mode = Some(network_mode);
    // }

    // pub fn with_network_mode(mut self, network_mode: String) -> Self {
    //     self.network_mode = Some(network_mode);
    //     self
    // }

    // pub fn network_mode(&self) -> Option<&str> {
    //     self.network_mode.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_network_mode(&mut self) {
    //     self.network_mode = None;
    // }

    pub fn set_port_bindings(
        &mut self,
        port_bindings: ::std::collections::HashMap<
            String,
            Vec<crate::models::HostConfigPortBindings>,
        >,
    ) {
        self.port_bindings = Some(port_bindings);
    }

    pub fn with_port_bindings(
        mut self,
        port_bindings: ::std::collections::HashMap<
            String,
            Vec<crate::models::HostConfigPortBindings>,
        >,
    ) -> Self {
        self.port_bindings = Some(port_bindings);
        self
    }

    pub fn port_bindings(
        &self,
    ) -> Option<&::std::collections::HashMap<String, Vec<crate::models::HostConfigPortBindings>>>
    {
        self.port_bindings.as_ref()
    }

    pub fn reset_port_bindings(&mut self) {
        self.port_bindings = None;
    }

    // pub fn set_restart_policy(&mut self, restart_policy: crate::models::RestartPolicy) {
    //     self.restart_policy = Some(restart_policy);
    // }

    // pub fn with_restart_policy(mut self, restart_policy: crate::models::RestartPolicy) -> Self {
    //     self.restart_policy = Some(restart_policy);
    //     self
    // }

    // pub fn restart_policy(&self) -> Option<&crate::models::RestartPolicy> {
    //     self.restart_policy.as_ref()
    // }

    // pub fn reset_restart_policy(&mut self) {
    //     self.restart_policy = None;
    // }

    // pub fn set_auto_remove(&mut self, auto_remove: bool) {
    //     self.auto_remove = Some(auto_remove);
    // }

    // pub fn with_auto_remove(mut self, auto_remove: bool) -> Self {
    //     self.auto_remove = Some(auto_remove);
    //     self
    // }

    // pub fn auto_remove(&self) -> Option<&bool> {
    //     self.auto_remove.as_ref()
    // }

    // pub fn reset_auto_remove(&mut self) {
    //     self.auto_remove = None;
    // }

    // pub fn set_volume_driver(&mut self, volume_driver: String) {
    //     self.volume_driver = Some(volume_driver);
    // }

    // pub fn with_volume_driver(mut self, volume_driver: String) -> Self {
    //     self.volume_driver = Some(volume_driver);
    //     self
    // }

    // pub fn volume_driver(&self) -> Option<&str> {
    //     self.volume_driver.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_volume_driver(&mut self) {
    //     self.volume_driver = None;
    // }

    // pub fn set_volumes_from(&mut self, volumes_from: Vec<String>) {
    //     self.volumes_from = Some(volumes_from);
    // }

    // pub fn with_volumes_from(mut self, volumes_from: Vec<String>) -> Self {
    //     self.volumes_from = Some(volumes_from);
    //     self
    // }

    // pub fn volumes_from(&self) -> Option<&[String]> {
    //     self.volumes_from.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_volumes_from(&mut self) {
    //     self.volumes_from = None;
    // }

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

    // pub fn set_cap_add(&mut self, cap_add: Vec<String>) {
    //     self.cap_add = Some(cap_add);
    // }

    // pub fn with_cap_add(mut self, cap_add: Vec<String>) -> Self {
    //     self.cap_add = Some(cap_add);
    //     self
    // }

    // pub fn cap_add(&self) -> Option<&[String]> {
    //     self.cap_add.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_cap_add(&mut self) {
    //     self.cap_add = None;
    // }

    // pub fn set_cap_drop(&mut self, cap_drop: Vec<String>) {
    //     self.cap_drop = Some(cap_drop);
    // }

    // pub fn with_cap_drop(mut self, cap_drop: Vec<String>) -> Self {
    //     self.cap_drop = Some(cap_drop);
    //     self
    // }

    // pub fn cap_drop(&self) -> Option<&[String]> {
    //     self.cap_drop.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_cap_drop(&mut self) {
    //     self.cap_drop = None;
    // }

    // pub fn set_dns(&mut self, dns: Vec<String>) {
    //     self.dns = Some(dns);
    // }

    // pub fn with_dns(mut self, dns: Vec<String>) -> Self {
    //     self.dns = Some(dns);
    //     self
    // }

    // pub fn dns(&self) -> Option<&[String]> {
    //     self.dns.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_dns(&mut self) {
    //     self.dns = None;
    // }

    // pub fn set_dns_options(&mut self, dns_options: Vec<String>) {
    //     self.dns_options = Some(dns_options);
    // }

    // pub fn with_dns_options(mut self, dns_options: Vec<String>) -> Self {
    //     self.dns_options = Some(dns_options);
    //     self
    // }

    // pub fn dns_options(&self) -> Option<&[String]> {
    //     self.dns_options.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_dns_options(&mut self) {
    //     self.dns_options = None;
    // }

    // pub fn set_dns_search(&mut self, dns_search: Vec<String>) {
    //     self.dns_search = Some(dns_search);
    // }

    // pub fn with_dns_search(mut self, dns_search: Vec<String>) -> Self {
    //     self.dns_search = Some(dns_search);
    //     self
    // }

    // pub fn dns_search(&self) -> Option<&[String]> {
    //     self.dns_search.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_dns_search(&mut self) {
    //     self.dns_search = None;
    // }

    // pub fn set_extra_hosts(&mut self, extra_hosts: Vec<String>) {
    //     self.extra_hosts = Some(extra_hosts);
    // }

    // pub fn with_extra_hosts(mut self, extra_hosts: Vec<String>) -> Self {
    //     self.extra_hosts = Some(extra_hosts);
    //     self
    // }

    // pub fn extra_hosts(&self) -> Option<&[String]> {
    //     self.extra_hosts.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_extra_hosts(&mut self) {
    //     self.extra_hosts = None;
    // }

    // pub fn set_group_add(&mut self, group_add: Vec<String>) {
    //     self.group_add = Some(group_add);
    // }

    // pub fn with_group_add(mut self, group_add: Vec<String>) -> Self {
    //     self.group_add = Some(group_add);
    //     self
    // }

    // pub fn group_add(&self) -> Option<&[String]> {
    //     self.group_add.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_group_add(&mut self) {
    //     self.group_add = None;
    // }

    // pub fn set_ipc_mode(&mut self, ipc_mode: String) {
    //     self.ipc_mode = Some(ipc_mode);
    // }

    // pub fn with_ipc_mode(mut self, ipc_mode: String) -> Self {
    //     self.ipc_mode = Some(ipc_mode);
    //     self
    // }

    // pub fn ipc_mode(&self) -> Option<&str> {
    //     self.ipc_mode.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_ipc_mode(&mut self) {
    //     self.ipc_mode = None;
    // }

    // pub fn set_cgroup(&mut self, cgroup: String) {
    //     self.cgroup = Some(cgroup);
    // }

    // pub fn with_cgroup(mut self, cgroup: String) -> Self {
    //     self.cgroup = Some(cgroup);
    //     self
    // }

    // pub fn cgroup(&self) -> Option<&str> {
    //     self.cgroup.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_cgroup(&mut self) {
    //     self.cgroup = None;
    // }

    // pub fn set_links(&mut self, links: Vec<String>) {
    //     self.links = Some(links);
    // }

    // pub fn with_links(mut self, links: Vec<String>) -> Self {
    //     self.links = Some(links);
    //     self
    // }

    // pub fn links(&self) -> Option<&[String]> {
    //     self.links.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_links(&mut self) {
    //     self.links = None;
    // }

    // pub fn set_oom_score_adj(&mut self, oom_score_adj: i32) {
    //     self.oom_score_adj = Some(oom_score_adj);
    // }

    // pub fn with_oom_score_adj(mut self, oom_score_adj: i32) -> Self {
    //     self.oom_score_adj = Some(oom_score_adj);
    //     self
    // }

    // pub fn oom_score_adj(&self) -> Option<i32> {
    //     self.oom_score_adj
    // }

    // pub fn reset_oom_score_adj(&mut self) {
    //     self.oom_score_adj = None;
    // }

    // pub fn set_pid_mode(&mut self, pid_mode: String) {
    //     self.pid_mode = Some(pid_mode);
    // }

    // pub fn with_pid_mode(mut self, pid_mode: String) -> Self {
    //     self.pid_mode = Some(pid_mode);
    //     self
    // }

    // pub fn pid_mode(&self) -> Option<&str> {
    //     self.pid_mode.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_pid_mode(&mut self) {
    //     self.pid_mode = None;
    // }

    pub fn set_privileged(&mut self, privileged: bool) {
        self.privileged = Some(privileged);
    }

    pub fn with_privileged(mut self, privileged: bool) -> Self {
        self.privileged = Some(privileged);
        self
    }

    pub fn privileged(&self) -> Option<&bool> {
        self.privileged.as_ref()
    }

    pub fn reset_privileged(&mut self) {
        self.privileged = None;
    }

    // pub fn set_publish_all_ports(&mut self, publish_all_ports: bool) {
    //     self.publish_all_ports = Some(publish_all_ports);
    // }

    // pub fn with_publish_all_ports(mut self, publish_all_ports: bool) -> Self {
    //     self.publish_all_ports = Some(publish_all_ports);
    //     self
    // }

    // pub fn publish_all_ports(&self) -> Option<&bool> {
    //     self.publish_all_ports.as_ref()
    // }

    // pub fn reset_publish_all_ports(&mut self) {
    //     self.publish_all_ports = None;
    // }

    // pub fn set_readonly_rootfs(&mut self, readonly_rootfs: bool) {
    //     self.readonly_rootfs = Some(readonly_rootfs);
    // }

    // pub fn with_readonly_rootfs(mut self, readonly_rootfs: bool) -> Self {
    //     self.readonly_rootfs = Some(readonly_rootfs);
    //     self
    // }

    // pub fn readonly_rootfs(&self) -> Option<&bool> {
    //     self.readonly_rootfs.as_ref()
    // }

    // pub fn reset_readonly_rootfs(&mut self) {
    //     self.readonly_rootfs = None;
    // }

    // pub fn set_security_opt(&mut self, security_opt: Vec<String>) {
    //     self.security_opt = Some(security_opt);
    // }

    // pub fn with_security_opt(mut self, security_opt: Vec<String>) -> Self {
    //     self.security_opt = Some(security_opt);
    //     self
    // }

    // pub fn security_opt(&self) -> Option<&[String]> {
    //     self.security_opt.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_security_opt(&mut self) {
    //     self.security_opt = None;
    // }

    // pub fn set_storage_opt(&mut self, storage_opt: ::std::collections::HashMap<String, String>) {
    //     self.storage_opt = Some(storage_opt);
    // }

    // pub fn with_storage_opt(
    //     mut self,
    //     storage_opt: ::std::collections::HashMap<String, String>,
    // ) -> Self {
    //     self.storage_opt = Some(storage_opt);
    //     self
    // }

    // pub fn storage_opt(&self) -> Option<&::std::collections::HashMap<String, String>> {
    //     self.storage_opt.as_ref()
    // }

    // pub fn reset_storage_opt(&mut self) {
    //     self.storage_opt = None;
    // }

    // pub fn set_tmpfs(&mut self, tmpfs: ::std::collections::HashMap<String, String>) {
    //     self.tmpfs = Some(tmpfs);
    // }

    // pub fn with_tmpfs(mut self, tmpfs: ::std::collections::HashMap<String, String>) -> Self {
    //     self.tmpfs = Some(tmpfs);
    //     self
    // }

    // pub fn tmpfs(&self) -> Option<&::std::collections::HashMap<String, String>> {
    //     self.tmpfs.as_ref()
    // }

    // pub fn reset_tmpfs(&mut self) {
    //     self.tmpfs = None;
    // }

    // pub fn set_uts_mode(&mut self, uts_mode: String) {
    //     self.uts_mode = Some(uts_mode);
    // }

    // pub fn with_uts_mode(mut self, uts_mode: String) -> Self {
    //     self.uts_mode = Some(uts_mode);
    //     self
    // }

    // pub fn uts_mode(&self) -> Option<&str> {
    //     self.uts_mode.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_uts_mode(&mut self) {
    //     self.uts_mode = None;
    // }

    // pub fn set_userns_mode(&mut self, userns_mode: String) {
    //     self.userns_mode = Some(userns_mode);
    // }

    // pub fn with_userns_mode(mut self, userns_mode: String) -> Self {
    //     self.userns_mode = Some(userns_mode);
    //     self
    // }

    // pub fn userns_mode(&self) -> Option<&str> {
    //     self.userns_mode.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_userns_mode(&mut self) {
    //     self.userns_mode = None;
    // }

    // pub fn set_shm_size(&mut self, shm_size: i64) {
    //     self.shm_size = Some(shm_size);
    // }

    // pub fn with_shm_size(mut self, shm_size: i64) -> Self {
    //     self.shm_size = Some(shm_size);
    //     self
    // }

    // pub fn shm_size(&self) -> Option<i64> {
    //     self.shm_size
    // }

    // pub fn reset_shm_size(&mut self) {
    //     self.shm_size = None;
    // }

    // pub fn set_sysctls(&mut self, sysctls: ::std::collections::HashMap<String, String>) {
    //     self.sysctls = Some(sysctls);
    // }

    // pub fn with_sysctls(mut self, sysctls: ::std::collections::HashMap<String, String>) -> Self {
    //     self.sysctls = Some(sysctls);
    //     self
    // }

    // pub fn sysctls(&self) -> Option<&::std::collections::HashMap<String, String>> {
    //     self.sysctls.as_ref()
    // }

    // pub fn reset_sysctls(&mut self) {
    //     self.sysctls = None;
    // }

    // pub fn set_runtime(&mut self, runtime: String) {
    //     self.runtime = Some(runtime);
    // }

    // pub fn with_runtime(mut self, runtime: String) -> Self {
    //     self.runtime = Some(runtime);
    //     self
    // }

    // pub fn runtime(&self) -> Option<&str> {
    //     self.runtime.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_runtime(&mut self) {
    //     self.runtime = None;
    // }

    // pub fn set_console_size(&mut self, console_size: Vec<i32>) {
    //     self.console_size = Some(console_size);
    // }

    // pub fn with_console_size(mut self, console_size: Vec<i32>) -> Self {
    //     self.console_size = Some(console_size);
    //     self
    // }

    // pub fn console_size(&self) -> Option<&[i32]> {
    //     self.console_size.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_console_size(&mut self) {
    //     self.console_size = None;
    // }

    // pub fn set_isolation(&mut self, isolation: String) {
    //     self.isolation = Some(isolation);
    // }

    // pub fn with_isolation(mut self, isolation: String) -> Self {
    //     self.isolation = Some(isolation);
    //     self
    // }

    // pub fn isolation(&self) -> Option<&str> {
    //     self.isolation.as_ref().map(AsRef::as_ref)
    // }

    // pub fn reset_isolation(&mut self) {
    //     self.isolation = None;
    // }
}
