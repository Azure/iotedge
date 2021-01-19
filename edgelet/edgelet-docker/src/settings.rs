// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::path::Path;
use std::path::PathBuf;

use config::{Config, Environment};
use docker::models::{ContainerCreateBodyNetworkingConfig, EndpointSettings, HostConfig};
use edgelet_core::{
    Connect, Endpoints, Listen, MobyNetwork, ModuleSpec, RuntimeSettings, Settings as BaseSettings,
    UrlExt, WatchdogSettings,
};
use edgelet_utils::YamlFileSource;
use failure::{Context, Fail, ResultExt};

use url::Url;

use crate::config::DockerConfig;
use crate::error::{Error, ErrorKind};

#[cfg(unix)]
pub const DEFAULTS: &str = include_str!("../config/unix/default.yaml");

/// This is the key for the docker network Id.
const EDGE_NETWORKID_KEY: &str = "NetworkId";

const UNIX_SCHEME: &str = "unix";
const UPSTREAM_PARENT_KEYWORD: &str = "$upstream";

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
    pub ca_certs: Option<BTreeMap<String, PathBuf>>,
}

impl ContentTrust {
    pub fn ca_certs(&self) -> Option<&BTreeMap<String, PathBuf>> {
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

impl Settings {
    pub fn new(filename: &Path) -> Result<Self, LoadSettingsError> {
        let mut config = Config::default();
        config.merge(YamlFileSource::String(DEFAULTS.into()))?;
        config.merge(YamlFileSource::File(filename.into()))?;
        config.merge(Environment::with_prefix("iotedge"))?;

        let mut settings: Self = config.try_into()?;

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

    fn parent_hostname(&self) -> Option<&str> {
        self.base.parent_hostname()
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

    fn edge_ca_cert(&self) -> Option<&str> {
        self.base.edge_ca_cert()
    }

    fn edge_ca_key(&self) -> Option<&str> {
        self.base.edge_ca_key()
    }

    fn trust_bundle_cert(&self) -> Option<&str> {
        self.base.trust_bundle_cert()
    }
}

fn init_agent_spec(settings: &mut Settings) -> Result<(), LoadSettingsError> {
    // setup vol mounts for workload/management sockets
    agent_vol_mount(settings)?;

    // setup environment variables that are moby/docker specific
    agent_env(settings);

    // setup moby/docker specific networking config
    agent_networking(settings)?;

    agent_labels(settings)?;

    // In nested scenario, Agent image can be pulled from its parent.
    // It is possible to specify the parent address using the keyword $upstream
    agent_image_resolve(settings)?;

    Ok(())
}

fn agent_image_resolve(settings: &mut Settings) -> Result<(), LoadSettingsError> {
    let image = settings.agent().config().image().to_string();

    if let Some(parent_hostname) = settings.parent_hostname() {
        if image.starts_with(UPSTREAM_PARENT_KEYWORD) {
            let image_nested = format!(
                "{}{}",
                parent_hostname,
                &image[UPSTREAM_PARENT_KEYWORD.len()..]
            );
            let config = settings.agent().config().clone().with_image(image_nested);
            settings.agent_mut().set_config(config);
        }
    }

    Ok(())
}

fn agent_vol_mount(settings: &mut Settings) -> Result<(), LoadSettingsError> {
    let create_options = settings.agent().config().clone_create_options()?;
    let host_config = create_options
        .host_config()
        .cloned()
        .unwrap_or_else(HostConfig::new);
    let mut binds = host_config.binds().map_or_else(Vec::new, ToOwned::to_owned);

    // if the url is a domain socket URL then vol mount it into the container
    for uri in &[
        settings.connect().management_uri(),
        settings.connect().workload_uri(),
    ] {
        if uri.scheme() == UNIX_SCHEME {
            let path = uri
                .to_uds_file_path()
                .context(ErrorKind::InvalidSocketUri(uri.to_string()))?;
            let path = path
                .to_str()
                .ok_or_else(|| ErrorKind::InvalidSocketUri(uri.to_string()))?
                .to_string();
            let bind = format!("{}:{}", &path, &path);
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

fn agent_env(settings: &mut Settings) {
    let network_id = settings.moby_runtime().network().name().to_string();
    settings
        .agent_mut()
        .env_mut()
        .insert(EDGE_NETWORKID_KEY.to_string(), network_id);
}

fn agent_networking(settings: &mut Settings) -> Result<(), LoadSettingsError> {
    let network_id = settings.moby_runtime().network().name().to_string();

    let create_options = settings.agent().config().clone_create_options()?;

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

fn agent_labels(settings: &mut Settings) -> Result<(), LoadSettingsError> {
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

#[derive(Debug, Fail)]
#[fail(display = "Could not load settings")]
pub struct LoadSettingsError(#[cause] Context<Box<dyn std::fmt::Display + Send + Sync>>);

impl From<std::io::Error> for LoadSettingsError {
    fn from(err: std::io::Error) -> Self {
        LoadSettingsError(Context::new(Box::new(err)))
    }
}

impl From<config::ConfigError> for LoadSettingsError {
    fn from(err: config::ConfigError) -> Self {
        LoadSettingsError(Context::new(Box::new(err)))
    }
}

impl From<serde_json::Error> for LoadSettingsError {
    fn from(err: serde_json::Error) -> Self {
        LoadSettingsError(Context::new(Box::new(err)))
    }
}

impl From<Error> for LoadSettingsError {
    fn from(err: Error) -> Self {
        LoadSettingsError(Context::new(Box::new(err)))
    }
}

impl From<Context<ErrorKind>> for LoadSettingsError {
    fn from(inner: Context<ErrorKind>) -> Self {
        From::from(Error::from(inner))
    }
}

impl From<ErrorKind> for LoadSettingsError {
    fn from(kind: ErrorKind) -> Self {
        From::from(Error::from(kind))
    }
}

#[cfg(test)]
mod tests {
    #[cfg(target_os = "linux")]
    use super::ContentTrust;
    use super::{MobyNetwork, MobyRuntime, Path, RuntimeSettings, Settings, Url};
    use crate::settings::agent_image_resolve;
    use edgelet_core::{IpamConfig, DEFAULT_NETWORKID};
    use std::cmp::Ordering;

    #[cfg(unix)]
    static GOOD_SETTINGS: &str = "test/linux/sample_settings.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS: &str = "test/linux/bad_sample_settings.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_CASE_SENSITIVE: &str = "test/linux/case_sensitive.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_NETWORK: &str = "test/linux/sample_settings.network.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_CONTENT_TRUST: &str = "test/linux/sample_settings_content_trust.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_CONTENT_TRUST: &str = "test/linux/bad_settings_content_trust.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_IMAGE_RESOLVE: &str = "test/linux/sample_settings_image_resolve.yaml";

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
        let settings = Settings::new(Path::new(GOOD_SETTINGS_NETWORK));
        assert!(settings.is_ok());
        let s = settings.unwrap();
        let moby_runtime = s.moby_runtime();
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
        let settings = Settings::new(Path::new("garbage"));
        assert!(settings.is_err());
    }

    #[test]
    fn bad_file_gets_error() {
        let settings = Settings::new(Path::new(BAD_SETTINGS));
        assert!(settings.is_err());
    }

    #[test]
    fn case_of_names_of_keys_is_preserved() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_CASE_SENSITIVE)).unwrap();

        let env = settings.agent().env();
        assert_eq!(env.get("AbC").map(AsRef::as_ref), Some("VAluE1"));
        assert_eq!(env.get("DeF").map(AsRef::as_ref), Some("VAluE2"));

        let create_options = settings.agent().config().create_options();
        assert_eq!(create_options.hostname(), Some("VAluE3"));
    }

    #[test]
    fn watchdog_settings_are_read() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        let watchdog_settings = s.watchdog();
        assert_eq!(watchdog_settings.max_retries().compare(3), Ordering::Equal);
    }

    #[test]
    fn tls_settings_are_none_by_default() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
        assert_eq!(
            settings.listen().min_tls_version(),
            edgelet_core::Protocol::Tls10
        );
    }

    #[test]
    fn networking_config_is_set() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
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
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
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

    #[test]
    fn agent_image_is_resolved() {
        let mut settings = Settings::new(Path::new(GOOD_SETTINGS_IMAGE_RESOLVE)).unwrap();
        agent_image_resolve(&mut settings).unwrap();

        assert_eq!(
            "parent_hostname:443/microsoft/azureiotedge-agent:1.0",
            settings.agent().config().image()
        );
    }

    #[cfg(unix)]
    #[test]
    fn content_trust_env_are_set_properly() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_CONTENT_TRUST)).unwrap();
        if let Some(content_trust_map) = settings
            .moby_runtime()
            .content_trust()
            .and_then(ContentTrust::ca_certs)
        {
            assert_eq!(
                content_trust_map.get("contoso1.azurcr.io"),
                Some(&std::path::PathBuf::from("/path/to/root_ca_contoso1.crt"))
            );
            assert_eq!(
                content_trust_map.get("contoso2.azurcr.io"),
                Some(&std::path::PathBuf::from("/path/to/root_ca_contoso2.crt"))
            );
            assert_eq!(
                content_trust_map.get(""),
                Some(&std::path::PathBuf::from("/path/to/root_ca_contoso3.crt"))
            );
            assert_eq!(
                content_trust_map.get("contoso4.azurcr.io"),
                Some(&std::path::PathBuf::from(
                    "/path/to/root_ca_contoso4_replaced.crt"
                ))
            );
            assert_eq!(
                content_trust_map.get("contoso5.azurcr.io"),
                Some(&std::path::PathBuf::from(""))
            );
        } else {
            panic!();
        }
    }

    #[cfg(unix)]
    #[test]
    fn content_trust_env_are_not_set_properly() {
        let settings = Settings::new(Path::new(BAD_SETTINGS_CONTENT_TRUST));
        assert!(settings.is_err());
    }
}
