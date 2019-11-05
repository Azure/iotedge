use std::collections::HashMap;

use serde::{Deserialize, Serialize};

/// Spec is the base configuration for the container.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Spec {
    /// Version of the Open Container Runtime Specification with which the
    /// bundle complies.
    #[serde(rename = "ociVersion")]
    pub version: String,
    /// Process configures the container process.
    #[serde(rename = "process", skip_serializing_if = "Option::is_none")]
    pub process: Option<Process>,
    /// Root configures the container's root filesystem.
    #[serde(rename = "root", skip_serializing_if = "Option::is_none")]
    pub root: Option<Root>,
    /// Hostname configures the container's hostname.
    #[serde(rename = "hostname", skip_serializing_if = "Option::is_none")]
    pub hostname: Option<String>,
    /// Mounts configures additional mounts (on top of Root).
    #[serde(rename = "mounts", skip_serializing_if = "Option::is_none")]
    pub mounts: Option<Vec<Mount>>,
    /// Hooks configures callbacks for container lifecycle events.
    #[serde(rename = "hooks", skip_serializing_if = "Option::is_none")]
    pub hooks: Option<Hooks>,
    /// Annotations contains arbitrary metadata for the container.
    #[serde(rename = "annotations", skip_serializing_if = "Option::is_none")]
    pub annotations: Option<HashMap<String, String>>,
    /// Linux is platform-specific configuration for Linux based containers.
    #[serde(rename = "linux", skip_serializing_if = "Option::is_none")]
    pub linux: Option<Linux>,
    /// Solaris is platform-specific configuration for Solaris based containers.
    #[serde(rename = "solaris", skip_serializing_if = "Option::is_none")]
    pub solaris: Option<Solaris>,
    /// Windows is platform-specific configuration for Windows based containers.
    #[serde(rename = "windows", skip_serializing_if = "Option::is_none")]
    pub windows: Option<Windows>,
}

/// Process contains information to start a specific application inside the
/// container.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Process {
    /// Terminal creates an interactive terminal for the container.
    #[serde(rename = "terminal", skip_serializing_if = "Option::is_none")]
    pub terminal: Option<bool>,
    /// ConsoleSize specifies the size of the console.
    #[serde(rename = "consoleSize", skip_serializing_if = "Option::is_none")]
    pub console_size: Option<Box>,
    /// User specifies user information for the process.
    #[serde(rename = "user")]
    pub user: User,
    /// Args specifies the binary and arguments for the application to execute.
    #[serde(rename = "args")]
    pub args: Vec<String>,
    /// Env populates the process environment for the process.
    #[serde(rename = "env", skip_serializing_if = "Option::is_none")]
    pub env: Option<Vec<String>>,
    /// Cwd is the current working directory for the process and must be
    /// relative to the container's root.
    #[serde(rename = "cwd")]
    pub cwd: String,
    /// Capabilities are Linux capabilities that are kept for the process.
    #[serde(rename = "capabilities", skip_serializing_if = "Option::is_none")]
    pub capabilities: Option<LinuxCapabilities>,
    /// Rlimits specifies rlimit options to apply to the process.
    #[serde(rename = "rlimits", skip_serializing_if = "Option::is_none")]
    pub rlimits: Option<Vec<POSIXRlimit>>,
    /// NoNewPrivileges controls whether additional privileges could be gained
    /// by processes in the container.
    #[serde(rename = "noNewPrivileges", skip_serializing_if = "Option::is_none")]
    pub no_new_privileges: Option<bool>,
    /// ApparmorProfile specifies the apparmor profile for the container.
    #[serde(rename = "apparmorProfile", skip_serializing_if = "Option::is_none")]
    pub apparmor_profile: Option<String>,
    /// Specify an oom_score_adj for the container.
    #[serde(rename = "oomScoreAdj", skip_serializing_if = "Option::is_none")]
    pub o_o_m_score_adj: Option<isize>,
    /// SelinuxLabel specifies the selinux context that the container process is
    /// run as.
    #[serde(rename = "selinuxLabel", skip_serializing_if = "Option::is_none")]
    pub selinux_label: Option<String>,
}

/// LinuxCapabilities specifies the whitelist of capabilities that are kept for
/// a process. http://man7.org/linux/man-pages/man7/capabilities.7.html
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxCapabilities {
    /// Bounding is the set of capabilities checked by the kernel.
    #[serde(rename = "bounding", skip_serializing_if = "Option::is_none")]
    pub bounding: Option<Vec<String>>,
    /// Effective is the set of capabilities checked by the kernel.
    #[serde(rename = "effective", skip_serializing_if = "Option::is_none")]
    pub effective: Option<Vec<String>>,
    /// Inheritable is the capabilities preserved across execve.
    #[serde(rename = "inheritable", skip_serializing_if = "Option::is_none")]
    pub inheritable: Option<Vec<String>>,
    /// Permitted is the limiting superset for effective capabilities.
    #[serde(rename = "permitted", skip_serializing_if = "Option::is_none")]
    pub permitted: Option<Vec<String>>,
    /// Ambient is the ambient set of capabilities that are kept.
    #[serde(rename = "ambient", skip_serializing_if = "Option::is_none")]
    pub ambient: Option<Vec<String>>,
}

/// Box specifies dimensions of a rectangle. Used for specifying the size of a
/// console.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Box {
    /// Height is the vertical dimension of a box.
    #[serde(rename = "height")]
    pub height: usize,
    /// Width is the horizontal dimension of a box.
    #[serde(rename = "width")]
    pub width: usize,
}

/// User specifies specific user (and group) information for the container
/// process.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct User {
    /// UID is the user id.
    #[serde(rename = "uid")]
    pub uid: u32,
    /// GID is the group id.
    #[serde(rename = "gid")]
    pub gid: u32,
    /// AdditionalGids are additional group ids set for the container's process.
    #[serde(rename = "additionalGids", skip_serializing_if = "Option::is_none")]
    pub additional_gids: Option<Vec<u32>>,
    /// Username is the user name.
    #[serde(rename = "username", skip_serializing_if = "Option::is_none")]
    pub username: Option<String>,
}

/// Root contains information about the container's root filesystem on the host.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Root {
    /// Path is the absolute path to the container's root filesystem.
    #[serde(rename = "path")]
    pub path: String,
    /// Readonly makes the root filesystem for the container readonly before the
    /// process is executed.
    #[serde(rename = "readonly", skip_serializing_if = "Option::is_none")]
    pub readonly: Option<bool>,
}

/// Mount specifies a mount for a container.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Mount {
    /// Destination is the absolute path where the mount will be placed in the
    /// container.
    #[serde(rename = "destination")]
    pub destination: String,
    /// Type specifies the mount kind.
    #[serde(rename = "type", skip_serializing_if = "Option::is_none")]
    pub type_: Option<String>,
    /// Source specifies the source path of the mount.
    #[serde(rename = "source", skip_serializing_if = "Option::is_none")]
    pub source: Option<String>,
    /// Options are fstab style mount options.
    #[serde(rename = "options", skip_serializing_if = "Option::is_none")]
    pub options: Option<Vec<String>>,
}

/// Hook specifies a command that is run at a particular event in the lifecycle
/// of a container
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Hook {
    #[serde(rename = "path")]
    pub path: String,
    #[serde(rename = "args", skip_serializing_if = "Option::is_none")]
    pub args: Option<Vec<String>>,
    #[serde(rename = "env", skip_serializing_if = "Option::is_none")]
    pub env: Option<Vec<String>>,
    #[serde(rename = "timeout", skip_serializing_if = "Option::is_none")]
    pub timeout: Option<isize>,
}

/// Hooks for container setup and teardown
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Hooks {
    /// Prestart is a list of hooks to be run before the container process is
    /// executed.
    #[serde(rename = "prestart", skip_serializing_if = "Option::is_none")]
    pub prestart: Option<Vec<Hook>>,
    /// Poststart is a list of hooks to be run after the container process is
    /// started.
    #[serde(rename = "poststart", skip_serializing_if = "Option::is_none")]
    pub poststart: Option<Vec<Hook>>,
    /// Poststop is a list of hooks to be run after the container process exits.
    #[serde(rename = "poststop", skip_serializing_if = "Option::is_none")]
    pub poststop: Option<Vec<Hook>>,
}

/// Linux contains platform-specific configuration for Linux based containers.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Linux {
    /// UIDMapping specifies user mappings for supporting user namespaces.
    #[serde(rename = "uidMappings", skip_serializing_if = "Option::is_none")]
    pub ui_d_mappings: Option<Vec<LinuxIDMapping>>,
    /// GIDMapping specifies group mappings for supporting user namespaces.
    #[serde(rename = "gidMappings", skip_serializing_if = "Option::is_none")]
    pub g_id_mappings: Option<Vec<LinuxIDMapping>>,
    /// Sysctl are a set of key value pairs that are set for the container on
    /// start
    #[serde(rename = "sysctl", skip_serializing_if = "Option::is_none")]
    pub sysctl: Option<HashMap<String, String>>,
    /// Resources contain cgroup information for handling resource constraints
    /// for the container
    #[serde(rename = "resources", skip_serializing_if = "Option::is_none")]
    pub resources: Option<LinuxResources>,
    /// CgroupsPath specifies the path to cgroups that are created and/or joined
    /// by the container. The path is expected to be relative to the cgroups
    /// mountpoint. If resources are specified, the cgroups at CgroupsPath
    /// will be updated based on resources.
    #[serde(rename = "cgroupsPath", skip_serializing_if = "Option::is_none")]
    pub cgroups_path: Option<String>,
    /// Namespaces contains the namespaces that are created and/or joined by the
    /// container
    #[serde(rename = "namespaces", skip_serializing_if = "Option::is_none")]
    pub namespaces: Option<Vec<LinuxNamespace>>,
    /// Devices are a list of device nodes that are created for the container
    #[serde(rename = "devices", skip_serializing_if = "Option::is_none")]
    pub devices: Option<Vec<LinuxDevice>>,
    /// Seccomp specifies the seccomp security settings for the container.
    #[serde(rename = "seccomp", skip_serializing_if = "Option::is_none")]
    pub seccomp: Option<LinuxSeccomp>,
    /// RootfsPropagation is the rootfs mount propagation mode for the
    /// container.
    #[serde(rename = "rootfsPropagation", skip_serializing_if = "Option::is_none")]
    pub rootfs_propagation: Option<String>,
    /// MaskedPaths masks over the provided paths inside the container.
    #[serde(rename = "maskedPaths", skip_serializing_if = "Option::is_none")]
    pub masked_paths: Option<Vec<String>>,
    /// ReadonlyPaths sets the provided paths as RO inside the container.
    #[serde(rename = "readonlyPaths", skip_serializing_if = "Option::is_none")]
    pub readonly_paths: Option<Vec<String>>,
    /// MountLabel specifies the selinux context for the mounts in the
    /// container.
    #[serde(rename = "mountLabel", skip_serializing_if = "Option::is_none")]
    pub mount_label: Option<String>,
    /// IntelRdt contains Intel Resource Director Technology (RDT) information
    /// for handling resource constraints (e.g., L3 cache) for the container
    #[serde(rename = "intelRdt", skip_serializing_if = "Option::is_none")]
    pub intel_rdt: Option<LinuxIntelRdt>,
}

/// LinuxNamespace is the configuration for a Linux namespace
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxNamespace {
    /// Type is the type of namespace
    #[serde(rename = "type")]
    pub type_: LinuxNamespaceType,
    /// Path is a path to an existing namespace persisted on disk that can be
    /// joined and is of the same type
    #[serde(rename = "path", skip_serializing_if = "Option::is_none")]
    pub path: Option<String>,
}

/// LinuxNamespaceType is one of the Linux namespaces
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub enum LinuxNamespaceType {
    /// PIDNamespace for isolating process IDs
    #[serde(rename = "pid")]
    PIDNamespace,
    /// NetworkNamespace for isolating network devices, stacks, ports, etc
    #[serde(rename = "network")]
    NetworkNamespace,
    /// MountNamespace for isolating mount points
    #[serde(rename = "mount")]
    MountNamespace,
    /// IPCNamespace for isolating System V IPC, POSIX message queues
    #[serde(rename = "ipc")]
    IPCNamespace,
    /// UTSNamespace for isolating hostname and NIS domain name
    #[serde(rename = "uts")]
    UTSNamespace,
    /// UserNamespace for isolating user and group IDs
    #[serde(rename = "user")]
    UserNamespace,
    /// CgroupNamespace for isolating cgroup hierarchies
    #[serde(rename = "cgroup")]
    CgroupNamespace,
}

impl std::fmt::Display for LinuxNamespaceType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", serde_json::to_string(self).unwrap())
    }
}

/// LinuxIDMapping specifies UID/GID mappings
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxIDMapping {
    /// HostID is the starting UID/GID on the host to be mapped to 'ContainerID'
    #[serde(rename = "hostID")]
    pub host_id: u32,
    /// ContainerID is the starting UID/GID in the container
    #[serde(rename = "containerID")]
    pub container_id: u32,
    /// Size is the number of IDs to be mapped
    #[serde(rename = "size")]
    pub size: u32,
}

/// POSIXRlimit type and restrictions
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct POSIXRlimit {
    /// Type of the rlimit to set
    #[serde(rename = "type")]
    pub type_: String,
    /// Hard is the hard limit for the specified type
    #[serde(rename = "hard")]
    pub hard: u64,
    /// Soft is the soft limit for the specified type
    #[serde(rename = "soft")]
    pub soft: u64,
}

/// LinuxHugepageLimit structure corresponds to limiting kernel hugepages
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxHugepageLimit {
    /// Pagesize is the hugepage size
    #[serde(rename = "pageSize")]
    pub pagesize: String,
    /// Limit is the limit of "hugepagesize" hugetlb usage
    #[serde(rename = "limit")]
    pub limit: u64,
}

/// LinuxInterfacePriority for network interfaces
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxInterfacePriority {
    /// Name is the name of the network interface
    #[serde(rename = "name")]
    pub name: String,
    /// Priority for the interface
    #[serde(rename = "priority")]
    pub priority: u32,
}

/// LinuxWeightDevice struct holds a `major:minor weight` pair for weightDevice
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxWeightDevice {
    /// Major is the device's major number.
    #[serde(rename = "major")]
    pub major: i64,
    /// Minor is the device's minor number.
    #[serde(rename = "minor")]
    pub minor: i64,
    /// Weight is the bandwidth rate for the device.
    #[serde(rename = "weight", skip_serializing_if = "Option::is_none")]
    pub weight: Option<u16>,
    /// LeafWeight is the bandwidth rate for the device while competing with the
    /// cgroup's child cgroups, CFQ scheduler only
    #[serde(rename = "leafWeight", skip_serializing_if = "Option::is_none")]
    pub leaf_weight: Option<u16>,
}

/// LinuxThrottleDevice struct holds a `major:minor rate_per_second` pair
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxThrottleDevice {
    /// Major is the device's major number.
    #[serde(rename = "major")]
    pub major: i64,
    /// Minor is the device's minor number.
    #[serde(rename = "minor")]
    pub minor: i64,
    /// Rate is the IO rate limit per cgroup per device
    #[serde(rename = "rate")]
    pub rate: u64,
}

/// LinuxBlockIO for Linux cgroup 'blkio' resource management
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxBlockIO {
    /// Specifies per cgroup weight
    #[serde(rename = "weight", skip_serializing_if = "Option::is_none")]
    pub weight: Option<u16>,
    /// Specifies tasks' weight in the given cgroup while competing with the
    /// cgroup's child cgroups, CFQ scheduler only
    #[serde(rename = "leafWeight", skip_serializing_if = "Option::is_none")]
    pub leaf_weight: Option<u16>,
    /// Weight per cgroup per device, can override BlkioWeight
    #[serde(rename = "weightDevice", skip_serializing_if = "Option::is_none")]
    pub weight_device: Option<Vec<LinuxWeightDevice>>,
    /// IO read rate limit per cgroup per device, bytes per second
    #[serde(
        rename = "throttleReadBpsDevice",
        skip_serializing_if = "Option::is_none"
    )]
    pub throttle_read_bps_device: Option<Vec<LinuxThrottleDevice>>,
    /// IO write rate limit per cgroup per device, bytes per second
    #[serde(
        rename = "throttleWriteBpsDevice",
        skip_serializing_if = "Option::is_none"
    )]
    pub throttle_write_bps_device: Option<Vec<LinuxThrottleDevice>>,
    /// IO read rate limit per cgroup per device, IO per second
    #[serde(
        rename = "throttleReadIOPSDevice",
        skip_serializing_if = "Option::is_none"
    )]
    pub throttle_read_i_o_p_s_device: Option<Vec<LinuxThrottleDevice>>,
    /// IO write rate limit per cgroup per device, IO per second
    #[serde(
        rename = "throttleWriteIOPSDevice",
        skip_serializing_if = "Option::is_none"
    )]
    pub throttle_write_i_o_p_s_device: Option<Vec<LinuxThrottleDevice>>,
}

/// LinuxMemory for Linux cgroup 'memory' resource management
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxMemory {
    /// Memory limit (in bytes).
    #[serde(rename = "limit", skip_serializing_if = "Option::is_none")]
    pub limit: Option<i64>,
    /// Memory reservation or soft_limit (in bytes).
    #[serde(rename = "reservation", skip_serializing_if = "Option::is_none")]
    pub reservation: Option<i64>,
    /// Total memory limit (memory + swap).
    #[serde(rename = "swap", skip_serializing_if = "Option::is_none")]
    pub swap: Option<i64>,
    /// Kernel memory limit (in bytes).
    #[serde(rename = "kernel", skip_serializing_if = "Option::is_none")]
    pub kernel: Option<i64>,
    /// Kernel memory limit for tcp (in bytes)
    #[serde(rename = "kernelTCP", skip_serializing_if = "Option::is_none")]
    pub kernel_t_c_p: Option<i64>,
    /// How aggressive the kernel will swap memory pages.
    #[serde(rename = "swappiness", skip_serializing_if = "Option::is_none")]
    pub swappiness: Option<u64>,
    /// DisableOOMKiller disables the OOM killer for out of memory conditions
    #[serde(rename = "disableOOMKiller", skip_serializing_if = "Option::is_none")]
    pub disable_o_o_m_killer: Option<bool>,
}

/// LinuxCPU for Linux cgroup 'cpu' resource management
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxCPU {
    /// CPU shares (relative weight (ratio) vs. other cgroups with cpu shares).
    #[serde(rename = "shares", skip_serializing_if = "Option::is_none")]
    pub shares: Option<u64>,
    /// CPU hardcap limit (in usecs). Allowed cpu time in a given period.
    #[serde(rename = "quota", skip_serializing_if = "Option::is_none")]
    pub quota: Option<i64>,
    /// CPU period to be used for hardcapping (in usecs).
    #[serde(rename = "period", skip_serializing_if = "Option::is_none")]
    pub period: Option<u64>,
    /// How much time realtime scheduling may use (in usecs).
    #[serde(rename = "realtimeRuntime", skip_serializing_if = "Option::is_none")]
    pub realtime_runtime: Option<i64>,
    /// CPU period to be used for realtime scheduling (in usecs).
    #[serde(rename = "realtimePeriod", skip_serializing_if = "Option::is_none")]
    pub realtime_period: Option<u64>,
    /// CPUs to use within the cpuset. Default is to use any CPU available.
    #[serde(rename = "cpus", skip_serializing_if = "Option::is_none")]
    pub cpus: Option<String>,
    /// List of memory nodes in the cpuset. Default is to use any available
    /// memory node.
    #[serde(rename = "mems", skip_serializing_if = "Option::is_none")]
    pub mems: Option<String>,
}

/// LinuxPids for Linux cgroup 'pids' resource management (Linux 4.3)
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxPids {
    /// Maximum number of PIDs. Default is "no limit".
    #[serde(rename = "limit")]
    pub limit: i64,
}

/// LinuxNetwork identification and priority configuration
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxNetwork {
    /// Set class identifier for container's network packets
    #[serde(rename = "classID", skip_serializing_if = "Option::is_none")]
    pub class_id: Option<u32>,
    /// Set priority of network traffic for container
    #[serde(rename = "priorities", skip_serializing_if = "Option::is_none")]
    pub priorities: Option<Vec<LinuxInterfacePriority>>,
}

/// LinuxResources has container runtime resource constraints
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxResources {
    /// Devices configures the device whitelist.
    #[serde(rename = "devices", skip_serializing_if = "Option::is_none")]
    pub devices: Option<Vec<LinuxDeviceCgroup>>,
    /// Memory restriction configuration
    #[serde(rename = "memory", skip_serializing_if = "Option::is_none")]
    pub memory: Option<LinuxMemory>,
    /// CPU resource restriction configuration
    #[serde(rename = "cpu", skip_serializing_if = "Option::is_none")]
    pub cpu: Option<LinuxCPU>,
    /// Task resource restriction configuration.
    #[serde(rename = "pids", skip_serializing_if = "Option::is_none")]
    pub pids: Option<LinuxPids>,
    /// BlockIO restriction configuration
    #[serde(rename = "blockIO", skip_serializing_if = "Option::is_none")]
    pub block_i_o: Option<LinuxBlockIO>,
    /// Hugetlb limit (in bytes)
    #[serde(rename = "hugepageLimits", skip_serializing_if = "Option::is_none")]
    pub hugepage_limits: Option<Vec<LinuxHugepageLimit>>,
    /// Network restriction configuration
    #[serde(rename = "network", skip_serializing_if = "Option::is_none")]
    pub network: Option<LinuxNetwork>,
}

/// LinuxDevice represents the mknod information for a Linux special device file
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxDevice {
    /// Path to the device.
    #[serde(rename = "path")]
    pub path: String,
    /// Device type, block, char, etc.
    #[serde(rename = "type")]
    pub type_: String,
    /// Major is the device's major number.
    #[serde(rename = "major")]
    pub major: i64,
    /// Minor is the device's minor number.
    #[serde(rename = "minor")]
    pub minor: i64,
    /// FileMode permission bits for the device.
    #[serde(rename = "fileMode", skip_serializing_if = "Option::is_none")]
    pub file_mode: Option<u32>, // TODO: this should match go's os.FileMode type
    /// UID of the device.
    #[serde(rename = "uid", skip_serializing_if = "Option::is_none")]
    pub uid: Option<u32>,
    /// Gid of the device.
    #[serde(rename = "gid", skip_serializing_if = "Option::is_none")]
    pub gid: Option<u32>,
}

/// LinuxDeviceCgroup represents a device rule for the whitelist controller
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxDeviceCgroup {
    /// Allow or deny
    #[serde(rename = "allow")]
    pub allow: bool,
    /// Device type, block, char, etc.
    #[serde(rename = "type", skip_serializing_if = "Option::is_none")]
    pub type_: Option<String>,
    /// Major is the device's major number.
    #[serde(rename = "major", skip_serializing_if = "Option::is_none")]
    pub major: Option<i64>,
    /// Minor is the device's minor number.
    #[serde(rename = "minor", skip_serializing_if = "Option::is_none")]
    pub minor: Option<i64>,
    /// Cgroup access permissions format, rwm.
    #[serde(rename = "access", skip_serializing_if = "Option::is_none")]
    pub access: Option<String>,
}

/// Solaris contains platform-specific configuration for Solaris application
/// containers.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Solaris {
    /// SMF FMRI which should go "online" before we start the container process.
    #[serde(rename = "milestone", skip_serializing_if = "Option::is_none")]
    pub milestone: Option<String>,
    /// Maximum set of privileges any process in this container can obtain.
    #[serde(rename = "limitpriv", skip_serializing_if = "Option::is_none")]
    pub limit_priv: Option<String>,
    /// The maximum amount of shared memory allowed for this container.
    #[serde(rename = "maxShmMemory", skip_serializing_if = "Option::is_none")]
    pub max_shm_memory: Option<String>,
    /// Specification for automatic creation of network resources for this
    /// container.
    #[serde(rename = "anet", skip_serializing_if = "Option::is_none")]
    pub anet: Option<Vec<SolarisAnet>>,
    /// Set limit on the amount of CPU time that can be used by container.
    #[serde(rename = "cappedCPU", skip_serializing_if = "Option::is_none")]
    pub capped_c_p_u: Option<SolarisCappedCPU>,
    /// The physical and swap caps on the memory that can be used by this
    /// container.
    #[serde(rename = "cappedMemory", skip_serializing_if = "Option::is_none")]
    pub capped_memory: Option<SolarisCappedMemory>,
}

/// SolarisCappedCPU allows users to set limit on the amount of CPU time that
/// can be used by container.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct SolarisCappedCPU {
    #[serde(rename = "ncpus", skip_serializing_if = "Option::is_none")]
    pub ncpus: Option<String>,
}

/// SolarisCappedMemory allows users to set the physical and swap caps on the
/// memory that can be used by this container.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct SolarisCappedMemory {
    #[serde(rename = "physical", skip_serializing_if = "Option::is_none")]
    pub physical: Option<String>,
    #[serde(rename = "swap", skip_serializing_if = "Option::is_none")]
    pub swap: Option<String>,
}

/// SolarisAnet provides the specification for automatic creation of network
/// resources for this container.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct SolarisAnet {
    /// Specify a name for the automatically created VNIC datalink.
    #[serde(rename = "linkname", skip_serializing_if = "Option::is_none")]
    pub linkname: Option<String>,
    /// Specify the link over which the VNIC will be created.
    #[serde(rename = "lowerLink", skip_serializing_if = "Option::is_none")]
    pub lowerlink: Option<String>,
    /// The set of IP addresses that the container can use.
    #[serde(rename = "allowedAddress", skip_serializing_if = "Option::is_none")]
    pub allowedaddr: Option<String>,
    /// Specifies whether allowedAddress limitation is to be applied to the
    /// VNIC.
    #[serde(
        rename = "configureAllowedAddress",
        skip_serializing_if = "Option::is_none"
    )]
    pub configallowedaddr: Option<String>,
    /// The value of the optional default router.
    #[serde(rename = "defrouter", skip_serializing_if = "Option::is_none")]
    pub defrouter: Option<String>,
    /// Enable one or more types of link protection.
    #[serde(rename = "linkProtection", skip_serializing_if = "Option::is_none")]
    pub linkprotection: Option<String>,
    /// Set the VNIC's macAddress
    #[serde(rename = "macAddress", skip_serializing_if = "Option::is_none")]
    pub macaddress: Option<String>,
}

/// Windows defines the runtime configuration for Windows based containers,
/// including Hyper-V containers.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct Windows {
    /// LayerFolders contains a list of absolute paths to directories containing
    /// image layers.
    #[serde(rename = "layerFolders")]
    pub layer_folders: Vec<String>,
    /// Resources contains information for handling resource constraints for the
    /// container.
    #[serde(rename = "resources", skip_serializing_if = "Option::is_none")]
    pub resources: Option<WindowsResources>,
    // XXX: work around serde_json::Value not supportting #[derive(Eq)]
    //
    // /// CredentialSpec contains a JSON object describing a group Managed Service
    // /// Account (gMSA) specification.
    // #[serde(rename = "credentialSpec", skip_serializing_if = "Option::is_none")]
    // pub credential_spec: Option<serde_json::Value>,
    /// Servicing indicates if the container is being started in a mode to apply
    /// a Windows Update servicing operation.
    #[serde(rename = "servicing", skip_serializing_if = "Option::is_none")]
    pub servicing: Option<bool>,
    /// IgnoreFlushesDuringBoot indicates if the container is being started in a
    /// mode where disk writes are not flushed during its boot process.
    #[serde(
        rename = "ignoreFlushesDuringBoot",
        skip_serializing_if = "Option::is_none"
    )]
    pub ignore_flushes_during_boot: Option<bool>,
    /// HyperV contains information for running a container with Hyper-V
    /// isolation.
    #[serde(rename = "hyperv", skip_serializing_if = "Option::is_none")]
    pub hyper_v: Option<WindowsHyperV>,
    /// Network restriction configuration.
    #[serde(rename = "network", skip_serializing_if = "Option::is_none")]
    pub network: Option<WindowsNetwork>,
}

/// WindowsResources has container runtime resource constraints for containers
/// running on Windows.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct WindowsResources {
    /// Memory restriction configuration.
    #[serde(rename = "memory", skip_serializing_if = "Option::is_none")]
    pub memory: Option<WindowsMemoryResources>,
    /// CPU resource restriction configuration.
    #[serde(rename = "cpu", skip_serializing_if = "Option::is_none")]
    pub cpu: Option<WindowsCPUResources>,
    /// Storage restriction configuration.
    #[serde(rename = "storage", skip_serializing_if = "Option::is_none")]
    pub storage: Option<WindowsStorageResources>,
}

/// WindowsMemoryResources contains memory resource management settings.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct WindowsMemoryResources {
    /// Memory limit in bytes.
    #[serde(rename = "limit", skip_serializing_if = "Option::is_none")]
    pub limit: Option<u64>,
}

/// WindowsCPUResources contains CPU resource management settings.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct WindowsCPUResources {
    /// Number of CPUs available to the container.
    #[serde(rename = "count", skip_serializing_if = "Option::is_none")]
    pub count: Option<u64>,
    /// CPU shares (relative weight to other containers with cpu shares).
    #[serde(rename = "shares", skip_serializing_if = "Option::is_none")]
    pub shares: Option<u16>,
    /// Specifies the portion of processor cycles that this container can use as
    /// a percentage times 100.
    #[serde(rename = "maximum", skip_serializing_if = "Option::is_none")]
    pub maximum: Option<u16>,
}

/// WindowsStorageResources contains storage resource management settings.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct WindowsStorageResources {
    /// Specifies maximum Iops for the system drive.
    #[serde(rename = "iops", skip_serializing_if = "Option::is_none")]
    pub iops: Option<u64>,
    /// Specifies maximum bytes per second for the system drive.
    #[serde(rename = "bps", skip_serializing_if = "Option::is_none")]
    pub bps: Option<u64>,
    /// Sandbox size specifies the minimum size of the system drive in bytes.
    #[serde(rename = "sandboxSize", skip_serializing_if = "Option::is_none")]
    pub sandbox_size: Option<u64>,
}

/// WindowsNetwork contains network settings for Windows containers.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct WindowsNetwork {
    /// List of HNS endpoints that the container should connect to.
    #[serde(rename = "endpointList", skip_serializing_if = "Option::is_none")]
    pub endpoint_list: Option<Vec<String>>,
    /// Specifies if unqualified DNS name resolution is allowed.
    #[serde(
        rename = "allowUnqualifiedDNSQuery",
        skip_serializing_if = "Option::is_none"
    )]
    pub allow_unqualified_d_n_s_query: Option<bool>,
    /// Comma separated list of DNS suffixes to use for name resolution.
    #[serde(rename = "DNSSearchList", skip_serializing_if = "Option::is_none")]
    pub d_n_s_search_list: Option<Vec<String>>,
    /// Name (ID) of the container that we will share with the network stack.
    #[serde(
        rename = "networkSharedContainerName",
        skip_serializing_if = "Option::is_none"
    )]
    pub network_shared_container_name: Option<String>,
}

/// WindowsHyperV contains information for configuring a container to run with
/// Hyper-V isolation.
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct WindowsHyperV {
    /// UtilityVMPath is an optional path to the image used for the Utility VM.
    #[serde(rename = "utilityVMPath", skip_serializing_if = "Option::is_none")]
    pub utility_v_m_path: Option<String>,
}

/// LinuxSeccomp represents syscall restrictions
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxSeccomp {
    #[serde(rename = "defaultAction")]
    pub default_action: LinuxSeccompAction,
    #[serde(rename = "architectures", skip_serializing_if = "Option::is_none")]
    pub architectures: Option<Vec<Arch>>,
    #[serde(rename = "syscalls", skip_serializing_if = "Option::is_none")]
    pub syscalls: Option<Vec<LinuxSyscall>>,
}

/// Additional architectures permitted to be used for system calls
/// By default only the native architecture of the kernel is permitted
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub enum Arch {
    #[serde(rename = "SCMP_ARCH_X86")]
    X86,
    #[serde(rename = "SCMP_ARCH_X86_64")]
    X86_64,
    #[serde(rename = "SCMP_ARCH_X32")]
    X32,
    #[serde(rename = "SCMP_ARCH_ARM")]
    ARM,
    #[serde(rename = "SCMP_ARCH_AARCH64")]
    AARCH64,
    #[serde(rename = "SCMP_ARCH_MIPS")]
    MIPS,
    #[serde(rename = "SCMP_ARCH_MIPS64")]
    MIPS64,
    #[serde(rename = "SCMP_ARCH_MIPS64N32")]
    MIPS64N32,
    #[serde(rename = "SCMP_ARCH_MIPSEL")]
    MIPSEL,
    #[serde(rename = "SCMP_ARCH_MIPSEL64")]
    MIPSEL64,
    #[serde(rename = "SCMP_ARCH_MIPSEL64N32")]
    MIPSEL64N32,
    #[serde(rename = "SCMP_ARCH_PPC")]
    PPC,
    #[serde(rename = "SCMP_ARCH_PPC64")]
    PPC64,
    #[serde(rename = "SCMP_ARCH_PPC64LE")]
    PPC64LE,
    #[serde(rename = "SCMP_ARCH_S390")]
    S390,
    #[serde(rename = "SCMP_ARCH_S390X")]
    S390X,
    #[serde(rename = "SCMP_ARCH_PARISC")]
    PARISC,
    #[serde(rename = "SCMP_ARCH_PARISC64")]
    PARISC64,
}

impl std::fmt::Display for Arch {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", serde_json::to_string(self).unwrap())
    }
}

/// Define actions for Seccomp rules
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub enum LinuxSeccompAction {
    #[serde(rename = "SCMP_ACT_KILL")]
    ActKill,
    #[serde(rename = "SCMP_ACT_TRAP")]
    ActTrap,
    #[serde(rename = "SCMP_ACT_ERRNO")]
    ActErrno,
    #[serde(rename = "SCMP_ACT_TRACE")]
    ActTrace,
    #[serde(rename = "SCMP_ACT_ALLOW")]
    ActAllow,
}

impl std::fmt::Display for LinuxSeccompAction {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", serde_json::to_string(self).unwrap())
    }
}

/// Define operators for syscall arguments in Seccomp
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub enum LinuxSeccompOperator {
    #[serde(rename = "SCMP_CMP_NE")]
    OpNotEqual,
    #[serde(rename = "SCMP_CMP_LT")]
    OpLessThan,
    #[serde(rename = "SCMP_CMP_LE")]
    OpLessEqual,
    #[serde(rename = "SCMP_CMP_EQ")]
    OpEqualTo,
    #[serde(rename = "SCMP_CMP_GE")]
    OpGreaterEqual,
    #[serde(rename = "SCMP_CMP_GT")]
    OpGreaterThan,
    #[serde(rename = "SCMP_CMP_MASKED_EQ")]
    OpMaskedEqual,
}

impl std::fmt::Display for LinuxSeccompOperator {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", serde_json::to_string(self).unwrap())
    }
}

/// LinuxSeccompArg used for matching specific syscall arguments in Seccomp
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxSeccompArg {
    #[serde(rename = "index")]
    pub index: usize,
    #[serde(rename = "value")]
    pub value: u64,
    #[serde(rename = "valueTwo", skip_serializing_if = "Option::is_none")]
    pub value_two: Option<u64>,
    #[serde(rename = "op")]
    pub op: LinuxSeccompOperator,
}

/// LinuxSyscall is used to match a syscall in Seccomp
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxSyscall {
    #[serde(rename = "names")]
    pub names: Vec<String>,
    #[serde(rename = "action")]
    pub action: LinuxSeccompAction,
    #[serde(rename = "args", skip_serializing_if = "Option::is_none")]
    pub args: Option<Vec<LinuxSeccompArg>>,
}

/// LinuxIntelRdt has container runtime resource constraints
/// for Intel RDT/CAT which introduced in Linux 4.10 kernel
#[derive(Debug, Default, Serialize, Deserialize, PartialEq, Eq, Clone)]
pub struct LinuxIntelRdt {
    /// The schema for L3 cache id and capacity bitmask (CBM)
    /// Format: "L3:<cache_id0>=<cbm0>;<cache_id1>=<cbm1>;..."
    #[serde(rename = "l3CacheSchema", skip_serializing_if = "Option::is_none")]
    pub l3_cache_schema: Option<String>,
}
