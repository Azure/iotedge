/*
 * Docker Engine API
 *
 * The Engine API is an HTTP API served by Docker Engine. It is the API the Docker client uses to communicate with the Engine, so everything the Docker client can do can be done with the API.  Most of the client's commands map directly to API endpoints (e.g. `docker ps` is `GET /containers/json`). The notable exception is running containers, which consists of several API calls.  # Errors  The API uses standard HTTP status codes to indicate the success or failure of the API call. The body of the response will be JSON in the following format:  ``` {   \"message\": \"page not found\" } ```  # Versioning  The API is usually changed in each release of Docker, so API calls are versioned to ensure that clients don't break.  For Docker Engine 17.10, the API version is 1.33. To lock to this version, you prefix the URL with `/v1.33`. For example, calling `/info` is the same as calling `/v1.33/info`.  Engine releases in the near future should support this version of the API, so your client will continue to work even if it is talking to a newer Engine.  In previous versions of Docker, it was possible to access the API without providing a version. This behaviour is now deprecated will be removed in a future version of Docker.  If the API version specified in the URL is not supported by the daemon, a HTTP `400 Bad Request` error message is returned.  The API uses an open schema model, which means server may add extra properties to responses. Likewise, the server will ignore any extra query parameters and request body properties. When you write clients, you need to ignore additional properties in responses to ensure they do not break when talking to newer Docker daemons.  This documentation is for version 1.34 of the API. Use this table to find documentation for previous versions of the API:  Docker version  | API version | Changes ----------------|-------------|--------- 17.10.x | [1.33](https://docs.docker.com/engine/api/v1.33/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-33-api-changes) 17.09.x | [1.32](https://docs.docker.com/engine/api/v1.32/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-32-api-changes) 17.07.x | [1.31](https://docs.docker.com/engine/api/v1.31/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-31-api-changes) 17.06.x | [1.30](https://docs.docker.com/engine/api/v1.30/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-30-api-changes) 17.05.x | [1.29](https://docs.docker.com/engine/api/v1.29/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-29-api-changes) 17.04.x | [1.28](https://docs.docker.com/engine/api/v1.28/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-28-api-changes) 17.03.1 | [1.27](https://docs.docker.com/engine/api/v1.27/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-27-api-changes) 1.13.1 & 17.03.0 | [1.26](https://docs.docker.com/engine/api/v1.26/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-26-api-changes) 1.13.0 | [1.25](https://docs.docker.com/engine/api/v1.25/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-25-api-changes) 1.12.x | [1.24](https://docs.docker.com/engine/api/v1.24/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-24-api-changes) 1.11.x | [1.23](https://docs.docker.com/engine/api/v1.23/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-23-api-changes) 1.10.x | [1.22](https://docs.docker.com/engine/api/v1.22/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-22-api-changes) 1.9.x | [1.21](https://docs.docker.com/engine/api/v1.21/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-21-api-changes) 1.8.x | [1.20](https://docs.docker.com/engine/api/v1.20/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-20-api-changes) 1.7.x | [1.19](https://docs.docker.com/engine/api/v1.19/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-19-api-changes) 1.6.x | [1.18](https://docs.docker.com/engine/api/v1.18/) | [API changes](https://docs.docker.com/engine/api/version-history/#v1-18-api-changes)  # Authentication  Authentication for registries is handled client side. The client has to send authentication details to various endpoints that need to communicate with registries, such as `POST /images/(name)/push`. These are sent as `X-Registry-Auth` header as a Base64 encoded (JSON) string with the following structure:  ``` {   \"username\": \"string\",   \"password\": \"string\",   \"email\": \"string\",   \"serveraddress\": \"string\" } ```  The `serveraddress` is a domain/IP without a protocol. Throughout this structure, double quotes are required.  If you have already got an identity token from the [`/auth` endpoint](#operation/SystemAuth), you can just pass this instead of credentials:  ``` {   \"identitytoken\": \"9cbaf023786cd7...\" } ```
 *
 * OpenAPI spec version: 1.34
 *
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

/// Resources : A container's resources (cgroups config, ulimits, etc)
use serde_derive::{Deserialize, Serialize};
#[allow(unused_imports)]
use serde_json::Value;

#[derive(Debug, Serialize, Deserialize)]
pub struct Resources {
    /// An integer value representing this container's relative CPU weight versus other containers.
    #[serde(rename = "CpuShares", skip_serializing_if = "Option::is_none")]
    cpu_shares: Option<i32>,
    /// Memory limit in bytes.
    #[serde(rename = "Memory", skip_serializing_if = "Option::is_none")]
    memory: Option<i32>,
    /// Path to `cgroups` under which the container's `cgroup` is created. If the path is not absolute, the path is considered to be relative to the `cgroups` path of the init process. Cgroups are created if they do not already exist.
    #[serde(rename = "CgroupParent", skip_serializing_if = "Option::is_none")]
    cgroup_parent: Option<String>,
    /// Block IO weight (relative weight).
    #[serde(rename = "BlkioWeight", skip_serializing_if = "Option::is_none")]
    blkio_weight: Option<i32>,
    /// Block IO weight (relative device weight) in the form `[{\"Path\": \"device_path\", \"Weight\": weight}]`.
    #[serde(rename = "BlkioWeightDevice", skip_serializing_if = "Option::is_none")]
    blkio_weight_device: Option<Vec<crate::models::ResourcesBlkioWeightDevice>>,
    /// Limit read rate (bytes per second) from a device, in the form `[{\"Path\": \"device_path\", \"Rate\": rate}]`.
    #[serde(rename = "BlkioDeviceReadBps", skip_serializing_if = "Option::is_none")]
    blkio_device_read_bps: Option<Vec<crate::models::ThrottleDevice>>,
    /// Limit write rate (bytes per second) to a device, in the form `[{\"Path\": \"device_path\", \"Rate\": rate}]`.
    #[serde(
        rename = "BlkioDeviceWriteBps",
        skip_serializing_if = "Option::is_none"
    )]
    blkio_device_write_bps: Option<Vec<crate::models::ThrottleDevice>>,
    /// Limit read rate (IO per second) from a device, in the form `[{\"Path\": \"device_path\", \"Rate\": rate}]`.
    #[serde(
        rename = "BlkioDeviceReadIOps",
        skip_serializing_if = "Option::is_none"
    )]
    blkio_device_read_i_ops: Option<Vec<crate::models::ThrottleDevice>>,
    /// Limit write rate (IO per second) to a device, in the form `[{\"Path\": \"device_path\", \"Rate\": rate}]`.
    #[serde(
        rename = "BlkioDeviceWriteIOps",
        skip_serializing_if = "Option::is_none"
    )]
    blkio_device_write_i_ops: Option<Vec<crate::models::ThrottleDevice>>,
    /// The length of a CPU period in microseconds.
    #[serde(rename = "CpuPeriod", skip_serializing_if = "Option::is_none")]
    cpu_period: Option<i64>,
    /// Microseconds of CPU time that the container can get in a CPU period.
    #[serde(rename = "CpuQuota", skip_serializing_if = "Option::is_none")]
    cpu_quota: Option<i64>,
    /// The length of a CPU real-time period in microseconds. Set to 0 to allocate no time allocated to real-time tasks.
    #[serde(rename = "CpuRealtimePeriod", skip_serializing_if = "Option::is_none")]
    cpu_realtime_period: Option<i64>,
    /// The length of a CPU real-time runtime in microseconds. Set to 0 to allocate no time allocated to real-time tasks.
    #[serde(rename = "CpuRealtimeRuntime", skip_serializing_if = "Option::is_none")]
    cpu_realtime_runtime: Option<i64>,
    /// CPUs in which to allow execution (e.g., `0-3`, `0,1`)
    #[serde(rename = "CpusetCpus", skip_serializing_if = "Option::is_none")]
    cpuset_cpus: Option<String>,
    /// Memory nodes (MEMs) in which to allow execution (0-3, 0,1). Only effective on NUMA systems.
    #[serde(rename = "CpusetMems", skip_serializing_if = "Option::is_none")]
    cpuset_mems: Option<String>,
    /// A list of devices to add to the container.
    #[serde(rename = "Devices", skip_serializing_if = "Option::is_none")]
    devices: Option<Vec<crate::models::DeviceMapping>>,
    /// a list of cgroup rules to apply to the container
    #[serde(rename = "DeviceCgroupRules", skip_serializing_if = "Option::is_none")]
    device_cgroup_rules: Option<Vec<String>>,
    /// Disk limit (in bytes).
    #[serde(rename = "DiskQuota", skip_serializing_if = "Option::is_none")]
    disk_quota: Option<i64>,
    /// Kernel memory limit in bytes.
    #[serde(rename = "KernelMemory", skip_serializing_if = "Option::is_none")]
    kernel_memory: Option<i64>,
    /// Memory soft limit in bytes.
    #[serde(rename = "MemoryReservation", skip_serializing_if = "Option::is_none")]
    memory_reservation: Option<i64>,
    /// Total memory limit (memory + swap). Set as `-1` to enable unlimited swap.
    #[serde(rename = "MemorySwap", skip_serializing_if = "Option::is_none")]
    memory_swap: Option<i64>,
    /// Tune a container's memory swappiness behavior. Accepts an integer between 0 and 100.
    #[serde(rename = "MemorySwappiness", skip_serializing_if = "Option::is_none")]
    memory_swappiness: Option<i64>,
    /// CPU quota in units of 10<sup>-9</sup> CPUs.
    #[serde(rename = "NanoCPUs", skip_serializing_if = "Option::is_none")]
    nano_cp_us: Option<i64>,
    /// Disable OOM Killer for the container.
    #[serde(rename = "OomKillDisable", skip_serializing_if = "Option::is_none")]
    oom_kill_disable: Option<bool>,
    /// Tune a container's pids limit. Set -1 for unlimited.
    #[serde(rename = "PidsLimit", skip_serializing_if = "Option::is_none")]
    pids_limit: Option<i64>,
    /// A list of resource limits to set in the container. For example: `{\"Name\": \"nofile\", \"Soft\": 1024, \"Hard\": 2048}`\"
    #[serde(rename = "Ulimits", skip_serializing_if = "Option::is_none")]
    ulimits: Option<Vec<crate::models::ResourcesUlimits>>,
    /// The number of usable CPUs (Windows only).  On Windows Server containers, the processor resource controls are mutually exclusive. The order of precedence is `CPUCount` first, then `CPUShares`, and `CPUPercent` last.
    #[serde(rename = "CpuCount", skip_serializing_if = "Option::is_none")]
    cpu_count: Option<i64>,
    /// The usable percentage of the available CPUs (Windows only).  On Windows Server containers, the processor resource controls are mutually exclusive. The order of precedence is `CPUCount` first, then `CPUShares`, and `CPUPercent` last.
    #[serde(rename = "CpuPercent", skip_serializing_if = "Option::is_none")]
    cpu_percent: Option<i64>,
    /// Maximum IOps for the container system drive (Windows only)
    #[serde(rename = "IOMaximumIOps", skip_serializing_if = "Option::is_none")]
    io_maximum_i_ops: Option<i64>,
    /// Maximum IO in bytes per second for the container system drive (Windows only)
    #[serde(rename = "IOMaximumBandwidth", skip_serializing_if = "Option::is_none")]
    io_maximum_bandwidth: Option<i64>,
}

impl Resources {
    /// A container's resources (cgroups config, ulimits, etc)
    pub fn new() -> Self {
        Resources {
            cpu_shares: None,
            memory: None,
            cgroup_parent: None,
            blkio_weight: None,
            blkio_weight_device: None,
            blkio_device_read_bps: None,
            blkio_device_write_bps: None,
            blkio_device_read_i_ops: None,
            blkio_device_write_i_ops: None,
            cpu_period: None,
            cpu_quota: None,
            cpu_realtime_period: None,
            cpu_realtime_runtime: None,
            cpuset_cpus: None,
            cpuset_mems: None,
            devices: None,
            device_cgroup_rules: None,
            disk_quota: None,
            kernel_memory: None,
            memory_reservation: None,
            memory_swap: None,
            memory_swappiness: None,
            nano_cp_us: None,
            oom_kill_disable: None,
            pids_limit: None,
            ulimits: None,
            cpu_count: None,
            cpu_percent: None,
            io_maximum_i_ops: None,
            io_maximum_bandwidth: None,
        }
    }

    pub fn set_cpu_shares(&mut self, cpu_shares: i32) {
        self.cpu_shares = Some(cpu_shares);
    }

    pub fn with_cpu_shares(mut self, cpu_shares: i32) -> Self {
        self.cpu_shares = Some(cpu_shares);
        self
    }

    pub fn cpu_shares(&self) -> Option<i32> {
        self.cpu_shares
    }

    pub fn reset_cpu_shares(&mut self) {
        self.cpu_shares = None;
    }

    pub fn set_memory(&mut self, memory: i32) {
        self.memory = Some(memory);
    }

    pub fn with_memory(mut self, memory: i32) -> Self {
        self.memory = Some(memory);
        self
    }

    pub fn memory(&self) -> Option<i32> {
        self.memory
    }

    pub fn reset_memory(&mut self) {
        self.memory = None;
    }

    pub fn set_cgroup_parent(&mut self, cgroup_parent: String) {
        self.cgroup_parent = Some(cgroup_parent);
    }

    pub fn with_cgroup_parent(mut self, cgroup_parent: String) -> Self {
        self.cgroup_parent = Some(cgroup_parent);
        self
    }

    pub fn cgroup_parent(&self) -> Option<&str> {
        self.cgroup_parent.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_cgroup_parent(&mut self) {
        self.cgroup_parent = None;
    }

    pub fn set_blkio_weight(&mut self, blkio_weight: i32) {
        self.blkio_weight = Some(blkio_weight);
    }

    pub fn with_blkio_weight(mut self, blkio_weight: i32) -> Self {
        self.blkio_weight = Some(blkio_weight);
        self
    }

    pub fn blkio_weight(&self) -> Option<i32> {
        self.blkio_weight
    }

    pub fn reset_blkio_weight(&mut self) {
        self.blkio_weight = None;
    }

    pub fn set_blkio_weight_device(
        &mut self,
        blkio_weight_device: Vec<crate::models::ResourcesBlkioWeightDevice>,
    ) {
        self.blkio_weight_device = Some(blkio_weight_device);
    }

    pub fn with_blkio_weight_device(
        mut self,
        blkio_weight_device: Vec<crate::models::ResourcesBlkioWeightDevice>,
    ) -> Self {
        self.blkio_weight_device = Some(blkio_weight_device);
        self
    }

    pub fn blkio_weight_device(&self) -> Option<&[crate::models::ResourcesBlkioWeightDevice]> {
        self.blkio_weight_device.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_blkio_weight_device(&mut self) {
        self.blkio_weight_device = None;
    }

    pub fn set_blkio_device_read_bps(
        &mut self,
        blkio_device_read_bps: Vec<crate::models::ThrottleDevice>,
    ) {
        self.blkio_device_read_bps = Some(blkio_device_read_bps);
    }

    pub fn with_blkio_device_read_bps(
        mut self,
        blkio_device_read_bps: Vec<crate::models::ThrottleDevice>,
    ) -> Self {
        self.blkio_device_read_bps = Some(blkio_device_read_bps);
        self
    }

    pub fn blkio_device_read_bps(&self) -> Option<&[crate::models::ThrottleDevice]> {
        self.blkio_device_read_bps.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_blkio_device_read_bps(&mut self) {
        self.blkio_device_read_bps = None;
    }

    pub fn set_blkio_device_write_bps(
        &mut self,
        blkio_device_write_bps: Vec<crate::models::ThrottleDevice>,
    ) {
        self.blkio_device_write_bps = Some(blkio_device_write_bps);
    }

    pub fn with_blkio_device_write_bps(
        mut self,
        blkio_device_write_bps: Vec<crate::models::ThrottleDevice>,
    ) -> Self {
        self.blkio_device_write_bps = Some(blkio_device_write_bps);
        self
    }

    pub fn blkio_device_write_bps(&self) -> Option<&[crate::models::ThrottleDevice]> {
        self.blkio_device_write_bps.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_blkio_device_write_bps(&mut self) {
        self.blkio_device_write_bps = None;
    }

    pub fn set_blkio_device_read_i_ops(
        &mut self,
        blkio_device_read_i_ops: Vec<crate::models::ThrottleDevice>,
    ) {
        self.blkio_device_read_i_ops = Some(blkio_device_read_i_ops);
    }

    pub fn with_blkio_device_read_i_ops(
        mut self,
        blkio_device_read_i_ops: Vec<crate::models::ThrottleDevice>,
    ) -> Self {
        self.blkio_device_read_i_ops = Some(blkio_device_read_i_ops);
        self
    }

    pub fn blkio_device_read_i_ops(&self) -> Option<&[crate::models::ThrottleDevice]> {
        self.blkio_device_read_i_ops.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_blkio_device_read_i_ops(&mut self) {
        self.blkio_device_read_i_ops = None;
    }

    pub fn set_blkio_device_write_i_ops(
        &mut self,
        blkio_device_write_i_ops: Vec<crate::models::ThrottleDevice>,
    ) {
        self.blkio_device_write_i_ops = Some(blkio_device_write_i_ops);
    }

    pub fn with_blkio_device_write_i_ops(
        mut self,
        blkio_device_write_i_ops: Vec<crate::models::ThrottleDevice>,
    ) -> Self {
        self.blkio_device_write_i_ops = Some(blkio_device_write_i_ops);
        self
    }

    pub fn blkio_device_write_i_ops(&self) -> Option<&[crate::models::ThrottleDevice]> {
        self.blkio_device_write_i_ops.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_blkio_device_write_i_ops(&mut self) {
        self.blkio_device_write_i_ops = None;
    }

    pub fn set_cpu_period(&mut self, cpu_period: i64) {
        self.cpu_period = Some(cpu_period);
    }

    pub fn with_cpu_period(mut self, cpu_period: i64) -> Self {
        self.cpu_period = Some(cpu_period);
        self
    }

    pub fn cpu_period(&self) -> Option<i64> {
        self.cpu_period
    }

    pub fn reset_cpu_period(&mut self) {
        self.cpu_period = None;
    }

    pub fn set_cpu_quota(&mut self, cpu_quota: i64) {
        self.cpu_quota = Some(cpu_quota);
    }

    pub fn with_cpu_quota(mut self, cpu_quota: i64) -> Self {
        self.cpu_quota = Some(cpu_quota);
        self
    }

    pub fn cpu_quota(&self) -> Option<i64> {
        self.cpu_quota
    }

    pub fn reset_cpu_quota(&mut self) {
        self.cpu_quota = None;
    }

    pub fn set_cpu_realtime_period(&mut self, cpu_realtime_period: i64) {
        self.cpu_realtime_period = Some(cpu_realtime_period);
    }

    pub fn with_cpu_realtime_period(mut self, cpu_realtime_period: i64) -> Self {
        self.cpu_realtime_period = Some(cpu_realtime_period);
        self
    }

    pub fn cpu_realtime_period(&self) -> Option<i64> {
        self.cpu_realtime_period
    }

    pub fn reset_cpu_realtime_period(&mut self) {
        self.cpu_realtime_period = None;
    }

    pub fn set_cpu_realtime_runtime(&mut self, cpu_realtime_runtime: i64) {
        self.cpu_realtime_runtime = Some(cpu_realtime_runtime);
    }

    pub fn with_cpu_realtime_runtime(mut self, cpu_realtime_runtime: i64) -> Self {
        self.cpu_realtime_runtime = Some(cpu_realtime_runtime);
        self
    }

    pub fn cpu_realtime_runtime(&self) -> Option<i64> {
        self.cpu_realtime_runtime
    }

    pub fn reset_cpu_realtime_runtime(&mut self) {
        self.cpu_realtime_runtime = None;
    }

    pub fn set_cpuset_cpus(&mut self, cpuset_cpus: String) {
        self.cpuset_cpus = Some(cpuset_cpus);
    }

    pub fn with_cpuset_cpus(mut self, cpuset_cpus: String) -> Self {
        self.cpuset_cpus = Some(cpuset_cpus);
        self
    }

    pub fn cpuset_cpus(&self) -> Option<&str> {
        self.cpuset_cpus.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_cpuset_cpus(&mut self) {
        self.cpuset_cpus = None;
    }

    pub fn set_cpuset_mems(&mut self, cpuset_mems: String) {
        self.cpuset_mems = Some(cpuset_mems);
    }

    pub fn with_cpuset_mems(mut self, cpuset_mems: String) -> Self {
        self.cpuset_mems = Some(cpuset_mems);
        self
    }

    pub fn cpuset_mems(&self) -> Option<&str> {
        self.cpuset_mems.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_cpuset_mems(&mut self) {
        self.cpuset_mems = None;
    }

    pub fn set_devices(&mut self, devices: Vec<crate::models::DeviceMapping>) {
        self.devices = Some(devices);
    }

    pub fn with_devices(mut self, devices: Vec<crate::models::DeviceMapping>) -> Self {
        self.devices = Some(devices);
        self
    }

    pub fn devices(&self) -> Option<&[crate::models::DeviceMapping]> {
        self.devices.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_devices(&mut self) {
        self.devices = None;
    }

    pub fn set_device_cgroup_rules(&mut self, device_cgroup_rules: Vec<String>) {
        self.device_cgroup_rules = Some(device_cgroup_rules);
    }

    pub fn with_device_cgroup_rules(mut self, device_cgroup_rules: Vec<String>) -> Self {
        self.device_cgroup_rules = Some(device_cgroup_rules);
        self
    }

    pub fn device_cgroup_rules(&self) -> Option<&[String]> {
        self.device_cgroup_rules.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_device_cgroup_rules(&mut self) {
        self.device_cgroup_rules = None;
    }

    pub fn set_disk_quota(&mut self, disk_quota: i64) {
        self.disk_quota = Some(disk_quota);
    }

    pub fn with_disk_quota(mut self, disk_quota: i64) -> Self {
        self.disk_quota = Some(disk_quota);
        self
    }

    pub fn disk_quota(&self) -> Option<i64> {
        self.disk_quota
    }

    pub fn reset_disk_quota(&mut self) {
        self.disk_quota = None;
    }

    pub fn set_kernel_memory(&mut self, kernel_memory: i64) {
        self.kernel_memory = Some(kernel_memory);
    }

    pub fn with_kernel_memory(mut self, kernel_memory: i64) -> Self {
        self.kernel_memory = Some(kernel_memory);
        self
    }

    pub fn kernel_memory(&self) -> Option<i64> {
        self.kernel_memory
    }

    pub fn reset_kernel_memory(&mut self) {
        self.kernel_memory = None;
    }

    pub fn set_memory_reservation(&mut self, memory_reservation: i64) {
        self.memory_reservation = Some(memory_reservation);
    }

    pub fn with_memory_reservation(mut self, memory_reservation: i64) -> Self {
        self.memory_reservation = Some(memory_reservation);
        self
    }

    pub fn memory_reservation(&self) -> Option<i64> {
        self.memory_reservation
    }

    pub fn reset_memory_reservation(&mut self) {
        self.memory_reservation = None;
    }

    pub fn set_memory_swap(&mut self, memory_swap: i64) {
        self.memory_swap = Some(memory_swap);
    }

    pub fn with_memory_swap(mut self, memory_swap: i64) -> Self {
        self.memory_swap = Some(memory_swap);
        self
    }

    pub fn memory_swap(&self) -> Option<i64> {
        self.memory_swap
    }

    pub fn reset_memory_swap(&mut self) {
        self.memory_swap = None;
    }

    pub fn set_memory_swappiness(&mut self, memory_swappiness: i64) {
        self.memory_swappiness = Some(memory_swappiness);
    }

    pub fn with_memory_swappiness(mut self, memory_swappiness: i64) -> Self {
        self.memory_swappiness = Some(memory_swappiness);
        self
    }

    pub fn memory_swappiness(&self) -> Option<i64> {
        self.memory_swappiness
    }

    pub fn reset_memory_swappiness(&mut self) {
        self.memory_swappiness = None;
    }

    pub fn set_nano_cp_us(&mut self, nano_cp_us: i64) {
        self.nano_cp_us = Some(nano_cp_us);
    }

    pub fn with_nano_cp_us(mut self, nano_cp_us: i64) -> Self {
        self.nano_cp_us = Some(nano_cp_us);
        self
    }

    pub fn nano_cp_us(&self) -> Option<i64> {
        self.nano_cp_us
    }

    pub fn reset_nano_cp_us(&mut self) {
        self.nano_cp_us = None;
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

    pub fn set_pids_limit(&mut self, pids_limit: i64) {
        self.pids_limit = Some(pids_limit);
    }

    pub fn with_pids_limit(mut self, pids_limit: i64) -> Self {
        self.pids_limit = Some(pids_limit);
        self
    }

    pub fn pids_limit(&self) -> Option<i64> {
        self.pids_limit
    }

    pub fn reset_pids_limit(&mut self) {
        self.pids_limit = None;
    }

    pub fn set_ulimits(&mut self, ulimits: Vec<crate::models::ResourcesUlimits>) {
        self.ulimits = Some(ulimits);
    }

    pub fn with_ulimits(mut self, ulimits: Vec<crate::models::ResourcesUlimits>) -> Self {
        self.ulimits = Some(ulimits);
        self
    }

    pub fn ulimits(&self) -> Option<&[crate::models::ResourcesUlimits]> {
        self.ulimits.as_ref().map(AsRef::as_ref)
    }

    pub fn reset_ulimits(&mut self) {
        self.ulimits = None;
    }

    pub fn set_cpu_count(&mut self, cpu_count: i64) {
        self.cpu_count = Some(cpu_count);
    }

    pub fn with_cpu_count(mut self, cpu_count: i64) -> Self {
        self.cpu_count = Some(cpu_count);
        self
    }

    pub fn cpu_count(&self) -> Option<i64> {
        self.cpu_count
    }

    pub fn reset_cpu_count(&mut self) {
        self.cpu_count = None;
    }

    pub fn set_cpu_percent(&mut self, cpu_percent: i64) {
        self.cpu_percent = Some(cpu_percent);
    }

    pub fn with_cpu_percent(mut self, cpu_percent: i64) -> Self {
        self.cpu_percent = Some(cpu_percent);
        self
    }

    pub fn cpu_percent(&self) -> Option<i64> {
        self.cpu_percent
    }

    pub fn reset_cpu_percent(&mut self) {
        self.cpu_percent = None;
    }

    pub fn set_io_maximum_i_ops(&mut self, io_maximum_i_ops: i64) {
        self.io_maximum_i_ops = Some(io_maximum_i_ops);
    }

    pub fn with_io_maximum_i_ops(mut self, io_maximum_i_ops: i64) -> Self {
        self.io_maximum_i_ops = Some(io_maximum_i_ops);
        self
    }

    pub fn io_maximum_i_ops(&self) -> Option<i64> {
        self.io_maximum_i_ops
    }

    pub fn reset_io_maximum_i_ops(&mut self) {
        self.io_maximum_i_ops = None;
    }

    pub fn set_io_maximum_bandwidth(&mut self, io_maximum_bandwidth: i64) {
        self.io_maximum_bandwidth = Some(io_maximum_bandwidth);
    }

    pub fn with_io_maximum_bandwidth(mut self, io_maximum_bandwidth: i64) -> Self {
        self.io_maximum_bandwidth = Some(io_maximum_bandwidth);
        self
    }

    pub fn io_maximum_bandwidth(&self) -> Option<i64> {
        self.io_maximum_bandwidth
    }

    pub fn reset_io_maximum_bandwidth(&mut self) {
        self.io_maximum_bandwidth = None;
    }
}
