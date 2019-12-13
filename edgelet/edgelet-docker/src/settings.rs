// Copyright (c) Microsoft. All rights reserved.

use std::path::Path;

use config::{Config, Environment};
use docker::models::HostConfig;
use edgelet_core::{
    Certificates, Connect, Listen, MobyNetwork, ModuleSpec, Provisioning, RuntimeSettings,
    Settings as BaseSettings, UrlExt, WatchdogSettings,
};
use edgelet_utils::YamlFileSource;
use failure::{Context, Fail, ResultExt};

use url::Url;

use crate::config::DockerConfig;
use crate::error::{Error, ErrorKind};

#[cfg(unix)]
pub const DEFAULTS: &str = include_str!("../config/unix/default.yaml");

#[cfg(windows)]
pub const DEFAULTS: &str = include_str!("../config/windows/default.yaml");

/// This is the key for the docker network Id.
const EDGE_NETWORKID_KEY: &str = "NetworkId";

const UNIX_SCHEME: &str = "unix";

#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct MobyRuntime {
    #[serde(with = "url_serde")]
    uri: Url,
    network: MobyNetwork,
}

impl MobyRuntime {
    pub fn uri(&self) -> &Url {
        &self.uri
    }

    pub fn network(&self) -> &MobyNetwork {
        &self.network
    }
}

/// This struct is the same as the Settings type from the `edgelet_core` crate
/// except that it also sets up the volume mounting of workload & management
/// UDS sockets for the edge agent container and also injects the docker
/// network name as an environment variable for edge agent.
#[derive(Clone, Debug, serde_derive::Deserialize, serde_derive::Serialize)]
pub struct Settings {
    #[serde(flatten)]
    base: BaseSettings<DockerConfig>,
    moby_runtime: MobyRuntime,
}

impl Settings {
    pub fn new(filename: &Path) -> Result<Self, LoadSettingsError> {
        let mut config = Config::default();
        config.merge(YamlFileSource::String(DEFAULTS))?;
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

    fn certificates(&self) -> &Certificates {
        self.base.certificates()
    }

    fn watchdog(&self) -> &WatchdogSettings {
        self.base.watchdog()
    }
}

fn init_agent_spec(settings: &mut Settings) -> Result<(), LoadSettingsError> {
    // setup vol mounts for workload/management sockets
    agent_vol_mount(settings)?;

    // setup environment variables that are moby/docker specific
    agent_env(settings);

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
            // On Windows we mount the parent folder because we can't mount the
            // socket files directly
            #[cfg(windows)]
            let path = path
                .parent()
                .ok_or_else(|| ErrorKind::InvalidSocketUri(uri.to_string()))?;
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
    use super::*;

    use std::cmp::Ordering;
    use std::fs::File;
    use std::io::prelude::*;

    use serde_json::json;
    use tempdir::TempDir;

    use edgelet_core::{
        AttestationMethod, IpamConfig, ManualAuthMethod, ProvisioningType, DEFAULT_NETWORKID,
    };

    #[cfg(unix)]
    static GOOD_SETTINGS: &str = "test/linux/sample_settings.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_MANUAL_CS_AUTH: &str = "test/linux/sample_settings.manual.auth.cs.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS: &str = "test/linux/bad_sample_settings.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_CS2: &str = "test/linux/bad_sample_settings.cs.2.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_CS3: &str = "test/linux/bad_sample_settings.cs.3.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_DPS_SYM_KEY: &str = "test/linux/sample_settings.dps.sym.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_CASE_SENSITIVE: &str = "test/linux/case_sensitive.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_DPS_TPM: &str = "test/linux/sample_settings.dps.tpm.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_DPS_DEFAULT: &str = "test/linux/sample_settings.dps.default.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DPS_TPM: &str = "test/linux/bad_sample_settings.dps.tpm.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DPS_DEFAULT: &str = "test/linux/bad_sample_settings.dps.default.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DPS_SYM_KEY: &str = "test/linux/bad_sample_settings.dps.sym.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DPS_X5091: &str = "test/linux/bad_settings.dps.x509.1.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DPS_X5092: &str = "test/linux/bad_settings.dps.x509.2.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DPS_X5093: &str = "test/linux/bad_settings.dps.x509.3.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DPS_X5094: &str = "test/linux/bad_settings.dps.x509.4.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_CS_AUTH1: &str = "test/linux/bad_settings.manual.cs.1.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_CS_AUTH2: &str = "test/linux/bad_settings.manual.cs.2.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_CS_AUTH3: &str = "test/linux/bad_settings.manual.cs.3.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_CS_AUTH4: &str = "test/linux/bad_settings.manual.cs.4.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_X509_AUTH1: &str = "test/linux/bad_settings.manual.x509.1.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_X509_AUTH2: &str = "test/linux/bad_settings.manual.x509.2.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_X509_AUTH3: &str = "test/linux/bad_settings.manual.x509.3.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_X509_AUTH4: &str = "test/linux/bad_settings.manual.x509.4.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_MANUAL_X509_AUTH5: &str = "test/linux/bad_settings.manual.x509.5.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_EXTERNAL1: &str = "test/linux/sample_settings.external.1.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_EXTERNAL2: &str = "test/linux/sample_settings.external.2.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_NETWORK: &str = "test/linux/sample_settings.network.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_DYNAMIC_REPROVISIONING: &str = "test/linux/sample_settings.dyn.repro.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DYNAMIC_REPROVISIONING: &str =
        "test/linux/bad_sample_settings.dyn.repro.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_TLS: &str = "test/linux/sample_settings.tls.yaml";

    #[cfg(windows)]
    static GOOD_SETTINGS: &str = "test/windows/sample_settings.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_MANUAL_CS_AUTH: &str = "test/windows/sample_settings.manual.auth.cs.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS: &str = "test/windows/bad_sample_settings.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_CS2: &str = "test/windows/bad_sample_settings.cs.2.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_CS3: &str = "test/windows/bad_sample_settings.cs.3.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_DPS_SYM_KEY: &str = "test/windows/sample_settings.dps.sym.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_CASE_SENSITIVE: &str = "test/windows/case_sensitive.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_DPS_TPM: &str = "test/windows/sample_settings.dps.tpm.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_DPS_DEFAULT: &str = "test/windows/sample_settings.dps.default.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DPS_TPM: &str = "test/windows/bad_sample_settings.dps.tpm.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DPS_DEFAULT: &str = "test/windows/bad_sample_settings.dps.default.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DPS_SYM_KEY: &str = "test/windows/bad_sample_settings.dps.sym.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DPS_X5091: &str = "test/windows/bad_settings.dps.x509.1.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DPS_X5092: &str = "test/windows/bad_settings.dps.x509.2.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DPS_X5093: &str = "test/windows/bad_settings.dps.x509.3.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DPS_X5094: &str = "test/windows/bad_settings.dps.x509.4.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_CS_AUTH1: &str = "test/windows/bad_settings.manual.cs.1.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_CS_AUTH2: &str = "test/windows/bad_settings.manual.cs.2.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_CS_AUTH3: &str = "test/windows/bad_settings.manual.cs.3.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_CS_AUTH4: &str = "test/windows/bad_settings.manual.cs.4.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_X509_AUTH1: &str = "test/windows/bad_settings.manual.x509.1.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_X509_AUTH2: &str = "test/windows/bad_settings.manual.x509.2.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_X509_AUTH3: &str = "test/windows/bad_settings.manual.x509.3.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_X509_AUTH4: &str = "test/windows/bad_settings.manual.x509.4.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_MANUAL_X509_AUTH5: &str = "test/windows/bad_settings.manual.x509.5.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_EXTERNAL1: &str = "test/windows/sample_settings.external.1.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_EXTERNAL2: &str = "test/windows/sample_settings.external.2.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_NETWORK: &str = "test/windows/sample_settings.network.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_DYNAMIC_REPROVISIONING: &str =
        "test/windows/sample_settings.dyn.repro.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DYNAMIC_REPROVISIONING: &str =
        "test/windows/bad_sample_settings.dyn.repro.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_TLS: &str = "test/windows/sample_settings.tls.yaml";

    fn unwrap_manual_provisioning(p: &ProvisioningType) -> String {
        match p {
            ProvisioningType::Manual(manual) => {
                if let ManualAuthMethod::DeviceConnectionString(cs) = manual.authentication_method()
                {
                    cs.device_connection_string().to_string()
                } else {
                    "not implemented".to_string()
                }
            }
            _ => "not implemented".to_string(),
        }
    }

    #[test]
    fn network_default() {
        let moby1 = MobyRuntime {
            uri: Url::parse("http://test").unwrap(),
            network: MobyNetwork::Name("".to_string()),
        };
        assert_eq!(DEFAULT_NETWORKID, moby1.network().name());

        let moby2 = MobyRuntime {
            uri: Url::parse("http://test").unwrap(),
            network: MobyNetwork::Name("some-network".to_string()),
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

        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_CS2));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_DPS_DEFAULT));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_DPS_TPM));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_DPS_SYM_KEY));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_DPS_X5091));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_DPS_X5092));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_DPS_X5093));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_DPS_X5094));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_X509_AUTH1));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_X509_AUTH2));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_X509_AUTH3));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_X509_AUTH4));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_X509_AUTH5));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_CS_AUTH1));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_CS_AUTH3));
        assert!(settings.is_err());

        let settings = Settings::new(Path::new(BAD_SETTINGS_DYNAMIC_REPROVISIONING));
        assert!(settings.is_err());
    }

    #[test]
    fn manual_file_gets_sample_connection_string() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();

        assert_eq!(s.provisioning().dynamic_reprovisioning(), false);
        let p = s.provisioning().provisioning_type();
        let connection_string = unwrap_manual_provisioning(p);
        assert_eq!(
            connection_string,
            "HostName=something.something.com;DeviceId=something;SharedAccessKey=QXp1cmUgSW9UIEVkZ2U="
        );
    }

    #[test]
    fn manual_authentication_connection_string() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_MANUAL_CS_AUTH));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        assert_eq!(s.provisioning().dynamic_reprovisioning(), false);

        let p = s.provisioning().provisioning_type();
        let connection_string = unwrap_manual_provisioning(p);
        assert_eq!(
            connection_string,
            "HostName=something.something.com;DeviceId=something;SharedAccessKey=QXp1cmUgSW9UIEVkZ2U="
        );
    }

    #[test]
    fn manual_empty_connection_string_fails() {
        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_CS3));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        match s.provisioning().provisioning_type() {
            ProvisioningType::Manual(manual) => match manual.authentication_method() {
                ManualAuthMethod::DeviceConnectionString(cs) => {
                    assert_eq!(cs.device_connection_string(), "");
                    cs.parse_device_connection_string().unwrap_err();
                }
                _ => unreachable!(),
            },
            _ => unreachable!(),
        }
    }

    #[test]
    fn manual_authentication_bad_connection_string_fails() {
        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_CS_AUTH2));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        match s.provisioning().provisioning_type() {
            ProvisioningType::Manual(manual) => match manual.authentication_method() {
                ManualAuthMethod::DeviceConnectionString(cs) => {
                    assert_eq!(cs.device_connection_string(), "blah");
                    cs.parse_device_connection_string().unwrap_err();
                }
                _ => unreachable!(),
            },
            _ => unreachable!(),
        }
    }

    #[test]
    fn manual_authentication_empty_connection_string_fails() {
        let settings = Settings::new(Path::new(BAD_SETTINGS_MANUAL_CS_AUTH4));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        match s.provisioning().provisioning_type() {
            ProvisioningType::Manual(manual) => match manual.authentication_method() {
                ManualAuthMethod::DeviceConnectionString(cs) => {
                    assert_eq!(cs.device_connection_string(), "");
                    cs.parse_device_connection_string().unwrap_err();
                }
                _ => unreachable!(),
            },
            _ => unreachable!(),
        }
    }

    fn prepare_test_manual_x509_authentication_settings_yaml(
        settings_path: &Path,
        id_cert_path: &Path,
        id_key_path: &Path,
    ) -> String {
        File::create(&id_cert_path)
            .expect("Test identity cert file could not be created")
            .write_all(b"CN=Identity Cert")
            .expect("Test identity cert file could not be written");

        File::create(&id_key_path)
            .expect("Test identity private key file could not be created")
            .write_all(b"Gateway Private Key")
            .expect("Test identity private key file could not be written");

        let settings_yaml = json!({
        "provisioning": {
            "source": "manual",
            "authentication": {
                "method": "x509",
                "iothub_hostname": "something.something.com",
                "device_id": "something",
                "identity_cert": Url::from_file_path(id_cert_path).unwrap().into_string(),
                "identity_pk": Url::from_file_path(id_key_path).unwrap().into_string(),
            }
        }})
        .to_string();

        File::create(&settings_path)
            .expect("Test settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Test settings file could not be written");

        settings_yaml
    }

    fn prepare_test_gateway_x509_certificate_settings_yaml(
        settings_path: &Path,
        ca_cert_path: &Path,
        ca_key_path: &Path,
        trust_bundle_path: &Path,
        use_uri_format: bool,
    ) -> String {
        File::create(&ca_cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Gateway CA")
            .expect("Test cert file could not be written");

        File::create(&ca_key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"Gateway Private Key")
            .expect("Test cert private key file could not be written");

        File::create(&trust_bundle_path)
            .expect("Test trust bundle file could not be created")
            .write_all(b"Trust me, I'm good for it.")
            .expect("Test trust bundle file could not be written");

        let (ca_cert_setting, ca_key_setting, trust_bundle_setting) = if use_uri_format {
            (
                Url::from_file_path(ca_cert_path).unwrap().into_string(),
                Url::from_file_path(ca_key_path).unwrap().into_string(),
                Url::from_file_path(trust_bundle_path)
                    .unwrap()
                    .into_string(),
            )
        } else {
            (
                ca_cert_path.to_str().unwrap().to_owned(),
                ca_key_path.to_str().unwrap().to_owned(),
                trust_bundle_path.to_str().unwrap().to_owned(),
            )
        };
        let settings_yaml = json!({
            "provisioning": {
                "source": "manual",
                "device_connection_string": "HostName=something.something.com;DeviceId=something;SharedAccessKey=QXp1cmUgSW9UIEVkZ2U="
            },
            "certificates": {
                "device_ca_cert": ca_cert_setting,
                "device_ca_pk": ca_key_setting,
                "trusted_ca_certs": trust_bundle_setting,
            }}).to_string();

        File::create(&settings_path)
            .expect("Test settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Test settings file could not be written");

        settings_yaml
    }

    #[test]
    fn manual_file_gets_sample_tg_file_paths() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let ca_cert_path = tmp_dir.path().join("device_ca_cert.pem");
        let ca_key_path = tmp_dir.path().join("device_ca_pk.pem");
        let trust_bundle_path = tmp_dir.path().join("trusted_ca_certs.pem");
        let settings_path = tmp_dir.path().join("test_settings.yaml");
        prepare_test_gateway_x509_certificate_settings_yaml(
            &settings_path,
            &ca_cert_path,
            &ca_key_path,
            &trust_bundle_path,
            false,
        );
        let settings = Settings::new(&settings_path).expect("Settings create failed");
        println!("{:?}", settings);
        let certificates = settings.certificates().device_cert();
        certificates
            .map(|c| {
                let path = c.device_ca_cert().expect("Did not obtain device CA cert");
                assert_eq!(ca_cert_path, path);
                let path = c
                    .device_ca_pk()
                    .expect("Did not obtain device CA private key");
                assert_eq!(ca_key_path, path);
                let path = c.trusted_ca_certs().expect("Did not obtain trust bundle");
                assert_eq!(trust_bundle_path, path);
            })
            .expect("certificates not configured");
    }

    #[test]
    fn manual_file_gets_sample_tg_file_uris() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let ca_cert_path = tmp_dir.path().join("device_ca_cert.pem");
        let ca_key_path = tmp_dir.path().join("device_ca_pk.pem");
        let trust_bundle_path = tmp_dir.path().join("trusted_ca_certs.pem");
        let settings_path = tmp_dir.path().join("test_settings.yaml");
        prepare_test_gateway_x509_certificate_settings_yaml(
            &settings_path,
            &ca_cert_path,
            &ca_key_path,
            &trust_bundle_path,
            true,
        );
        let settings = Settings::new(&settings_path).unwrap();
        println!("{:?}", settings);
        let certificates = settings.certificates().device_cert();
        certificates
            .map(|c| {
                let path = c.device_ca_cert().expect("Did not obtain device CA cert");
                assert_eq!(ca_cert_path, path);
                let path = c
                    .device_ca_pk()
                    .expect("Did not obtain device CA private key");
                assert_eq!(ca_key_path, path);
                let path = c.trusted_ca_certs().expect("Did not obtain trust bundle");
                assert_eq!(trust_bundle_path, path);
            })
            .expect("certificates not configured");
    }

    #[test]
    fn manual_x509_authentication() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let id_cert_path = tmp_dir.path().join("device_id_cert.pem");
        let id_key_path = tmp_dir.path().join("device_id_pk.pem");
        let settings_path = tmp_dir.path().join("test_settings.yaml");
        prepare_test_manual_x509_authentication_settings_yaml(
            &settings_path,
            &id_cert_path,
            &id_key_path,
        );
        let settings = Settings::new(&settings_path).unwrap();
        println!("{:?}", settings);
        match settings.provisioning().provisioning_type() {
            ProvisioningType::Manual(manual) => match manual.authentication_method() {
                ManualAuthMethod::X509(x509) => {
                    assert_eq!(x509.iothub_hostname(), "something.something.com");
                    assert_eq!(x509.device_id(), "something");
                    assert_eq!(
                        &Url::parse(&format!("file://{}", id_cert_path.to_str().unwrap())).unwrap(),
                        x509.identity_cert_uri().unwrap(),
                    );
                    assert_eq!(
                        &Url::parse(&format!("file://{}", id_key_path.to_str().unwrap())).unwrap(),
                        x509.identity_pk_uri().unwrap(),
                    );
                    assert_eq!(
                        id_cert_path.to_str().unwrap(),
                        x509.identity_cert().unwrap().to_str().unwrap(),
                    );
                    assert_eq!(
                        id_key_path.to_str().unwrap(),
                        x509.identity_pk().unwrap().to_str().unwrap(),
                    );
                }
                _ => unreachable!(),
            },
            _ => unreachable!(),
        }
    }

    #[test]
    fn dps_prov_default_get_settings() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_DPS_DEFAULT));
        assert!(settings.is_ok());
        let s = settings.unwrap();
        assert_eq!(s.provisioning().dynamic_reprovisioning(), false);
        match s.provisioning().provisioning_type() {
            ProvisioningType::Dps(ref dps) => {
                assert_eq!(dps.global_endpoint().scheme(), "scheme");
                assert_eq!(dps.global_endpoint().host_str().unwrap(), "jibba-jabba.net");
                assert_eq!(dps.scope_id(), "i got no time for the jibba-jabba");
                match dps.attestation() {
                    AttestationMethod::Tpm(ref tpm) => {
                        assert_eq!(tpm.registration_id(), "register me fool");
                    }
                    _ => unreachable!(),
                }
            }
            _ => unreachable!(),
        };
    }

    #[test]
    fn dps_prov_tpm_get_settings() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_DPS_TPM));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        assert_eq!(s.provisioning().dynamic_reprovisioning(), false);
        match s.provisioning().provisioning_type() {
            ProvisioningType::Dps(ref dps) => {
                assert_eq!(dps.global_endpoint().scheme(), "scheme");
                assert_eq!(dps.global_endpoint().host_str().unwrap(), "jibba-jabba.net");
                assert_eq!(dps.scope_id(), "i got no time for the jibba-jabba");
                match dps.attestation() {
                    AttestationMethod::Tpm(ref tpm) => {
                        assert_eq!(tpm.registration_id(), "register me fool");
                    }
                    _ => unreachable!(),
                }
            }
            _ => unreachable!(),
        };
    }

    #[test]
    fn dps_prov_symmetric_key_get_settings() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_DPS_SYM_KEY));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        assert_eq!(s.provisioning().dynamic_reprovisioning(), true);
        match s.provisioning().provisioning_type() {
            ProvisioningType::Dps(ref dps) => {
                assert_eq!(dps.global_endpoint().scheme(), "scheme");
                assert_eq!(dps.global_endpoint().host_str().unwrap(), "jibba-jabba.net");
                assert_eq!(dps.scope_id(), "i got no time for the jibba-jabba");
                match dps.attestation() {
                    AttestationMethod::SymmetricKey(ref key) => {
                        assert_eq!(key.symmetric_key(), "key");
                        assert_eq!(key.registration_id(), "register me fool");
                    }
                    _ => unreachable!(),
                }
            }
            _ => unreachable!(),
        };
    }

    fn prepare_test_dps_x509_settings_yaml(
        settings_path: &Path,
        cert_path: &Path,
        key_path: &Path,
    ) -> String {
        File::create(&cert_path)
            .expect("Test cert file could not be created")
            .write_all(b"CN=Mr. T")
            .expect("Test cert file could not be written");

        File::create(&key_path)
            .expect("Test cert private key file could not be created")
            .write_all(b"i pity the fool")
            .expect("Test cert private key file could not be written");

        let cert_uri = format!("file://{}", cert_path.to_str().unwrap());
        let pk_uri = format!("file://{}", key_path.to_str().unwrap());
        let settings_yaml = json!({
        "provisioning": {
            "source": "dps",
            "global_endpoint": "scheme://jibba-jabba.net",
            "scope_id": "i got no time for the jibba-jabba",
            "attestation": {
                "method": "x509",
                "identity_cert": cert_uri,
                "identity_pk": pk_uri,
            },
        }})
        .to_string();
        File::create(&settings_path)
            .expect("Test settings file could not be created")
            .write_all(settings_yaml.as_bytes())
            .expect("Test settings file could not be written");

        settings_yaml
    }

    #[test]
    fn dps_prov_x509_default_settings() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let cert_path = tmp_dir.path().join("test_cert");
        let key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");
        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &key_path);
        let settings = Settings::new(&settings_path).unwrap();
        println!("{:?}", settings);
        match settings.provisioning().provisioning_type() {
            ProvisioningType::Dps(ref dps) => {
                assert_eq!(dps.global_endpoint().scheme(), "scheme");
                assert_eq!(dps.global_endpoint().host_str().unwrap(), "jibba-jabba.net");
                assert_eq!(dps.scope_id(), "i got no time for the jibba-jabba");
                match dps.attestation() {
                    AttestationMethod::X509(ref x509) => {
                        assert!(x509.registration_id().is_none());
                        assert_eq!(
                            &Url::parse(&format!("file://{}", cert_path.to_str().unwrap()))
                                .unwrap(),
                            x509.identity_cert_uri().unwrap(),
                        );
                        assert_eq!(
                            &Url::parse(&format!("file://{}", key_path.to_str().unwrap())).unwrap(),
                            x509.identity_pk_uri().unwrap(),
                        );
                        assert_eq!(
                            cert_path.to_str().unwrap(),
                            x509.identity_cert().unwrap().to_str().unwrap(),
                        );
                        assert_eq!(
                            key_path.to_str().unwrap(),
                            x509.identity_pk().unwrap().to_str().unwrap(),
                        );
                    }
                    _ => unreachable!(),
                }
            }
            _ => unreachable!(),
        };
    }

    #[test]
    fn dps_prov_x509_reg_id_and_default_settings() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let cert_path = tmp_dir.path().join("test_cert");
        let key_path = tmp_dir.path().join("test_key");
        let settings_path = tmp_dir.path().join("test_settings.yaml");
        prepare_test_dps_x509_settings_yaml(&settings_path, &cert_path, &key_path);
        let settings = Settings::new(&settings_path).unwrap();
        println!("{:?}", settings);
        match settings.provisioning().provisioning_type() {
            ProvisioningType::Dps(ref dps) => {
                assert_eq!(dps.global_endpoint().scheme(), "scheme");
                assert_eq!(dps.global_endpoint().host_str().unwrap(), "jibba-jabba.net");
                assert_eq!(dps.scope_id(), "i got no time for the jibba-jabba");
                match dps.attestation() {
                    AttestationMethod::X509(ref x509) => {
                        assert!(x509.registration_id().is_none());
                        assert_eq!(
                            &Url::parse(&format!("file://{}", cert_path.to_str().unwrap()))
                                .unwrap(),
                            x509.identity_cert_uri().unwrap(),
                        );
                        assert_eq!(
                            &Url::parse(&format!("file://{}", key_path.to_str().unwrap())).unwrap(),
                            x509.identity_pk_uri().unwrap(),
                        );
                        assert_eq!(
                            cert_path.to_str().unwrap(),
                            x509.identity_cert().unwrap().to_str().unwrap(),
                        );
                        assert_eq!(
                            key_path.to_str().unwrap(),
                            x509.identity_pk().unwrap().to_str().unwrap(),
                        );
                    }
                    _ => unreachable!(),
                }
            }
            _ => unreachable!(),
        };
    }

    #[test]
    fn external_prov_get_settings() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_EXTERNAL1));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        assert_eq!(s.provisioning().dynamic_reprovisioning(), false);
        match s.provisioning().provisioning_type() {
            ProvisioningType::External(ref external) => {
                assert_eq!(external.endpoint().as_str(), "http://localhost:9999/");
            }
            _ => unreachable!(),
        };
    }

    #[test]
    fn external_prov_get_settings_with_dynamic_reprovisioning() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_EXTERNAL2));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        assert_eq!(s.provisioning().dynamic_reprovisioning(), true);

        match s.provisioning().provisioning_type() {
            ProvisioningType::External(ref external) => {
                assert_eq!(external.endpoint().as_str(), "http://localhost:9999/");
            }
            _ => unreachable!(),
        };
    }

    #[test]
    fn manual_provisioning_settings_with_dynamic_reprovisioning() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_DYNAMIC_REPROVISIONING));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();

        assert_eq!(s.provisioning().dynamic_reprovisioning(), true);
        let p = s.provisioning().provisioning_type();
        let connection_string = unwrap_manual_provisioning(p);
        assert_eq!(
            connection_string,
            "HostName=something.something.com;DeviceId=something;SharedAccessKey=QXp1cmUgSW9UIEVkZ2U="
        );
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
    fn tls_settings_are_read() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS_TLS)).unwrap();
        assert_eq!(
            settings.listen().min_tls_version(),
            edgelet_core::Protocol::Tls12
        );
    }

    #[test]
    fn tls_settings_are_none_by_default() {
        let settings = Settings::new(Path::new(GOOD_SETTINGS)).unwrap();
        assert_eq!(
            settings.listen().min_tls_version(),
            edgelet_core::Protocol::Tls10
        );
    }
}
