// Copyright (c) Microsoft. All rights reserved.

use std::path::Path;

use crate::error::Error;
use crate::ErrorKind;
use config::{Config, Environment};
use docker::models::AuthConfig;
use edgelet_core::{
    Certificates, Connect, Listen, ModuleSpec, Provisioning, RuntimeSettings,
    Settings as BaseSettings, WatchdogSettings,
};
use edgelet_docker::{DockerConfig, DEFAULTS};
use edgelet_utils::YamlFileSource;
use failure::ResultExt;
use k8s_openapi::api::core::v1::ResourceRequirements;

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Settings {
    #[serde(flatten)]
    base: BaseSettings<DockerConfig>,
    namespace: String,
    iot_hub_hostname: Option<String>,
    device_id: Option<String>,
    device_hub_selector: String,
    proxy: ProxySettings,
    config_path: String,
    config_map_name: String,
    config_map_volume: String,
    resources: Option<ResourceRequirements>,
    #[serde(default = "Settings::default_nodes_rbac")]
    has_nodes_rbac: bool,
}

impl Settings {
    pub fn new(filename: &Path) -> Result<Self, Error> {
        let mut config = Config::default();
        config
            .merge(YamlFileSource::String(DEFAULTS))
            .context(ErrorKind::Config)?;

        config
            .merge(YamlFileSource::File(filename.into()))
            .context(ErrorKind::Config)?;

        config
            .merge(Environment::with_prefix("iotedge"))
            .context(ErrorKind::Config)?;

        let settings = config.try_into().context(ErrorKind::Config)?;

        Ok(settings)
    }

    pub fn with_device_id(mut self, device_id: &str) -> Self {
        self.device_id = Some(device_id.to_owned());
        self
    }

    pub fn with_iot_hub_hostname(mut self, iot_hub_hostname: &str) -> Self {
        self.iot_hub_hostname = Some(iot_hub_hostname.to_owned());
        self
    }

    pub fn with_nodes_rbac(mut self, has_nodes_rbac: bool) -> Self {
        self.has_nodes_rbac = has_nodes_rbac;
        self
    }

    pub fn namespace(&self) -> &str {
        &self.namespace
    }

    pub fn iot_hub_hostname(&self) -> Option<&str> {
        self.iot_hub_hostname.as_deref()
    }

    pub fn proxy(&self) -> &ProxySettings {
        &self.proxy
    }

    pub fn device_id(&self) -> Option<&str> {
        self.device_id.as_deref()
    }

    pub fn device_hub_selector(&self) -> &str {
        &self.device_hub_selector
    }
    pub fn config_path(&self) -> &str {
        &self.config_path
    }

    pub fn config_map_name(&self) -> &str {
        &self.config_map_name
    }

    pub fn config_map_volume(&self) -> &str {
        &self.config_map_volume
    }

    pub fn resources(&self) -> Option<&ResourceRequirements> {
        self.resources.as_ref()
    }

    pub fn has_nodes_rbac(&self) -> bool {
        self.has_nodes_rbac
    }

    fn default_nodes_rbac() -> bool {
        true
    }
}

impl RuntimeSettings for Settings {
    type Config = DockerConfig;

    fn provisioning(&self) -> &Provisioning {
        self.base.provisioning()
    }

    fn agent(&self) -> &ModuleSpec<DockerConfig> {
        self.base.agent()
    }

    fn agent_mut(&mut self) -> &mut ModuleSpec<DockerConfig> {
        self.base.agent_mut()
    }

    fn hostname(&self) -> &str {
        self.base.hostname()
    }

    fn allow_elevated_docker_permissions(&self) -> bool {
        true
    }

    fn connect(&self) -> &Connect {
        self.base.connect()
    }

    fn listen(&self) -> &Listen {
        self.base.listen()
    }

    fn homedir(&self) -> &Path {
        self.base.homedir()
    }

    fn certificates(&self) -> &Certificates {
        self.base.certificates()
    }

    fn watchdog(&self) -> &WatchdogSettings {
        self.base.watchdog()
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct ProxySettings {
    auth: Option<AuthConfig>,
    image: String,
    image_pull_policy: String,
    config_path: String,
    config_map_name: String,
    trust_bundle_path: String,
    trust_bundle_config_map_name: String,
    resources: Option<ResourceRequirements>,
}

impl ProxySettings {
    pub fn auth(&self) -> Option<&AuthConfig> {
        self.auth.as_ref()
    }

    pub fn image(&self) -> &str {
        &self.image
    }

    pub fn config_path(&self) -> &str {
        &self.config_path
    }

    pub fn config_map_name(&self) -> &str {
        &self.config_map_name
    }

    pub fn trust_bundle_path(&self) -> &str {
        &self.trust_bundle_path
    }

    pub fn trust_bundle_config_map_name(&self) -> &str {
        &self.trust_bundle_config_map_name
    }

    pub fn image_pull_policy(&self) -> &str {
        &self.image_pull_policy
    }

    pub fn resources(&self) -> Option<&ResourceRequirements> {
        self.resources.as_ref()
    }
}
