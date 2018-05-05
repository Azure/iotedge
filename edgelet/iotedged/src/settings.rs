// Copyright (c) Microsoft. All rights reserved.

use config::{Config, File};
use serde_json;
use serde::de::DeserializeOwned;

use edgelet_core::ModuleSpec;
use error::Error;

#[derive(Debug, Deserialize)]
#[serde(tag = "source")]
#[serde(rename_all = "lowercase")]
pub enum Provisioning {
    Manual {
        device_connection_string: String,
    },
    Dps {
        global_endpoint: String,
        scope_id: String,
    },
}

#[derive(Debug, Deserialize)]
pub struct Settings<T> {
    provisioning: Provisioning,
    runtime: ModuleSpec<T>,
    hostname: String,
    workload_port: u16,
    management_port: u16,
    docker_uri: String,
}

static DEFAULTS: &str = r#"{
    "provisioning": {
      "source": "manual",
      "device_connection_string": "HostName=something.some.com;DeviceId=some;SharedAccessKey=some"
    },    
    "runtime": {
      "name": "edgeAgent",
      "type": "docker",
      "env": {},
      "config": {
        "image": "microsoft/azureiotedge-agent:1.0-preview",
        "create_options": "",
        "auth": {}
      }
    },
    "hostname": "localhost",
    "workload_port": 8081,
    "management_port": 8080,
    "docker_uri": "http://localhost:2375"
}"#;

impl<T> Settings<T>
where
    T: DeserializeOwned,
{
    pub fn new(filename: Option<&str>) -> Result<Self, Error> {
        filename
            .map(|val| {
                let mut settings = Config::default();
                settings.merge(File::with_name(val))?;
                settings.try_into().map_err(Error::from)
            })
            .unwrap_or_else(|| {
                Ok(serde_json::from_str::<Settings<T>>(DEFAULTS)
                    .expect("Invalid default configuration"))
            })
    }

    pub fn provisioning(&self) -> &Provisioning {
        &self.provisioning
    }

    pub fn runtime(&self) -> &ModuleSpec<T> {
        &self.runtime
    }

    pub fn hostname(&self) -> &str {
        &self.hostname
    }

    pub fn workload_port(&self) -> &u16 {
        &self.workload_port
    }

    pub fn management_port(&self) -> &u16 {
        &self.management_port
    }

    pub fn docker_uri(&self) -> &str {
        &self.docker_uri
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use edgelet_docker::DockerConfig;

    fn unwrap_manual_provisioning(p: &Provisioning) -> Result<String, Error> {
        match p {
            &Provisioning::Manual {
                ref device_connection_string,
            } => Ok(device_connection_string.to_string()),
            &Provisioning::Dps {
                global_endpoint: _,
                scope_id: _,
            } => Ok("not implemented".to_string()),
        }
    }

    #[test]
    fn manual_gets_default_connection_string() {
        let settings = Settings::<DockerConfig>::new(None);
        assert_eq!(settings.is_ok(), true);
        let s = settings.unwrap();
        let p = s.provisioning();
        let connection_string = unwrap_manual_provisioning(p);
        assert_eq!(connection_string.is_ok(), true);
        assert_eq!(
            connection_string.expect("unexpected"),
            "HostName=something.some.com;DeviceId=some;SharedAccessKey=some"
        );
    }

    #[test]
    fn no_file_gets_error() {
        let settings = Settings::<DockerConfig>::new(Some("garbage"));
        assert_eq!(settings.is_err(), true);
    }

    #[test]
    fn bad_file_gets_error() {
        let settings = Settings::<DockerConfig>::new(Some("test/bad_sample_settings.json"));
        assert_eq!(settings.is_err(), true);
    }

    #[test]
    fn manual_file_gets_sample_connection_string() {
        let settings = Settings::<DockerConfig>::new(Some("test/sample_settings.json"));
        assert_eq!(settings.is_ok(), true);
        let s = settings.unwrap();
        let p = s.provisioning();
        let connection_string = unwrap_manual_provisioning(p);
        assert_eq!(connection_string.is_ok(), true);
        assert_eq!(
            connection_string.expect("unexpected"),
            "HostName=something.something.com;DeviceId=something;SharedAccessKey=something"
        );
    }
}
