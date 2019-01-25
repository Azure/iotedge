// Copyright (c) Microsoft. All rights reserved.

use std::fs::{File as FsFile, OpenOptions};
use std::io::Read;
use std::path::{Path, PathBuf};

use base64;
use config::{Config, Environment, File, FileFormat};
use failure::{Fail, ResultExt};
use log::Level;
use serde::de::DeserializeOwned;
use serde::Serialize;
use serde_json;
use sha2::{Digest, Sha256};
use url::Url;
use url_serde;

use edgelet_core::ModuleSpec;
use edgelet_utils::log_failure;

use error::{Error, ErrorKind, InitializeErrorReason};

/// This is the name of the network created by the iotedged
const DEFAULT_NETWORKID: &str = "azure-iot-edge";

/// This is the default connection string
pub const DEFAULT_CONNECTION_STRING: &str = "<ADD DEVICE CONNECTION STRING HERE>";

#[cfg(unix)]
static DEFAULTS: &str = include_str!("config/unix/default.yaml");

#[cfg(windows)]
static DEFAULTS: &str = include_str!("config/windows/default.yaml");

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "lowercase")]
pub struct Manual {
    device_connection_string: String,
}

impl Manual {
    pub fn device_connection_string(&self) -> &str {
        &self.device_connection_string
    }
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "lowercase")]
pub struct Dps {
    #[serde(with = "url_serde")]
    global_endpoint: Url,
    scope_id: String,
    registration_id: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    symmetric_key: Option<String>,
}

impl Dps {
    pub fn global_endpoint(&self) -> &Url {
        &self.global_endpoint
    }

    pub fn scope_id(&self) -> &str {
        &self.scope_id
    }

    pub fn registration_id(&self) -> &str {
        &self.registration_id
    }

    pub fn symmetric_key(&self) ->  Option<&str> {
        self.symmetric_key.as_ref().map(AsRef::as_ref)
    }
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(tag = "source")]
#[serde(rename_all = "lowercase")]
pub enum Provisioning {
    Manual(Manual),
    Dps(Dps),
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
    network: String,
}

impl MobyRuntime {
    pub fn uri(&self) -> &Url {
        &self.uri
    }

    pub fn network(&self) -> &str {
        if self.network.is_empty() {
            &DEFAULT_NETWORKID
        } else {
            &self.network
        }
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
}

impl<T> Settings<T>
where
    T: DeserializeOwned + Serialize,
{
    pub fn new(filename: Option<&str>) -> Result<Self, Error> {
        let mut config = Config::default();
        config
            .merge(File::from_str(DEFAULTS, FileFormat::Yaml))
            .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;
        if let Some(file) = filename {
            config
                .merge(File::with_name(file).required(true))
                .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;
        }

        config
            .merge(Environment::with_prefix("iotedge"))
            .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;

        let settings: Self = config
            .try_into()
            .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;

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

    pub fn diff_with_cached(&self, path: PathBuf) -> Result<bool, Error> {
        OpenOptions::new()
            .read(true)
            .open(path)
            .map_err(|err| err.context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings)))
            .and_then(|mut file: FsFile| {
                let mut buffer = String::new();
                file.read_to_string(&mut buffer)
                    .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;
                let s = serde_json::to_string(self)
                    .context(ErrorKind::Initialize(InitializeErrorReason::LoadSettings))?;
                let s = Sha256::digest_str(&s);
                let encoded = base64::encode(&s);
                if encoded == buffer {
                    debug!("Config state matches supplied config.");
                    Ok(false)
                } else {
                    Ok(true)
                }
            })
            .or_else(|err| {
                log_failure(Level::Debug, &err);
                debug!("Error reading config backup.");
                Ok(true)
            })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use config::{Config, File, FileFormat};
    use edgelet_docker::DockerConfig;
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
            _ => assert!(false),
        }
    }

    #[test]
    fn no_file_gets_error() {
        let settings = Settings::<DockerConfig>::new(Some("garbage"));
        assert!(settings.is_err());
    }

    #[test]
    fn bad_file_gets_error() {
        let settings = Settings::<DockerConfig>::new(Some(BAD_SETTINGS));
        assert!(settings.is_err());
    }

    #[test]
    fn manual_file_gets_sample_connection_string() {
        let settings = Settings::<DockerConfig>::new(Some(GOOD_SETTINGS));
        println!("{:?}", settings);
        assert!(settings.is_ok());
        let s = settings.unwrap();
        let p = s.provisioning();
        let connection_string = unwrap_manual_provisioning(p);
        assert_eq!(
            connection_string,
            "HostName=something.something.com;DeviceId=something;SharedAccessKey=something"
        );
    }

    #[test]
    fn manual_file_gets_sample_tg_paths() {
        let settings = Settings::<DockerConfig>::new(Some(GOOD_SETTINGS_TG));
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
    fn diff_with_same_cached_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings = Settings::<DockerConfig>::new(Some(GOOD_SETTINGS)).unwrap();
        let settings_to_write = serde_json::to_string(&settings).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        FsFile::create(path.clone())
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        assert_eq!(settings.diff_with_cached(path).unwrap(), false);
    }

    #[test]
    fn diff_with_same_cached_env_var_unordered_returns_false() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::<DockerConfig>::new(Some(GOOD_SETTINGS2)).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        FsFile::create(path.clone())
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::<DockerConfig>::new(Some(GOOD_SETTINGS)).unwrap();
        assert_eq!(settings.diff_with_cached(path).unwrap(), false);
    }

    #[test]
    fn diff_with_different_cached_returns_true() {
        let tmp_dir = TempDir::new("blah").unwrap();
        let path = tmp_dir.path().join("cache");
        let settings1 = Settings::<DockerConfig>::new(Some(GOOD_SETTINGS1)).unwrap();
        let settings_to_write = serde_json::to_string(&settings1).unwrap();
        let sha_to_write = Sha256::digest_str(&settings_to_write);
        let base64_to_write = base64::encode(&sha_to_write);
        FsFile::create(path.clone())
            .unwrap()
            .write_all(base64_to_write.as_bytes())
            .unwrap();
        let settings = Settings::<DockerConfig>::new(Some(GOOD_SETTINGS)).unwrap();
        assert_eq!(settings.diff_with_cached(path).unwrap(), true);
    }

    #[test]
    fn diff_with_no_file_returns_true() {
        let settings = Settings::<DockerConfig>::new(Some(GOOD_SETTINGS)).unwrap();
        assert_eq!(
            settings
                .diff_with_cached(PathBuf::from("i dont exist"))
                .unwrap(),
            true
        );
    }

    #[test]
    fn network_default() {
        let moby1 = MobyRuntime {
            uri: Url::parse("http://test").unwrap(),
            network: "".to_string(),
        };
        assert_eq!(DEFAULT_NETWORKID, moby1.network());

        let moby2 = MobyRuntime {
            uri: Url::parse("http://test").unwrap(),
            network: "some-network".to_string(),
        };
        assert_eq!("some-network", moby2.network());
    }
}
