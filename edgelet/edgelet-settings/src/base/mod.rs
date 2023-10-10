// Copyright (c) Microsoft. All rights reserved.

pub mod aziot;
pub mod image;
pub mod module;
pub mod uri;
pub mod watchdog;

pub trait RuntimeSettings {
    type ModuleConfig: Clone;

    fn hostname(&self) -> &str;

    fn edge_ca_cert(&self) -> Option<&str>;
    fn edge_ca_key(&self) -> Option<&str>;
    fn edge_ca_auto_renew(&self) -> &Option<cert_renewal::AutoRenewConfig>;
    fn edge_ca_subject(&self) -> &Option<aziot_certd_config::CertSubject>;

    fn trust_bundle_cert(&self) -> Option<&str>;
    fn manifest_trust_bundle_cert(&self) -> Option<&str>;

    fn auto_reprovisioning_mode(&self) -> aziot::AutoReprovisioningMode;

    fn homedir(&self) -> &std::path::Path;

    fn allow_elevated_docker_permissions(&self) -> bool;

    fn iotedge_max_requests(&self) -> &IotedgeMaxRequests;

    fn agent(&self) -> &module::Settings<Self::ModuleConfig>;
    fn agent_mut(&mut self) -> &mut module::Settings<Self::ModuleConfig>;

    fn connect(&self) -> &uri::Connect;
    fn listen(&self) -> &uri::Listen;

    fn watchdog(&self) -> &watchdog::Settings;

    fn endpoints(&self) -> &aziot::Endpoints;

    fn additional_info(&self) -> &std::collections::BTreeMap<String, String>;

    fn image_garbage_collection(&self) -> &image::ImagePruneSettings;
}

#[derive(Clone, Debug, Default, Eq, PartialEq, serde::Deserialize, serde::Serialize)]
pub struct EdgeCa {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub cert: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub key: Option<String>,

    // This enum has one value variant and one table variant. It must be placed
    // after all values and before all tables.
    #[serde(flatten, skip_serializing_if = "Option::is_none")]
    pub subject: Option<aziot_certd_config::CertSubject>,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub auto_renew: Option<cert_renewal::AutoRenewConfig>,
}

impl EdgeCa {
    pub fn is_default(&self) -> bool {
        self == &EdgeCa::default()
    }
}

#[derive(Clone, Debug, Eq, PartialEq, serde::Deserialize, serde::Serialize)]
pub struct IotedgeMaxRequests {
    pub management: usize,
    pub workload: usize,
}

impl Default for IotedgeMaxRequests {
    fn default() -> IotedgeMaxRequests {
        IotedgeMaxRequests {
            // Allow 50 concurrent requests on the management socket, as that is the
            // maximum number of modules allowed by IoT Hub.
            management: 50,
            workload: http_common::Incoming::default_max_requests(),
        }
    }
}

impl IotedgeMaxRequests {
    pub fn is_default(&self) -> bool {
        self == &IotedgeMaxRequests::default()
    }
}

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Settings<ModuleConfig> {
    pub hostname: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub trust_bundle_cert: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub manifest_trust_bundle_cert: Option<String>,

    #[serde(default)]
    pub auto_reprovisioning_mode: aziot::AutoReprovisioningMode,

    pub homedir: std::path::PathBuf,

    #[serde(default = "default_allow_elevated_docker_permissions")]
    pub allow_elevated_docker_permissions: bool,

    #[serde(default, skip_serializing_if = "IotedgeMaxRequests::is_default")]
    pub iotedge_max_requests: IotedgeMaxRequests,

    #[serde(default, skip_serializing_if = "EdgeCa::is_default")]
    pub edge_ca: EdgeCa,

    pub agent: module::Settings<ModuleConfig>,
    pub connect: uri::Connect,
    pub listen: uri::Listen,

    #[serde(default)]
    pub watchdog: watchdog::Settings,

    /// Map of service names to endpoint URIs.
    ///
    /// Only configurable in debug builds for the sake of tests.
    #[serde(default, skip_serializing)]
    #[cfg_attr(not(debug_assertions), serde(skip_deserializing))]
    pub endpoints: aziot::Endpoints,

    /// Additional system information
    #[serde(default, skip_serializing_if = "std::collections::BTreeMap::is_empty")]
    pub additional_info: std::collections::BTreeMap<String, String>,

    #[serde(default, skip_serializing_if = "image::ImagePruneSettings::is_default")]
    pub image_garbage_collection: image::ImagePruneSettings,
}

pub(crate) fn default_allow_elevated_docker_permissions() -> bool {
    // For now, we will allow elevated docker permissions by default. This will change in a future version.
    true
}

impl<T: Clone> RuntimeSettings for Settings<T> {
    type ModuleConfig = T;

    fn hostname(&self) -> &str {
        &self.hostname
    }

    fn edge_ca_cert(&self) -> Option<&str> {
        self.edge_ca.cert.as_deref()
    }

    fn edge_ca_key(&self) -> Option<&str> {
        self.edge_ca.key.as_deref()
    }

    fn edge_ca_auto_renew(&self) -> &Option<cert_renewal::AutoRenewConfig> {
        &self.edge_ca.auto_renew
    }

    fn edge_ca_subject(&self) -> &Option<aziot_certd_config::CertSubject> {
        &self.edge_ca.subject
    }

    fn trust_bundle_cert(&self) -> Option<&str> {
        self.trust_bundle_cert.as_deref()
    }

    fn manifest_trust_bundle_cert(&self) -> Option<&str> {
        self.manifest_trust_bundle_cert.as_deref()
    }

    fn auto_reprovisioning_mode(&self) -> aziot::AutoReprovisioningMode {
        self.auto_reprovisioning_mode
    }
    fn iotedge_max_requests(&self) -> &IotedgeMaxRequests {
        &self.iotedge_max_requests
    }

    fn homedir(&self) -> &std::path::Path {
        &self.homedir
    }

    fn allow_elevated_docker_permissions(&self) -> bool {
        self.allow_elevated_docker_permissions
    }

    fn agent(&self) -> &module::Settings<Self::ModuleConfig> {
        &self.agent
    }

    fn agent_mut(&mut self) -> &mut module::Settings<Self::ModuleConfig> {
        &mut self.agent
    }

    fn connect(&self) -> &uri::Connect {
        &self.connect
    }

    fn listen(&self) -> &uri::Listen {
        &self.listen
    }

    fn watchdog(&self) -> &watchdog::Settings {
        &self.watchdog
    }

    fn endpoints(&self) -> &aziot::Endpoints {
        &self.endpoints
    }

    fn additional_info(&self) -> &std::collections::BTreeMap<String, String> {
        &self.additional_info
    }

    fn image_garbage_collection(&self) -> &image::ImagePruneSettings {
        &self.image_garbage_collection
    }
}
