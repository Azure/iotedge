use serde::{Deserialize, Serialize};

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
    pub oom_score_adj: Option<isize>,
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
