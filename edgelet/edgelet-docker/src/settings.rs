// Copyright (c) Microsoft. All rights reserved.

use std::fs;
use std::path::{Path, PathBuf};
use std::str::FromStr;

use cfg::{Config, Environment, File, FileFormat};
use failure::ResultExt;
use url::Url;
use url_serde;

use docker::models::HostConfig;
use edgelet_core::{Certificates, Connect, Listen, ModuleSpec, Provisioning, RuntimeSettings};
use edgelet_http::UrlExt;

use error::{Error, ErrorKind};

use super::DockerConfig;

/// This is the name of the network created by the iotedged
const DEFAULT_NETWORKID: &str = "azure-iot-edge";

/// This is the key for the docker network Id.
const EDGE_NETWORKID_KEY: &str = "NetworkId";

const UNIX_SCHEME: &str = "unix";

#[derive(Clone, Debug, Deserialize, Serialize)]
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

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Settings {
    provisioning: Provisioning,
    agent: ModuleSpec<DockerConfig>,
    hostname: String,
    connect: Connect,
    listen: Listen,
    homedir: PathBuf,
    certificates: Option<Certificates>,
    moby_runtime: MobyRuntime,
}

impl FromStr for Settings {
    type Err = Error;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        let mut config = Config::default();
        config
            .merge(File::from_str(s, FileFormat::Yaml).required(true))
            .and_then(|config| config.merge(Environment::with_prefix("iotedge")))
            .context(ErrorKind::Initialization)?;
        config
            .try_into()
            .context(ErrorKind::Initialization)
            .map_err(Error::from)
            .and_then(Settings::init_agent_spec)
    }
}

impl Settings {
    pub fn new<P>(path: P) -> Result<Self, Error>
    where
        P: AsRef<Path>,
    {
        FromStr::from_str(&fs::read_to_string(path).context(ErrorKind::SettingsFile)?)
    }

    pub fn moby_runtime(&self) -> &MobyRuntime {
        &self.moby_runtime
    }

    fn agent_vol_mount(&mut self) -> Result<(), Error> {
        let create_options = self.agent.config().clone_create_options()?;
        let host_config = create_options
            .host_config()
            .cloned()
            .unwrap_or_else(HostConfig::new);
        let mut binds = host_config.binds().map_or_else(Vec::new, ToOwned::to_owned);

        // if the url is a domain socket URL then vol mount it into the container
        for uri in &[
            self.connect().management_uri(),
            self.connect().workload_uri(),
        ] {
            if uri.scheme() == UNIX_SCHEME {
                let path = uri
                    .to_uds_file_path()
                    .context(ErrorKind::InvalidSocketUri)?;
                // On Windows we mount the parent folder because we can't mount the
                // socket files directly
                #[cfg(windows)]
                let path = path.parent().ok_or_else(|| ErrorKind::InvalidSocketUri)?;
                let path = path
                    .to_str()
                    .ok_or_else(|| ErrorKind::InvalidSocketUri)?
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

            self.agent.config_mut().set_create_options(create_options);
        }

        Ok(())
    }

    fn agent_env(&mut self) {
        let network_id = self.moby_runtime().network().to_string();
        self.agent
            .env_mut()
            .insert(EDGE_NETWORKID_KEY.to_string(), network_id);
    }

    fn init_agent_spec(mut self) -> Result<Settings, Error> {
        // setup vol mounts for workload/management sockets
        self.agent_vol_mount()?;

        // setup environment variables that are moby/docker specific
        self.agent_env();

        Ok(self)
    }
}

impl RuntimeSettings for Settings {
    type Config = DockerConfig;

    fn provisioning(&self) -> &Provisioning {
        &self.provisioning
    }

    fn agent(&self) -> &ModuleSpec<DockerConfig> {
        &self.agent
    }

    fn hostname(&self) -> &str {
        &self.hostname
    }

    fn connect(&self) -> &Connect {
        &self.connect
    }

    fn listen(&self) -> &Listen {
        &self.listen
    }

    fn homedir(&self) -> &Path {
        &self.homedir
    }

    fn certificates(&self) -> Option<&Certificates> {
        self.certificates.as_ref()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[cfg(unix)]
    static GOOD_SETTINGS: &str = "test/linux/sample_settings.yaml";
    #[cfg(unix)]
    static BAD_SETTINGS: &str = "test/linux/bad_sample_settings.yaml";
    #[cfg(unix)]
    static GOOD_SETTINGS_TG: &str = "test/linux/sample_settings.tg.yaml";

    #[cfg(windows)]
    static GOOD_SETTINGS: &str = "test/windows/sample_settings.yaml";
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
    fn no_file_gets_error() {
        let settings = Settings::new("garbage");
        assert!(settings.is_err());
    }

    #[test]
    fn bad_file_gets_error() {
        let settings = Settings::new(BAD_SETTINGS);
        assert!(settings.is_err());
    }

    #[test]
    fn manual_file_gets_sample_connection_string() {
        let settings = Settings::new(GOOD_SETTINGS);
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
        let settings = Settings::new(GOOD_SETTINGS_TG);
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
