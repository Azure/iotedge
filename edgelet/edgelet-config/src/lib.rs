// Copyright (c) Microsoft. All rights reserved.

#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::default_trait_access,
    clippy::doc_markdown, // clippy want the "IoT" of "IoT Hub" in a code fence
    clippy::module_name_repetitions,
    clippy::shadow_unrelated,
    clippy::use_self,
)]

use std::fs::OpenOptions;
use std::io::Read;
use std::path::{Path, PathBuf};

use config::{Config, Environment};
use failure::{Context, Fail};
use log::{debug, Level};
use regex::Regex;
use serde::de::DeserializeOwned;
use serde::Serialize;
use serde_derive::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use url::Url;

use edgelet_core::crypto::MemoryKey;
use edgelet_core::network::MobyNetwork;
use edgelet_core::watchdog::RetryLimit;
use edgelet_core::ModuleSpec;
use edgelet_utils::log_failure;

mod yaml_file_source;
use yaml_file_source::YamlFileSource;

/// This is the default connection string
pub const DEFAULT_CONNECTION_STRING: &str = "<ADD DEVICE CONNECTION STRING HERE>";

#[cfg(unix)]
const DEFAULTS: &str = include_str!("../config/unix/default.yaml");

#[cfg(windows)]
const DEFAULTS: &str = include_str!("../config/windows/default.yaml");

const DEVICEID_KEY: &str = "DeviceId";
const HOSTNAME_KEY: &str = "HostName";
const SHAREDACCESSKEY_KEY: &str = "SharedAccessKey";

const DEVICEID_REGEX: &str = r"^[A-Za-z0-9\-:.+%_#*?!(),=@;$']{1,128}$";
const HOSTNAME_REGEX: &str = r"^[a-zA-Z0-9_\-\.]+$";

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "lowercase")]
pub struct Manual {
    device_connection_string: String,
}

impl Manual {
    pub fn new(device_connection_string: String) -> Self {
        Manual {
            device_connection_string,
        }
    }

    pub fn device_connection_string(&self) -> &str {
        &self.device_connection_string
    }

    pub fn parse_device_connection_string(
        &self,
    ) -> Result<(MemoryKey, String, String), ParseManualDeviceConnectionStringError> {
        if self.device_connection_string.is_empty() {
            return Err(ParseManualDeviceConnectionStringError::Empty);
        }

        let mut key = None;
        let mut device_id = None;
        let mut hub = None;

        let parts: Vec<&str> = self.device_connection_string.split(';').collect();
        for p in parts {
            let s: Vec<&str> = p.split('=').collect();
            match s[0] {
                SHAREDACCESSKEY_KEY => key = Some(s[1].to_string()),
                DEVICEID_KEY => device_id = Some(s[1].to_string()),
                HOSTNAME_KEY => hub = Some(s[1].to_string()),
                _ => (), // Ignore extraneous component in the connection string
            }
        }

        let key = key.ok_or(
            ParseManualDeviceConnectionStringError::MissingRequiredParameter(SHAREDACCESSKEY_KEY),
        )?;
        if key.is_empty() {
            return Err(ParseManualDeviceConnectionStringError::MalformedParameter(
                SHAREDACCESSKEY_KEY,
            ));
        }
        let key = MemoryKey::new(base64::decode(&key).map_err(|_| {
            ParseManualDeviceConnectionStringError::MalformedParameter(SHAREDACCESSKEY_KEY)
        })?);

        let device_id = device_id.ok_or(
            ParseManualDeviceConnectionStringError::MissingRequiredParameter(DEVICEID_KEY),
        )?;
        let device_id_regex =
            Regex::new(DEVICEID_REGEX).expect("This hard-coded regex is expected to be valid.");
        if !device_id_regex.is_match(&device_id) {
            return Err(ParseManualDeviceConnectionStringError::MalformedParameter(
                DEVICEID_KEY,
            ));
        }

        let hub = hub.ok_or(
            ParseManualDeviceConnectionStringError::MissingRequiredParameter(HOSTNAME_KEY),
        )?;
        let hub_regex =
            Regex::new(HOSTNAME_REGEX).expect("This hard-coded regex is expected to be valid.");
        if !hub_regex.is_match(&hub) {
            return Err(ParseManualDeviceConnectionStringError::MalformedParameter(
                HOSTNAME_KEY,
            ));
        }

        Ok((key, device_id.to_owned(), hub.to_owned()))
    }
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(tag = "method")]
#[serde(rename_all = "lowercase")]
pub enum AttestationMethod {
    Tpm(TpmAttestationInfo),
    #[serde(rename = "symmetric_key")]
    SymmetricKey(SymmetricKeyAttestationInfo),
    X509(X509AttestationInfo),
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "lowercase")]
pub struct TpmAttestationInfo {
    registration_id: String,
}

impl TpmAttestationInfo {
    pub fn new(registration_id: String) -> Self {
        TpmAttestationInfo { registration_id }
    }

    pub fn registration_id(&self) -> &str {
        &self.registration_id
    }
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "lowercase")]
pub struct SymmetricKeyAttestationInfo {
    registration_id: String,
    symmetric_key: String,
}

impl SymmetricKeyAttestationInfo {
    pub fn registration_id(&self) -> &str {
        &self.registration_id
    }

    pub fn symmetric_key(&self) -> &str {
        &self.symmetric_key
    }
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "lowercase")]
pub struct X509AttestationInfo {
    #[serde(skip_serializing_if = "Option::is_none")]
    registration_id: Option<String>,
    identity_cert: PathBuf,
    identity_pk: PathBuf,
}

impl X509AttestationInfo {
    pub fn identity_cert(&self) -> &Path {
        self.identity_cert.as_path()
    }

    pub fn identity_pk(&self) -> &Path {
        self.identity_pk.as_path()
    }

    pub fn registration_id(&self) -> Option<&str> {
        self.registration_id.as_ref().map(AsRef::as_ref)
    }
}

#[derive(Debug, Serialize)]
pub struct Dps {
    #[serde(with = "url_serde")]
    global_endpoint: Url,
    scope_id: String,
    attestation: AttestationMethod,
}

impl<'de> serde::Deserialize<'de> for Dps {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        #[derive(Debug, serde_derive::Deserialize)]
        struct Inner {
            #[serde(with = "url_serde")]
            global_endpoint: Url,
            scope_id: String,
            registration_id: Option<String>,
            #[serde(skip_serializing_if = "Option::is_none")]
            attestation: Option<AttestationMethod>,
        }

        let value: Inner = serde::Deserialize::deserialize(deserializer)?;

        let attestation = match (value.attestation, value.registration_id) {
            (Some(_att), Some(_)) => {
                return Err(serde::de::Error::custom(
                    "Provisioning registration_id has to be set only in attestation",
                ));
            }
            (Some(att), None) => att,
            (None, Some(reg_id)) => {
                AttestationMethod::Tpm(TpmAttestationInfo::new(reg_id.to_string()))
            }
            (None, None) => {
                return Err(serde::de::Error::custom(
                    "Provisioning registration_id has to be set",
                ));
            }
        };

        Ok(Dps {
            global_endpoint: value.global_endpoint,
            scope_id: value.scope_id,
            attestation,
        })
    }
}

impl Dps {
    pub fn global_endpoint(&self) -> &Url {
        &self.global_endpoint
    }

    pub fn scope_id(&self) -> &str {
        &self.scope_id
    }

    pub fn attestation(&self) -> &AttestationMethod {
        &self.attestation
    }
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "lowercase")]
pub struct External {
    #[serde(with = "url_serde")]
    endpoint: Url,
}

impl External {
    pub fn new(endpoint: Url) -> Self {
        External { endpoint }
    }

    pub fn endpoint(&self) -> &Url {
        &self.endpoint
    }
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(tag = "source")]
#[serde(rename_all = "lowercase")]
pub enum Provisioning {
    Manual(Manual),
    Dps(Dps),
    External(External),
}

#[derive(Debug, Deserialize, Serialize)]
pub struct Connect {
    #[serde(with = "url_serde")]
    workload_uri: Url,
    #[serde(with = "url_serde")]
    management_uri: Url,
}

impl Connect {
    pub fn workload_uri(&self) -> &Url {
        &self.workload_uri
    }

    pub fn management_uri(&self) -> &Url {
        &self.management_uri
    }
}

#[derive(Debug, Deserialize, Serialize)]
pub struct Listen {
    #[serde(with = "url_serde")]
    workload_uri: Url,
    #[serde(with = "url_serde")]
    management_uri: Url,
}

impl Listen {
    pub fn workload_uri(&self) -> &Url {
        &self.workload_uri
    }

    pub fn management_uri(&self) -> &Url {
        &self.management_uri
    }
}

#[derive(Debug, Deserialize, Serialize)]
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

#[derive(Debug, Deserialize, Serialize)]
pub struct Certificates {
    device_ca_cert: PathBuf,
    device_ca_pk: PathBuf,
    trusted_ca_certs: PathBuf,
}

impl Certificates {
    pub fn device_ca_cert(&self) -> &Path {
        &self.device_ca_cert
    }

    pub fn device_ca_pk(&self) -> &Path {
        &self.device_ca_pk
    }

    pub fn trusted_ca_certs(&self) -> &Path {
        &self.trusted_ca_certs
    }
}

#[derive(Debug, Default, Deserialize, Serialize)]
pub struct WatchdogSettings {
    #[serde(default)]
    max_retries: RetryLimit,
}

impl WatchdogSettings {
    pub fn max_retries(&self) -> &RetryLimit {
        &self.max_retries
    }
}

#[derive(Debug, Deserialize, Serialize)]
pub struct Settings<T> {
    provisioning: Provisioning,
    agent: ModuleSpec<T>,
    hostname: String,
    connect: Connect,
    listen: Listen,
    homedir: PathBuf,
    moby_runtime: MobyRuntime,
    certificates: Option<Certificates>,
    #[serde(default)]
    watchdog: WatchdogSettings,
}

impl<T> Settings<T>
where
    T: DeserializeOwned + Serialize,
{
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

        let settings: Self = config.try_into()?;

        Ok(settings)
    }

    pub fn provisioning(&self) -> &Provisioning {
        &self.provisioning
    }

    pub fn agent(&self) -> &ModuleSpec<T> {
        &self.agent
    }

    pub fn agent_mut(&mut self) -> &mut ModuleSpec<T> {
        &mut self.agent
    }

    pub fn hostname(&self) -> &str {
        &self.hostname
    }

    pub fn connect(&self) -> &Connect {
        &self.connect
    }

    pub fn listen(&self) -> &Listen {
        &self.listen
    }

    pub fn homedir(&self) -> &Path {
        &self.homedir
    }

    pub fn moby_runtime(&self) -> &MobyRuntime {
        &self.moby_runtime
    }

    pub fn certificates(&self) -> Option<&Certificates> {
        self.certificates.as_ref()
    }

    pub fn watchdog(&self) -> &WatchdogSettings {
        &self.watchdog
    }

    pub fn diff_with_cached(&self, path: &Path) -> bool {
        fn diff_with_cached_inner<T>(
            cached_settings: &Settings<T>,
            path: &Path,
        ) -> Result<bool, LoadSettingsError>
        where
            T: DeserializeOwned + Serialize,
        {
            let mut file = OpenOptions::new().read(true).open(path)?;
            let mut buffer = String::new();
            file.read_to_string(&mut buffer)?;
            let s = serde_json::to_string(cached_settings)?;
            let s = Sha256::digest_str(&s);
            let encoded = base64::encode(&s);
            if encoded == buffer {
                debug!("Config state matches supplied config.");
                Ok(false)
            } else {
                Ok(true)
            }
        }

        match diff_with_cached_inner(self, path) {
            Ok(result) => result,

            Err(err) => {
                log_failure(Level::Debug, &err);
                debug!("Error reading config backup.");
                true
            }
        }
    }
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

#[derive(Clone, Copy, Debug, Fail)]
pub enum ParseManualDeviceConnectionStringError {
    #[fail(
        display = "The Connection String is empty. Please update the config.yaml and provide the IoTHub connection information."
    )]
    Empty,

    #[fail(display = "The Connection String is missing required parameter {}", _0)]
    MissingRequiredParameter(&'static str),

    #[fail(
        display = "The Connection String has a malformed value for parameter {}.",
        _0
    )]
    MalformedParameter(&'static str),
}

#[cfg(test)]
mod tests {
    use super::*;
    use config::{Config, File, FileFormat};
    use edgelet_core::{Ipam, DEFAULT_NETWORKID};
    use edgelet_docker::DockerConfig;
    use std::cmp::Ordering;
    use std::fs::File as FsFile;
    use std::io::Write;
    use tempdir::TempDir;

    #[cfg(unix)]
    static GOOD_SETTINGS: &str = "test/linux/sample_settings.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS1: &str = "test/linux/sample_settings1.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS2: &str = "test/linux/sample_settings2.yaml";
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
    static GOOD_SETTINGS_DPS_TPM1: &str = "test/linux/sample_settings.dps.tpm.1.yaml";
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
    static GOOD_SETTINGS1: &str = "test/windows/sample_settings1.yaml";
    #[cfg(windows)]
    static GOOD_SETTINGS2: &str = "test/windows/sample_settings2.yaml";
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
    static GOOD_SETTINGS_DPS_TPM1: &str = "test/windows/sample_settings.dps.tpm.1.yaml";
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
        let settings: Settings<DockerConfig> = config.try_into().unwrap();

        match settings.provisioning() {
            Provisioning::Manual(ref manual) => {
                assert_eq!(manual.device_connection_string(), DEFAULT_CONNECTION_STRING)
            }
            _ => unreachable!(),
        }
    }

    #[test]
    fn no_file_gets_error() {
        let settings = Settings::<DockerConfig>::new(Some(Path::new("garbage")));
        assert!(settings.is_err());
    }

    #[test]
    fn bad_file_gets_error() {
        let settings = Settings::<DockerConfig>::new(Some(Path::new(BAD_SETTINGS)));
        assert!(settings.is_err());

        let settings = Settings::<DockerConfig>::new(Some(Path::new(BAD_SETTINGS_DPS_DEFAULT)));
        assert!(settings.is_err());

        let settings = Settings::<DockerConfig>::new(Some(Path::new(BAD_SETTINGS_DPS_TPM)));
        assert!(settings.is_err());

        let settings = Settings::<DockerConfig>::new(Some(Path::new(BAD_SETTINGS_DPS_SYM_KEY)));
        assert!(settings.is_err());

        let settings = Settings::<DockerConfig>::new(Some(Path::new(BAD_SETTINGS_DPS_X5091)));
        assert!(settings.is_err());

        let settings = Settings::<DockerConfig>::new(Some(Path::new(BAD_SETTINGS_DPS_X5092)));
        assert!(settings.is_err());
    }

    #[test]
    fn manual_file_gets_sample_connection_string() {
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS)));
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
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_TG)));
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
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_DPS_DEFAULT)));
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
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_DPS_TPM)));
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
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_DPS_SYM_KEY)));
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
        let settings = Settings::<DockerConfig>::new(Some(Path::new(X509_GOOD_SETTINGS1)));
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
        let settings = Settings::<DockerConfig>::new(Some(Path::new(X509_GOOD_SETTINGS2)));
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
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_EXTERNAL)));
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
    fn network_get_settings() {
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_NETWORK)));
        println!("{:?}", settings);
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
                assert_eq!(moby_network.ipv6().unwrap(), &true);
                if let Some(ipam_config) = moby_network.ipam() {
                    let ipam_1 = Ipam::default()
                        .with_gateway("172.18.0.1".to_string())
                        .with_ip_range("172.18.0.0/16".to_string())
                        .with_subnet("172.18.0.0/16".to_string());
                    let ipam_2 = Ipam::default()
                        .with_gateway("2001:4898:e0:3b1:1::1".to_string())
                        .with_ip_range("2001:4898:e0:3b1:1::/80".to_string())
                        .with_subnet("2001:4898:e0:3b1:1::/80".to_string());
                    let expected_ipam_config: Vec<Ipam> = vec![ipam_1, ipam_2];

                    ipam_config.iter().for_each(|ipam_config| {
                        assert_eq!(expected_ipam_config.contains(ipam_config), true);
                    });
                } else {
                    panic!("Expected IPAM configuration.")
                }
            }
            MobyNetwork::Name(_name) => panic!("Unexpected network configuration."),
        };
    }

    #[test]
    fn diff_with_same_cached_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        let settings_to_write = serde_json::to_string(&settings).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        FsFile::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        assert_eq!(settings.diff_with_cached(&path), false);
    }

    #[test]
    fn diff_with_tpm_default_and_explicit_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 =
            Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_DPS_DEFAULT))).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        FsFile::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings =
            Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_DPS_TPM1))).unwrap();
        assert_eq!(settings.diff_with_cached(&path), false);
    }

    #[test]
    fn diff_with_tpm_explicit_and_default_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 =
            Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_DPS_TPM1))).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        FsFile::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings =
            Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_DPS_DEFAULT))).unwrap();
        assert_eq!(settings.diff_with_cached(&path), false);
    }

    #[test]
    fn diff_with_same_cached_env_var_unordered_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS2))).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        FsFile::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        assert_eq!(settings.diff_with_cached(&path), false);
    }

    #[test]
    fn diff_with_different_cached_returns_true() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS1))).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        FsFile::create(&path)
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        assert_eq!(settings.diff_with_cached(&path), true);
    }

    #[test]
    fn diff_with_no_file_returns_true() {
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS))).unwrap();
        assert_eq!(settings.diff_with_cached(Path::new("i dont exist")), true);
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
    fn case_of_names_of_keys_is_preserved() {
        let settings =
            Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS_CASE_SENSITIVE))).unwrap();

        let env = settings.agent().env();
        assert_eq!(env.get("AbC").map(AsRef::as_ref), Some("VAluE1"));
        assert_eq!(env.get("DeF").map(AsRef::as_ref), Some("VAluE2"));

        let create_options = settings.agent().config().create_options();
        assert_eq!(create_options.hostname(), Some("VAluE3"));
    }

    #[test]
    fn watchdog_settings_are_read() {
        let settings = Settings::<DockerConfig>::new(Some(Path::new(GOOD_SETTINGS)));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        let watchdog_settings = s.watchdog();
        assert_eq!(watchdog_settings.max_retries().compare(3), Ordering::Equal);
    }
}
