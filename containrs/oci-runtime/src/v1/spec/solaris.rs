use serde::{Deserialize, Serialize};

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
    pub capped_cpu: Option<SolarisCappedCPU>,
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
    pub allowed_addr: Option<String>,
    /// Specifies whether allowedAddress limitation is to be applied to the
    /// VNIC.
    #[serde(
        rename = "configureAllowedAddress",
        skip_serializing_if = "Option::is_none"
    )]
    pub config_allowed_addr: Option<String>,
    /// The value of the optional default router.
    #[serde(rename = "defrouter", skip_serializing_if = "Option::is_none")]
    pub defrouter: Option<String>,
    /// Enable one or more types of link protection.
    #[serde(rename = "linkProtection", skip_serializing_if = "Option::is_none")]
    pub link_protection: Option<String>,
    /// Set the VNIC's macAddress
    #[serde(rename = "macAddress", skip_serializing_if = "Option::is_none")]
    pub mac_address: Option<String>,
}
