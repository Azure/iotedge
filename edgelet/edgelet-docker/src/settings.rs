// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::path::Path;

use anyhow::Context;
use docker::models::{ContainerCreateBodyNetworkingConfig, EndpointSettings, HostConfig};
use edgelet_core::{
    settings::AutoReprovisioningMode, Connect, Endpoints, Listen, MobyNetwork, ModuleSpec,
    RuntimeSettings, Settings as BaseSettings, UrlExt, WatchdogSettings,
};

use url::Url;

use crate::config::DockerConfig;
use crate::error::Error;

/// This is the key for the docker network Id.
const EDGE_NETWORKID_KEY: &str = "NetworkId";

const UNIX_SCHEME: &str = "unix";

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct MobyRuntime {
    pub uri: Url,
    pub network: MobyNetwork,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub content_trust: Option<ContentTrust>,
}

impl MobyRuntime {
    pub fn uri(&self) -> &Url {
        &self.uri
    }

    pub fn network(&self) -> &MobyNetwork {
        &self.network
    }

    pub fn content_trust(&self) -> Option<&ContentTrust> {
        self.content_trust.as_ref()
    }
}

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct ContentTrust {
    pub ca_certs: Option<BTreeMap<String, String>>,
}

impl ContentTrust {
    pub fn ca_certs(&self) -> Option<&BTreeMap<String, String>> {
        self.ca_certs.as_ref()
    }
}

/// This struct is the same as the Settings type from the `edgelet_core` crate
/// except that it also sets up the volume mounting of workload & management
/// UDS sockets for the edge agent container and injects the docker network
/// name both as an environment variable and as an endpoint setting in the
/// docker create options for edge agent.
#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Settings {
    #[serde(flatten)]
    pub base: BaseSettings<DockerConfig>,
    pub moby_runtime: MobyRuntime,
}

pub const CONFIG_FILE_DEFAULT: &str = "/etc/aziot/edged/config.toml";

impl Settings {
    /// Load the aziot-edged configuration.
    ///
    /// Configuration is made up of /etc/aziot/edged/config.toml (overridden by the `AZIOT_EDGED_CONFIG` env var)
    /// and any files in the /etc/aziot/edged/config.d directory (overridden by the `AZIOT_EDGED_CONFIG_DIR` env var).
    pub fn new() -> anyhow::Result<Self> {
        const CONFIG_ENV_VAR: &str = "AZIOT_EDGED_CONFIG";
        const CONFIG_DIRECTORY_ENV_VAR: &str = "AZIOT_EDGED_CONFIG_DIR";
        const CONFIG_DIRECTORY_DEFAULT: &str = "/etc/aziot/edged/config.d";

        let config_path: std::path::PathBuf =
            std::env::var_os(CONFIG_ENV_VAR).map_or_else(|| CONFIG_FILE_DEFAULT.into(), Into::into);

        let config_directory_path: std::path::PathBuf = std::env::var_os(CONFIG_DIRECTORY_ENV_VAR)
            .map_or_else(|| CONFIG_DIRECTORY_DEFAULT.into(), Into::into);

        let mut settings: Settings =
            config_common::read_config(&config_path, Some(&config_directory_path))
                .context(LoadSettingsError)?;

        init_agent_spec(&mut settings)?;

        Ok(settings)
    }

    pub fn moby_runtime(&self) -> &MobyRuntime {
        &self.moby_runtime
    }
}

impl RuntimeSettings for Settings {
    type Config = DockerConfig;

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

    fn watchdog(&self) -> &WatchdogSettings {
        self.base.watchdog()
    }

    fn endpoints(&self) -> &Endpoints {
        self.base.endpoints()
    }

    fn additional_info(&self) -> &BTreeMap<String, String> {
        self.base.additional_info()
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

    fn auto_reprovisioning_mode(&self) -> &AutoReprovisioningMode {
        self.base.auto_reprovisioning_mode()
    }
}

fn init_agent_spec(settings: &mut Settings) -> anyhow::Result<()> {
    // setup vol mounts for workload/management sockets
    agent_vol_mount(settings)?;

    // setup environment variables that are moby/docker specific
    agent_env(settings);

    // setup moby/docker specific networking config
    agent_networking(settings)?;

    agent_labels(settings)?;

    Ok(())
}

fn agent_vol_mount(settings: &mut Settings) -> anyhow::Result<()> {
    let create_options = settings.agent().config().clone_create_options().context(LoadSettingsError)?;
    let host_config = create_options
        .host_config()
        .cloned()
        .unwrap_or_else(HostConfig::new);
    let mut binds = host_config.binds().map_or_else(Vec::new, ToOwned::to_owned);

    let home_dir = settings
        .homedir()
        .to_str()
        .context(Error::InvalidHomeDirPath)
        .context(LoadSettingsError)?;

    let workload_listen_uri = &Listen::workload_uri(home_dir, settings.agent().name())
        .context(Error::InvalidHomeDirPath)
        .context(LoadSettingsError)?;

    let workload_connect_uri = settings.connect().workload_uri();

    let management_listen_uri = settings.connect().management_uri();

    let management_connect_uri = settings.connect().management_uri();

    // if the url is a domain socket URL then vol mount it into the container
    for (listen_uri, connect_uri) in &[
        (management_listen_uri, management_connect_uri),
        (workload_listen_uri, workload_connect_uri),
    ] {
        if connect_uri.scheme() == UNIX_SCHEME {
            let source_path = get_path_from_uri(listen_uri).context(LoadSettingsError)?;
            let target_path = get_path_from_uri(connect_uri).context(LoadSettingsError)?;

            let bind = format!("{}:{}", &source_path, &target_path);
            if !binds.contains(&bind) {
                binds.push(bind);
            }
        }
    }

    if !binds.is_empty() {
        let host_config = host_config.with_binds(binds);
        let create_options = create_options.with_host_config(host_config);

        settings
            .agent_mut()
            .config_mut()
            .set_create_options(create_options);
    }

    Ok(())
}

fn get_path_from_uri(uri: &Url) -> anyhow::Result<String> {
    let path = uri
        .to_uds_file_path()
        .context(Error::InvalidSocketUri(uri.to_string()))?;
    Ok(path
        .to_str()
        .with_context(|| Error::InvalidSocketUri(uri.to_string()))?
        .to_string())
}

fn agent_env(settings: &mut Settings) {
    let network_id = settings.moby_runtime().network().name().to_string();
    settings
        .agent_mut()
        .env_mut()
        .insert(EDGE_NETWORKID_KEY.to_string(), network_id);
}

fn agent_networking(settings: &mut Settings) -> anyhow::Result<()> {
    let network_id = settings.moby_runtime().network().name().to_string();

    let create_options = settings.agent().config().clone_create_options().context(LoadSettingsError)?;

    let mut network_config = create_options
        .networking_config()
        .cloned()
        .unwrap_or_else(ContainerCreateBodyNetworkingConfig::new);

    let mut endpoints_config = network_config
        .endpoints_config()
        .cloned()
        .unwrap_or_else(BTreeMap::new);

    if !endpoints_config.contains_key(network_id.as_str()) {
        endpoints_config.insert(network_id, EndpointSettings::new());
        network_config = network_config.with_endpoints_config(endpoints_config);
        let create_options = create_options.with_networking_config(network_config);

        settings
            .agent_mut()
            .config_mut()
            .set_create_options(create_options);
    }

    Ok(())
}

fn agent_labels(settings: &mut Settings) -> anyhow::Result<()> {
    let create_options = settings.agent().config().clone_create_options()?;

    let mut labels = create_options
        .labels()
        .cloned()
        .unwrap_or_else(BTreeMap::new);

    // IoT Edge reserves the label prefix "net.azure-devices.edge" for its own purposes
    // so we'll simply overwrite any matching labels created by the user.
    labels.insert(
        "net.azure-devices.edge.create-options".to_string(),
        "{}".to_string(),
    );
    labels.insert("net.azure-devices.edge.env".to_string(), "{}".to_string());

    let create_options = create_options.with_labels(labels);

    settings
        .agent_mut()
        .config_mut()
        .set_create_options(create_options);

    Ok(())
}

#[derive(Debug, thiserror::Error)]
#[error("Could not load settings")]
pub struct LoadSettingsError;

#[cfg(test)]
mod tests {
    #[cfg(target_os = "linux")]
    use super::ContentTrust;
    use super::{MobyNetwork, MobyRuntime, RuntimeSettings, Settings, Url};
    use edgelet_core::{IpamConfig, DEFAULT_NETWORKID};
    use std::cmp::Ordering;

    #[cfg(unix)]
    static GOOD_SETTINGS: &str = "test/linux/sample_settings.toml";
    #[cfg(unix)]
    static BAD_SETTINGS: &str = "test/linux/bad_sample_settings.toml";
    #[cfg(unix)]
    static GOOD_SETTINGS_CASE_SENSITIVE: &str = "test/linux/case_sensitive.toml";
    #[cfg(unix)]
    static GOOD_SETTINGS_NETWORK: &str = "test/linux/sample_settings.network.toml";
    #[cfg(unix)]
    static GOOD_SETTINGS_CONTENT_TRUST: &str = "test/linux/sample_settings_content_trust.toml";
    #[cfg(unix)]
    static BAD_SETTINGS_CONTENT_TRUST: &str = "test/linux/bad_settings_content_trust.toml";

    lazy_static::lazy_static! {
        static ref ENV_LOCK: std::sync::Mutex<()> = Default::default();
    }

    #[test]
    fn network_default() {
        let moby1 = MobyRuntime {
            uri: Url::parse("http://test").unwrap(),
            network: MobyNetwork::Name("".to_string()),
            content_trust: None,
        };
        assert_eq!(DEFAULT_NETWORKID, moby1.network().name());

        let moby2 = MobyRuntime {
            uri: Url::parse("http://test").unwrap(),
            network: MobyNetwork::Name("some-network".to_string()),
            content_trust: None,
        };
        assert_eq!("some-network", moby2.network().name());
    }

    #[test]
    fn network_get_settings() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS_NETWORK);

        let settings = Settings::new().unwrap();
        let moby_runtime = settings.moby_runtime();
        assert_eq!(
            moby_runtime.uri().to_owned().into_string(),
            "http://localhost:2375/".to_string()
        );

        let network = moby_runtime.network();
        assert_eq!(network.name(), "azure-iot-edge");
        match network {
            MobyNetwork::Network(moby_network) => {
                assert_eq!(moby_network.ipv6().unwrap(), true);
                let ipam_spec = moby_network.ipam().expect("Expected IPAM specification.");
                let ipam_config = ipam_spec.config().expect("Expected IPAM configuration.");
                let ipam_1 = IpamConfig::default()
                    .with_gateway("172.18.0.1".to_string())
                    .with_ip_range("172.18.0.0/16".to_string())
                    .with_subnet("172.18.0.0/16".to_string());
                let ipam_2 = IpamConfig::default()
                    .with_gateway("2001:4898:e0:3b1:1::1".to_string())
                    .with_ip_range("2001:4898:e0:3b1:1::/80".to_string())
                    .with_subnet("2001:4898:e0:3b1:1::/80".to_string());
                let expected_ipam_config: Vec<IpamConfig> = vec![ipam_1, ipam_2];

                ipam_config.iter().for_each(|ipam_config| {
                    assert!(expected_ipam_config.contains(ipam_config));
                });
            }
            MobyNetwork::Name(_name) => panic!("Unexpected network configuration."),
        };
    }

    #[test]
    fn no_file_gets_error() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", "garbage");
        let settings = Settings::new();
        assert!(settings.is_err());
    }

    #[test]
    fn bad_file_gets_error() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", BAD_SETTINGS);
        let settings = Settings::new();
        assert!(settings.is_err());
    }

    #[test]
    fn case_of_names_of_keys_is_preserved() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS_CASE_SENSITIVE);
        let settings = Settings::new().unwrap();

        let env = settings.agent().env();
        assert_eq!(env.get("AbC").map(AsRef::as_ref), Some("VAluE1"));
        assert_eq!(env.get("DeF").map(AsRef::as_ref), Some("VAluE2"));

        let create_options = settings.agent().config().create_options();
        assert_eq!(create_options.hostname(), Some("VAluE3"));
    }

    #[test]
    fn watchdog_settings_are_read() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS);
        let settings = Settings::new().unwrap();
        let watchdog_settings = settings.watchdog();
        assert_eq!(watchdog_settings.max_retries().compare(3), Ordering::Equal);
    }

    #[test]
    fn tls_settings_are_none_by_default() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS);
        let settings = Settings::new().unwrap();
        assert_eq!(
            settings.listen().min_tls_version(),
            edgelet_core::Protocol::Tls10
        );
    }

    #[test]
    fn networking_config_is_set() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS);
        let settings = Settings::new().unwrap();
        let create_options = settings.agent().config().create_options();
        assert!(create_options
            .networking_config()
            .unwrap()
            .endpoints_config()
            .unwrap()
            .contains_key("azure-iot-edge"));
    }

    #[test]
    fn agent_labels_are_set() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS);
        let settings = Settings::new().unwrap();
        let create_options = settings.agent().config().create_options();
        let labels = create_options.labels().unwrap();
        assert_eq!(
            labels.get("net.azure-devices.edge.create-options"),
            Some(&"{}".to_string())
        );
        assert_eq!(
            labels.get("net.azure-devices.edge.env"),
            Some(&"{}".to_string())
        );
    }

    #[cfg(unix)]
    #[test]
    fn content_trust_env_are_set_properly() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS_CONTENT_TRUST);
        let settings = Settings::new().unwrap();
        if let Some(content_trust_map) = settings
            .moby_runtime()
            .content_trust()
            .and_then(ContentTrust::ca_certs)
        {
            assert_eq!(
                content_trust_map
                    .get("contoso1.azurcr.io")
                    .map(AsRef::as_ref),
                Some("content-trust-contoso1.azurecr.io")
            );
            assert_eq!(
                content_trust_map
                    .get("contoso2.azurcr.io")
                    .map(AsRef::as_ref),
                Some("content-trust-contoso2.azurecr.io")
            );
        } else {
            panic!();
        }
    }

    #[cfg(unix)]
    #[test]
    fn content_trust_env_are_not_set_properly() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", BAD_SETTINGS_CONTENT_TRUST);
        let settings = Settings::new();
        assert!(settings.is_err());
    }
}
