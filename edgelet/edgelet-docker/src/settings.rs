// Copyright (c) Microsoft. All rights reserved.

use std::collections::BTreeMap;
use std::path::Path;

use docker::models::{ContainerCreateBodyNetworkingConfig, EndpointSettings, HostConfig};
use edgelet_core::{MobyNetwork, UrlExt};
use edgelet_settings::{
    aziot::AutoReprovisioningMode, aziot::Endpoints, module::Settings as ModuleSpec, uri::Connect,
    uri::Listen, watchdog::Settings as WatchdogSettings, RuntimeSettings,
};

use failure::{Context, Fail, ResultExt};

use url::Url;

use crate::config::DockerConfig;
use crate::error::{Error, ErrorKind};

/// This is the key for the docker network Id.
const EDGE_NETWORKID_KEY: &str = "NetworkId";

const UNIX_SCHEME: &str = "unix";

/// This struct is the same as the Settings type from the `edgelet_core` crate
/// except that it also sets up the volume mounting of workload & management
/// UDS sockets for the edge agent container and injects the docker network
/// name both as an environment variable and as an endpoint setting in the
/// docker create options for edge agent.

pub const CONFIG_FILE_DEFAULT: &str = "/etc/aziot/edged/config.toml";

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
