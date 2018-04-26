// Copyright (c) Microsoft. All rights reserved.

use config::{Config, File};
use serde;
use serde_json;

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
pub struct Settings {
    provisioning: Provisioning,
}

static DEFAULTS: &'static str = r#"{
    "provisioning": {
      "source": "manual",
      "device_connection_string": "HostName=something.some.com;DeviceId=some;SharedAccessKey=some"
    }
}"#;

impl Settings {
    pub fn new(filename: Option<&str>) -> Result<Self, Error> {
        let mut settings = Config::default();

        filename
            .map(|val| {
                settings
                    .merge(File::with_name(val))
                    .map_err(Error::from)
                    .and_then(|config| {
                        serde::Deserialize::deserialize(config.clone()).map_err(Error::from)
                    })
            })
            .unwrap_or_else(
                || {
                    Ok(serde_json::from_str::<Settings>(DEFAULTS)
                        .expect("Invalid default configuration"))
                },
            )
    }

    pub fn provisioning(&self) -> &Provisioning {
        &self.provisioning
    }
}

#[cfg(test)]
mod tests {
    use super::*;
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
        let settings = Settings::new(None);
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
        let settings = Settings::new(Some("garbage"));
        assert_eq!(settings.is_err(), true);
    }

    #[test]
    fn bad_file_gets_error() {
        let settings = Settings::new(Some("test/bad_sample_settings.json"));
        assert_eq!(settings.is_err(), true);
    }

    #[test]
    fn manual_file_gets_sample_connection_string() {
        let settings = Settings::new(Some("test/sample_settings.json"));
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
