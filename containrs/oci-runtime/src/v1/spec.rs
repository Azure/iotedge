use std::collections::HashMap;

use serde::{Deserialize, Serialize};

/// Types used by [Spec]`.linux`
pub mod linux;
/// Types used by [Spec]`.process`
pub mod process;
/// Types used by [Spec]`.solaris`
pub mod solaris;
/// Types used by [Spec]`.windows`
pub mod windows;

use linux::*;
use process::*;
use solaris::*;
use windows::*;

/// Spec is the base configuration for the container. (config.json)
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
