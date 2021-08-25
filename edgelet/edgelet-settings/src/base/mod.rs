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
    hostname: String,

    #[serde(skip_serializing_if = "Option::is_none")]
    edge_ca_cert: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    edge_ca_key: Option<String>,

    #[serde(skip_serializing_if = "Option::is_none")]
    trust_bundle_cert: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    manifest_trust_bundle_cert: Option<String>,

    #[serde(default)]
    auto_reprovisioning_mode: aziot::AutoReprovisioningMode,

    homedir: std::path::PathBuf,

    agent: module::Settings<ModuleConfig>,
    connect: uri::Connect,
    listen: uri::Listen,

    #[serde(default)]
    watchdog: watchdog::Settings,

    /// Map of service names to endpoint URIs.
    ///
    /// Only configurable in debug builds for the sake of tests.
    #[serde(default, skip_serializing)]
    #[cfg_attr(not(debug_assertions), serde(skip_deserializing))]
    endpoints: aziot::Endpoints,

    allow_elevated_docker_permissions: bool,
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
