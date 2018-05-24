// Copyright (c) Microsoft. All rights reserved.

use config::{Config, Environment, File, FileFormat};
use serde::de::DeserializeOwned;
use url::Url;
use url_serde;

use edgelet_core::ModuleSpec;
use error::Error;

#[cfg(unix)]
static DEFAULTS: &str = include_str!("config/unix/default.yaml");

#[cfg(windows)]
static DEFAULTS: &str = include_str!("config/windows/default.yaml");

#[derive(Debug, Deserialize)]
#[serde(rename_all = "lowercase")]
pub struct Manual {
    device_connection_string: String,
}

impl Manual {
    pub fn device_connection_string(&self) -> &str {
        &self.device_connection_string
    }
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "lowercase")]
pub struct Dps {
    #[serde(with = "url_serde")]
    global_endpoint: Url,
    scope_id: String,
    registration_id: String,
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
}

#[derive(Debug, Deserialize)]
#[serde(tag = "source")]
#[serde(rename_all = "lowercase")]
pub enum Provisioning {
    Manual(Manual),
    Dps(Dps),
}

#[derive(Debug, Deserialize)]
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

#[derive(Debug, Deserialize)]
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

#[derive(Debug, Deserialize)]
pub struct Settings<T> {
    provisioning: Provisioning,
    agent: ModuleSpec<T>,
    hostname: String,
    connect: Connect,
    listen: Listen,
    #[serde(with = "url_serde")]
    docker_uri: Url,
}

impl<T> Settings<T>
where
    T: DeserializeOwned,
{
    pub fn new(filename: Option<&str>) -> Result<Self, Error> {
        let mut config = Config::default();
        config.merge(File::from_str(DEFAULTS, FileFormat::Yaml))?;

        if let Some(file) = filename {
            config.merge(File::with_name(file).required(true))?;
        }

        config.merge(Environment::with_prefix("IOTEDGE"))?;

        let settings = config.try_into()?;
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

    pub fn docker_uri(&self) -> &Url {
        &self.docker_uri
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use edgelet_docker::DockerConfig;

    fn unwrap_manual_provisioning(p: &Provisioning) -> String {
        match p {
            Provisioning::Manual(manual) => manual.device_connection_string().to_string(),
            _ => "not implemented".to_string(),
        }
    }

    #[test]
    fn manual_gets_default_connection_string() {
        let settings = Settings::<DockerConfig>::new(None);
        assert_eq!(settings.is_ok(), true);
        let s = settings.unwrap();
        let p = s.provisioning();
        let connection_string = unwrap_manual_provisioning(p);
        assert_eq!(
            connection_string,
            "HostName=something.some.com;DeviceId=some;SharedAccessKey=some"
        );
    }

    #[test]
    fn no_file_gets_error() {
        let settings = Settings::<DockerConfig>::new(Some("garbage"));
        assert!(settings.is_err());
    }

    #[test]
    fn bad_file_gets_error() {
        let settings = Settings::<DockerConfig>::new(Some("test/bad_sample_settings.yaml"));
        assert!(settings.is_err());
    }

    #[test]
    fn manual_file_gets_sample_connection_string() {
        let settings = Settings::<DockerConfig>::new(Some("test/sample_settings.yaml"));
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
}
