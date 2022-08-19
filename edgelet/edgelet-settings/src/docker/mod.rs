// Copyright (c) Microsoft. All rights reserved.

mod init;

pub mod config;
pub mod network;
pub mod runtime;

#[derive(Clone, Debug, serde::Deserialize, serde::Serialize)]
pub struct Settings {
    #[serde(flatten)]
    pub base: crate::base::Settings<config::DockerConfig>,

    pub moby_runtime: runtime::MobyRuntime,
}

pub const CONFIG_FILE_DEFAULT: &str = "/etc/aziot/edged/config.toml";

impl Settings {
    /// Load the aziot-edged configuration.
    ///
    /// Configuration is made up of /etc/aziot/edged/config.toml (overridden by the `AZIOT_EDGED_CONFIG` env var)
    /// and any files in the /etc/aziot/edged/config.d directory (overridden by the `AZIOT_EDGED_CONFIG_DIR` env var).
    pub fn new() -> Result<Self, Box<dyn std::error::Error>> {
        let config_path = std::env::var("AZIOT_EDGED_CONFIG")
            .unwrap_or_else(|_| "/etc/aziot/edged/config.toml".to_string());
        let config_path = std::path::Path::new(&config_path);

        let config_directory_path = std::env::var("AZIOT_EDGED_CONFIG_DIR")
            .unwrap_or_else(|_| "/etc/aziot/edged/config.d".to_string());
        let config_directory_path = std::path::Path::new(&config_directory_path);

        let mut settings: Settings =
            config_common::read_config(config_path, Some(config_directory_path))?;

        init::agent_spec(&mut settings)?;

        Ok(settings)
    }

    pub fn moby_runtime(&self) -> &runtime::MobyRuntime {
        &self.moby_runtime
    }

    #[must_use]
    pub fn agent_upstream_resolve(mut self, parent_hostname: &str) -> Self {
        crate::RuntimeSettings::agent_mut(&mut self)
            .config_mut()
            .parent_hostname_resolve(parent_hostname);

        self
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

    fn edge_ca_auto_renew(&self) -> &Option<cert_renewal::AutoRenewConfig> {
        self.base.edge_ca_auto_renew()
    }

    fn edge_ca_subject(&self) -> &Option<aziot_certd_config::CertSubject> {
        self.base.edge_ca_subject()
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

    fn iotedge_max_requests(&self) -> &crate::IotedgeMaxRequests {
        self.base.iotedge_max_requests()
    }

    fn homedir(&self) -> &std::path::Path {
        self.base.homedir()
    }

    fn allow_elevated_docker_permissions(&self) -> bool {
        self.base.allow_elevated_docker_permissions()
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

    fn additional_info(&self) -> &std::collections::BTreeMap<String, String> {
        self.base.additional_info()
    }

    fn image_garbage_collection(&self) -> &crate::base::image::ImagePruneSettings {
        self.base.image_garbage_collection()
    }
}

#[cfg(test)]
mod tests {
    use std::time::Duration;

    use super::Settings;
    use crate::docker::network;
    use crate::RuntimeSettings;
    use crate::DEFAULT_NETWORKID;

    // Prevents multiple tests from modifying environment variables concurrently.
    lazy_static::lazy_static! {
        static ref ENV_LOCK: std::sync::Mutex<()> = std::sync::Mutex::default();
    }

    static CONFIG_DIR: &str = "test-files/config.d";

    // Test files.
    static BAD_SETTINGS: &str = "test-files/bad_sample_settings.toml";
    static BAD_SETTINGS_CONTENT_TRUST: &str = "test-files/bad_settings_content_trust.toml";

    static GOOD_SETTINGS: &str = "test-files/sample_settings.toml";
    static GOOD_SETTINGS_CASE_SENSITIVE: &str = "test-files/case_sensitive.toml";
    static GOOD_SETTINGS_CONTENT_TRUST: &str = "test-files/sample_settings_content_trust.toml";
    static GOOD_SETTINGS_NETWORK: &str = "test-files/sample_settings.network.toml";
    static GOOD_SETTINGS_IMAGE_GC: &str = "test-files/sample_settings_image_gc.toml";

    #[test]
    fn err_no_file() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");
        std::env::set_var("AZIOT_EDGED_CONFIG", "garbage");
        let settings = Settings::new();
        assert!(settings.is_err());
    }

    #[test]
    fn err_bad_file() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", BAD_SETTINGS);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let settings = Settings::new();
        assert!(settings.is_err());
    }

    #[test]
    fn case_sensitive() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS_CASE_SENSITIVE);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let settings = Settings::new().unwrap();

        let env = settings.agent().env();
        assert_eq!(env.get("AbC").map(AsRef::as_ref), Some("VAluE1"));
        assert_eq!(env.get("DeF").map(AsRef::as_ref), Some("VAluE2"));

        let create_options = settings.agent().config().create_options();
        assert_eq!(create_options.hostname(), Some("VAluE3"));
    }

    #[test]
    fn watchdog() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let settings = Settings::new().unwrap();
        let watchdog_settings = settings.watchdog();
        assert_eq!(watchdog_settings.max_retries(), 3);
    }

    #[test]
    fn min_tls_version_default() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let settings = Settings::new().unwrap();
        assert_eq!(
            settings.listen().min_tls_version(),
            crate::uri::MinTlsVersion::Tls10
        );
    }

    #[test]
    fn network_settings() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS_NETWORK);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let settings = Settings::new().unwrap();
        let moby_runtime = settings.moby_runtime();
        assert_eq!(
            moby_runtime.uri().to_string(),
            "http://localhost:2375/".to_string()
        );

        let network = moby_runtime.network();
        assert_eq!(network.name(), DEFAULT_NETWORKID);
        match network {
            network::MobyNetwork::Network(moby_network) => {
                assert!(moby_network.ipv6().unwrap());
                let ipam_spec = moby_network.ipam().expect("Expected IPAM specification.");
                let ipam_config = ipam_spec.config().expect("Expected IPAM configuration.");
                let ipam_1 = network::IpamConfig::default()
                    .with_gateway("172.18.0.1".to_string())
                    .with_ip_range("172.18.0.0/16".to_string())
                    .with_subnet("172.18.0.0/16".to_string());
                let ipam_2 = network::IpamConfig::default()
                    .with_gateway("2001:4898:e0:3b1:1::1".to_string())
                    .with_ip_range("2001:4898:e0:3b1:1::/80".to_string())
                    .with_subnet("2001:4898:e0:3b1:1::/80".to_string());
                let expected_ipam_config: Vec<network::IpamConfig> = vec![ipam_1, ipam_2];

                for ipam_config in ipam_config.iter() {
                    assert!(expected_ipam_config.contains(ipam_config));
                }
            }
            network::MobyNetwork::Name(_name) => panic!("Unexpected network configuration."),
        };
    }

    #[test]
    fn networking_create_options() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let settings = Settings::new().unwrap();
        let create_options = settings.agent().config().create_options();
        assert!(create_options
            .networking_config()
            .unwrap()
            .endpoints_config()
            .unwrap()
            .contains_key(DEFAULT_NETWORKID));
    }

    #[test]
    fn agent_labels() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

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

    #[test]
    fn image_garbage_collection() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS_IMAGE_GC);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let settings = Settings::new().unwrap();
        let image_prune_settings = settings.image_garbage_collection();
        assert!(image_prune_settings.is_enabled());
        assert_eq!(
            image_prune_settings.image_age_cleanup_threshold(),
            Duration::from_secs(1440 * 60 * 3 * 7)
        );
        assert_eq!(
            image_prune_settings.cleanup_recurrence(),
            Duration::from_secs(1440 * 60 * 3)
        );
        assert_eq!(image_prune_settings.cleanup_time(), 600);
    }

    #[test]
    fn image_garbage_collection_defaults() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let image_gc_settings = Settings::new().unwrap().image_garbage_collection().clone();
        assert!(image_gc_settings.is_enabled());
        assert_eq!(
            image_gc_settings.image_age_cleanup_threshold(),
            Duration::from_secs(60 * 60 * 24 * 7)
        );
        assert_eq!(
            image_gc_settings.cleanup_recurrence(),
            Duration::from_secs(60 * 60 * 24)
        );
        assert_eq!(image_gc_settings.cleanup_time(), 0);
    }

    #[test]
    fn content_trust_env() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", GOOD_SETTINGS_CONTENT_TRUST);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let settings = Settings::new().unwrap();
        if let Some(content_trust_map) = settings
            .moby_runtime()
            .content_trust()
            .and_then(crate::docker::runtime::ContentTrust::ca_certs)
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

    #[test]
    fn content_trust_env_err() {
        let _env_lock = ENV_LOCK.lock().expect("env lock poisoned");

        std::env::set_var("AZIOT_EDGED_CONFIG", BAD_SETTINGS_CONTENT_TRUST);
        std::env::set_var("AZIOT_EDGED_CONFIG_DIR", CONFIG_DIR);

        let settings = Settings::new();
        assert!(settings.is_err());
    }
}
