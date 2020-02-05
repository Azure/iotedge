use serde::{Deserialize, Serialize};

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
    /*

    XXX: work around serde_json::Value not supportting #[derive(Eq)]
      It makes sense why it doesn't (floats don't implement Eq), but Spec really
      aughta be able to implement Eq...

    /// CredentialSpec contains a JSON object describing a group Managed Service
    /// Account (gMSA) specification.
    #[serde(rename = "credentialSpec", skip_serializing_if = "Option::is_none")]
    pub credential_spec: Option<serde_json::Value>,

    */
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
    pub hyperv: Option<WindowsHyperV>,
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
    pub allow_unqualified_dns_query: Option<bool>,
    /// Comma separated list of DNS suffixes to use for name resolution.
    #[serde(rename = "DNSSearchList", skip_serializing_if = "Option::is_none")]
    pub dns_search_list: Option<Vec<String>>,
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
    pub utility_vm_path: Option<String>,
}
