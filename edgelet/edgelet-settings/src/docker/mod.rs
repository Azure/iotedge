// Copyright (c) Microsoft. All rights reserved.

mod init;

pub mod config;
pub mod network;
pub mod runtime;

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Settings {
    #[serde(flatten)]
    base: crate::base::Settings<config::DockerConfig>,

    moby_runtime: runtime::MobyRuntime,
}

impl Settings {
    /// Load the aziot-edged configuration.
    ///
    /// Configuration is made up of /etc/aziot/edged/config.toml (overridden by the `AZIOT_EDGED_CONFIG` env var)
    /// and any files in the /etc/aziot/edged/config.d directory (overridden by the `AZIOT_EDGED_CONFIG_DIR` env var).
    pub fn new() -> Result<Self, Box<dyn std::error::Error>> {
        let config_path = std::env::var("AZIOT_EDGED_CONFIG")
            .unwrap_or("/etc/aziot/edged/config.toml".to_string());
        let config_path = std::path::Path::new(&config_path);

        let config_directory_path = std::env::var("AZIOT_EDGED_CONFIG_DIR")
            .unwrap_or("/etc/aziot/edged/config.d".to_string());
        let config_directory_path = std::path::Path::new(&config_directory_path);

        let mut settings: Settings =
            config_common::read_config(&config_path, Some(&config_directory_path))?;

        init::agent_spec(&mut settings);

        Ok(settings)
    }

    pub fn moby_runtime(&self) -> &runtime::MobyRuntime {
        &self.moby_runtime
    }
}

impl crate::RuntimeSettings for Settings {
    type ModuleConfig = config::DockerConfig;

    fn hostname(&self) -> &str {
        self.base.hostname()
    }

    fn edge_ca_cert(&self) -> Option<&str> {
        self.base.edge_ca_cert()
    }

    fn edge_ca_key(&self) -> Option<&str> {
        self.base.edge_ca_key()
    }

    fn trust_bundle_cert(&self) -> Option<&str> {
        self.base.trust_bundle_cert()
    }

    fn manifest_trust_bundle_cert(&self) -> Option<&str> {
        self.base.manifest_trust_bundle_cert()
    }

    fn auto_reprovisioning_mode(&self) -> crate::aziot::AutoReprovisioningMode {
        self.base.auto_reprovisioning_mode()
    }

    fn homedir(&self) -> &std::path::Path {
        self.base.homedir()
    }

    fn agent(&self) -> &crate::module::Settings<Self::ModuleConfig> {
        self.base.agent()
    }

    fn agent_mut(&mut self) -> &mut crate::module::Settings<Self::ModuleConfig> {
        self.base.agent_mut()
    }

    fn connect(&self) -> &crate::uri::Connect {
        self.base.connect()
    }

    fn listen(&self) -> &crate::uri::Listen {
        self.base.listen()
    }

    fn watchdog(&self) -> &crate::watchdog::Settings {
        self.base.watchdog()
    }

    fn endpoints(&self) -> &crate::aziot::Endpoints {
        self.base.endpoints()
    }
}
