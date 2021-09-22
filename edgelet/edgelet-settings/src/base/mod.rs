// Copyright (c) Microsoft. All rights reserved.

pub mod aziot;
pub mod module;
pub mod uri;
pub mod watchdog;

pub trait RuntimeSettings {
    type ModuleConfig: Clone;

    fn hostname(&self) -> &str;

    fn edge_ca_cert(&self) -> Option<&str>;
    fn edge_ca_key(&self) -> Option<&str>;

    fn trust_bundle_cert(&self) -> Option<&str>;
    fn manifest_trust_bundle_cert(&self) -> Option<&str>;
    fn dps_trust_bundle(&self) -> &str;

    fn auto_reprovisioning_mode(&self) -> aziot::AutoReprovisioningMode;

    fn homedir(&self) -> &std::path::Path;

    fn agent(&self) -> &module::Settings<Self::ModuleConfig>;
    fn agent_mut(&mut self) -> &mut module::Settings<Self::ModuleConfig>;

    fn connect(&self) -> &uri::Connect;
    fn listen(&self) -> &uri::Listen;

    fn watchdog(&self) -> &watchdog::Settings;

    fn endpoints(&self) -> &aziot::Endpoints;

    fn allow_elevated_docker_permissions(&self) -> bool;
}

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Settings<ModuleConfig> {
    pub hostname: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub edge_ca_cert: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub edge_ca_key: Option<String>,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub trust_bundle_cert: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub manifest_trust_bundle_cert: Option<String>,

    #[serde(
        default = "aziot_identityd_config::Settings::default_dps_trust_bundle",
        skip_serializing_if = "aziot_identityd_config::Settings::is_default_dps_trust_bundle"
    )]
    pub dps_trust_bundle: String,

    #[serde(default)]
    pub auto_reprovisioning_mode: aziot::AutoReprovisioningMode,

    pub homedir: std::path::PathBuf,

    #[serde(default = "default_allow_elevated_docker_permissions")]
    pub allow_elevated_docker_permissions: bool,

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
        self.edge_ca_cert.as_deref()
    }

    fn edge_ca_key(&self) -> Option<&str> {
        self.edge_ca_key.as_deref()
    }

    fn trust_bundle_cert(&self) -> Option<&str> {
        self.trust_bundle_cert.as_deref()
    }

    fn manifest_trust_bundle_cert(&self) -> Option<&str> {
        self.manifest_trust_bundle_cert.as_deref()
    }

    fn dps_trust_bundle(&self) -> &str {
        &self.dps_trust_bundle
    }

    fn auto_reprovisioning_mode(&self) -> aziot::AutoReprovisioningMode {
        self.auto_reprovisioning_mode
    }

    fn homedir(&self) -> &std::path::Path {
        &self.homedir
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

    fn allow_elevated_docker_permissions(&self) -> bool {
        self.allow_elevated_docker_permissions
    }
}
