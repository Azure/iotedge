// Copyright (c) Microsoft. All rights reserved.

use std::path::Path;

use config::{Config, Environment};
use edgelet_core::{
    Certificates, Connect, Listen, ModuleSpec, Provisioning, RuntimeSettings,
    Settings as BaseSettings, WatchdogSettings,
};
use edgelet_docker::{DockerConfig, DEFAULTS};
use edgelet_utils::YamlFileSource;

use crate::error::Error;

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Settings {
    #[serde(flatten)]
    base: BaseSettings<DockerConfig>,
    namespace: String,
    use_pvc: bool,
    iot_hub_hostname: Option<String>,
    device_id: Option<String>,
    proxy_image: String,
    proxy_config_path: String,
    proxy_config_map_name: String,
    proxy_trust_bundle_path: String,
    proxy_trust_bundle_config_map_name: String,
    image_pull_policy: String,
    service_account_name: String,
    device_hub_selector: String,
}

impl Settings {
    pub fn new(filename: Option<&Path>) -> Result<Self, Error> {
        let filename = filename.map(|filename| {
            filename.to_str().unwrap_or_else(|| {
                panic!(
                    "cannot load config from {} because it is not a utf-8 path",
                    filename.display()
                )
            })
        });
        let mut config = Config::default();
        config.merge(YamlFileSource::String(DEFAULTS))?;
        if let Some(file) = filename {
            config.merge(YamlFileSource::File(file.into()))?;
        }

        config.merge(Environment::with_prefix("iotedge"))?;

        Ok(config.try_into()?)
    }

    pub fn with_device_id(mut self, device_id: &str) -> Self {
        self.device_id = Some(device_id.to_owned());
        self
    }

    pub fn with_iot_hub_hostname(mut self, iot_hub_hostname: &str) -> Self {
        self.iot_hub_hostname = Some(iot_hub_hostname.to_owned());
        self
    }

    pub fn namespace(&self) -> &str {
        &self.namespace
    }

    pub fn use_pvc(&self) -> bool {
        self.use_pvc
    }

    pub fn iot_hub_hostname(&self) -> Option<&str> {
        self.iot_hub_hostname.as_ref().map(String::as_str)
    }

    pub fn device_id(&self) -> Option<&str> {
        self.device_id.as_ref().map(String::as_str)
    }

    pub fn proxy_image(&self) -> &str {
        &self.proxy_image
    }

    pub fn proxy_config_path(&self) -> &str {
        &self.proxy_config_path
    }

    pub fn proxy_config_map_name(&self) -> &str {
        &self.proxy_config_map_name
    }

    pub fn proxy_trust_bundle_path(&self) -> &str {
        &self.proxy_trust_bundle_path
    }

    pub fn proxy_trust_bundle_config_map_name(&self) -> &str {
        &self.proxy_trust_bundle_config_map_name
    }

    pub fn image_pull_policy(&self) -> &str {
        &self.image_pull_policy
    }

    pub fn service_account_name(&self) -> &str {
        &self.service_account_name
    }

    pub fn device_hub_selector(&self) -> &str {
        &self.device_hub_selector
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

    fn connect(&self) -> &Connect {
        self.base.connect()
    }

    fn listen(&self) -> &Listen {
        self.base.listen()
    }

    fn homedir(&self) -> &Path {
        self.base.homedir()
    }

    fn certificates(&self) -> Option<&Certificates> {
        self.base.certificates()
    }

    fn watchdog(&self) -> &WatchdogSettings {
        self.base.watchdog()
    }
}
