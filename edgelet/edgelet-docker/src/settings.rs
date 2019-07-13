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
use serde_derive::{Deserialize, Serialize};
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

#[derive(Clone, Debug, Deserialize, Serialize)]
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
#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Settings {
    #[serde(flatten)]
    base: BaseSettings<DockerConfig>,
    moby_runtime: MobyRuntime,
}

impl Settings {
    pub fn new(filename: Option<&Path>) -> Result<Self, LoadSettingsError> {
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

    fn certificates(&self) -> Option<&Certificates> {
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

    use config::{File, FileFormat};

    use edgelet_core::{
        AttestationMethod, IpamConfig, DEFAULT_CONNECTION_STRING, DEFAULT_NETWORKID,
    };

    #[cfg(unix)]
    static GOOD_SETTINGS: &str = "test/linux/sample_settings.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS: &str = "test/linux/bad_sample_settings.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_TG: &str = "test/linux/sample_settings.tg.yaml";
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
    static X509_GOOD_SETTINGS1: &str = "test/linux/sample_settings.dps.x509.1.yaml";
    #[cfg(unix)]
    static X509_GOOD_SETTINGS2: &str = "test/linux/sample_settings.dps.x509.2.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DPS_X5091: &str = "test/linux/bad_settings.dps.x509.1.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS_DPS_X5092: &str = "test/linux/bad_settings.dps.x509.2.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_EXTERNAL: &str = "test/linux/sample_settings.external.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_NETWORK: &str = "test/linux/sample_settings.network.yaml";

    #[cfg(windows)]
    static GOOD_SETTINGS: &str = "test/windows/sample_settings.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS: &str = "test/windows/bad_sample_settings.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_TG: &str = "test/windows/sample_settings.tg.yaml";
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
    static X509_GOOD_SETTINGS1: &str = "test/windows/sample_settings.dps.x509.1.yaml";
    #[cfg(windows)]
    static X509_GOOD_SETTINGS2: &str = "test/windows/sample_settings.dps.x509.2.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DPS_X5091: &str = "test/windows/bad_settings.dps.x509.1.yaml";
    #[cfg(windows)]
    static BAD_SETTINGS_DPS_X5092: &str = "test/windows/bad_settings.dps.x509.2.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_EXTERNAL: &str = "test/windows/sample_settings.external.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS_NETWORK: &str = "test/windows/sample_settings.network.yaml";

    fn unwrap_manual_provisioning(p: &Provisioning) -> String {
        match p {
            Provisioning::Manual(manual) => manual.device_connection_string().to_string(),
            _ => "not implemented".to_string(),
        }
    }

    #[test]
    fn default_in_yaml_matches_constant() {
        let mut config = Config::default();
        config
            .merge(File::from_str(DEFAULTS, FileFormat::Yaml))
            .unwrap();
        let settings: Settings = config.try_into().unwrap();

        match settings.provisioning() {
            Provisioning::Manual(ref manual) => {
                assert_eq!(manual.device_connection_string(), DEFAULT_CONNECTION_STRING)
            }
            _ => unreachable!(),
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
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS_NETWORK)));
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
        let settings = Settings::new(Some(Path::new("garbage")));
        assert!(settings.is_err());
    }

    #[test]
    fn bad_file_gets_error() {
        let settings = Settings::new(Some(Path::new(BAD_SETTINGS)));
        assert!(settings.is_err());

        let settings = Settings::new(Some(Path::new(BAD_SETTINGS_DPS_DEFAULT)));
        assert!(settings.is_err());

        let settings = Settings::new(Some(Path::new(BAD_SETTINGS_DPS_TPM)));
        assert!(settings.is_err());

        let settings = Settings::new(Some(Path::new(BAD_SETTINGS_DPS_SYM_KEY)));
        assert!(settings.is_err());

        let settings = Settings::new(Some(Path::new(BAD_SETTINGS_DPS_X5091)));
        assert!(settings.is_err());

        let settings = Settings::new(Some(Path::new(BAD_SETTINGS_DPS_X5092)));
        assert!(settings.is_err());
    }

    #[test]
    fn manual_file_gets_sample_connection_string() {
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS)));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        let p = s.provisioning();
        let connection_string = unwrap_manual_provisioning(p);
        assert_eq!(
            connection_string,
            "HostName=something.something.com;DeviceId=something;SharedAccessKey=QXp1cmUgSW9UIEVkZ2U="
        );
    }

    #[test]
    fn manual_file_gets_sample_tg_paths() {
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS_TG)));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        let certificates = s.certificates();
        certificates
            .map(|c| {
                assert_eq!(c.device_ca_cert().to_str().unwrap(), "device_ca_cert.pem");
                assert_eq!(c.device_ca_pk().to_str().unwrap(), "device_ca_pk.pem");
                assert_eq!(
                    c.trusted_ca_certs().to_str().unwrap(),
                    "trusted_ca_certs.pem"
                );
            })
            .expect("certificates not configured");
    }

    #[test]
    fn dps_prov_default_get_settings() {
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS_DPS_DEFAULT)));
        assert!(settings.is_ok());
        let s = settings.unwrap();
        match s.provisioning() {
            Provisioning::Dps(ref dps) => {
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
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS_DPS_TPM)));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        match s.provisioning() {
            Provisioning::Dps(ref dps) => {
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
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS_DPS_SYM_KEY)));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        match s.provisioning() {
            Provisioning::Dps(ref dps) => {
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

    #[test]
    fn dps_prov_x509_default_settings() {
        let settings = Settings::new(Some(Path::new(X509_GOOD_SETTINGS1)));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        match s.provisioning() {
            Provisioning::Dps(ref dps) => {
                assert_eq!(dps.global_endpoint().scheme(), "scheme");
                assert_eq!(dps.global_endpoint().host_str().unwrap(), "jibba-jabba.net");
                assert_eq!(dps.scope_id(), "i got no time for the jibba-jabba");
                match dps.attestation() {
                    AttestationMethod::X509(ref x509) => {
                        assert!(x509.registration_id().is_none());
                        assert_eq!(x509.identity_cert(), Path::new("some/path/mr.t.cer.pem"));
                        assert_eq!(x509.identity_pk(), Path::new("some/path/mr.t.pk.pem"));
                    }
                    _ => unreachable!(),
                }
            }
            _ => unreachable!(),
        };
    }

    #[test]
    fn dps_prov_x509_reg_id_and_default_settings() {
        let settings = Settings::new(Some(Path::new(X509_GOOD_SETTINGS2)));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        match s.provisioning() {
            Provisioning::Dps(ref dps) => {
                assert_eq!(dps.global_endpoint().scheme(), "scheme");
                assert_eq!(dps.global_endpoint().host_str().unwrap(), "jibba-jabba.net");
                assert_eq!(dps.scope_id(), "i got no time for the jibba-jabba");
                match dps.attestation() {
                    AttestationMethod::X509(ref x509) => {
                        assert_eq!(x509.registration_id().unwrap(), "register me fool");
                        assert_eq!(x509.identity_cert(), Path::new("some/path/mr.t.cer.pem"));
                        assert_eq!(x509.identity_pk(), Path::new("some/path/mr.t.pk.pem"));
                    }
                    _ => unreachable!(),
                }
            }
            _ => unreachable!(),
        };
    }

    #[test]
    fn external_prov_get_settings() {
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS_EXTERNAL)));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        match s.provisioning() {
            Provisioning::External(ref external) => {
                assert_eq!(external.endpoint().as_str(), "http://localhost:9999/");
            }
            _ => unreachable!(),
        };
    }

    #[test]
    fn case_of_names_of_keys_is_preserved() {
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS_CASE_SENSITIVE))).unwrap();

        let env = settings.agent().env();
        assert_eq!(env.get("AbC").map(AsRef::as_ref), Some("VAluE1"));
        assert_eq!(env.get("DeF").map(AsRef::as_ref), Some("VAluE2"));

        let create_options = settings.agent().config().create_options();
        assert_eq!(create_options.hostname(), Some("VAluE3"));
    }

    #[test]
    fn watchdog_settings_are_read() {
        let settings = Settings::new(Some(Path::new(GOOD_SETTINGS)));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        let watchdog_settings = s.watchdog();
        assert_eq!(watchdog_settings.max_retries().compare(3), Ordering::Equal);
    }
}
