// Copyright (c) Microsoft. All rights reserved.

use config::{Config, File};
use serde;
use serde_json;

use error::Error;

#[derive(Debug, Deserialize)]
struct DeviceConnectionString {
    device_connection_string: String,
}

#[derive(Debug, Deserialize)]
struct Provisioning {
    source: String,
    manual: DeviceConnectionString,
}

#[derive(Debug, Deserialize)]
pub struct Settings {
    provisioning: Provisioning,
}

static DEFAULTS: &'static str = r#"{
    "provisioning": {
      "source": "manual",
      "manual": {
        "device_connection_string": "cs"
      }
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

    pub fn _device_connection_string(&self) -> &str {
        &self.provisioning.manual.device_connection_string
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn gets_default_connection_string() {
        let settings = Settings::new(None);
        assert_eq!(settings.is_ok(), true);
        assert_eq!(
            settings
                .unwrap()
                .provisioning
                .manual
                .device_connection_string,
            "cs"
        );
    }

    #[test]
    fn gets_default_provisioning_mode() {
        let settings = Settings::new(None);
        assert_eq!(settings.is_ok(), true);
        assert_eq!(settings.unwrap().provisioning.source, "manual");
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
    fn file_gets_sample_connection_string() {
        let settings = Settings::new(Some("test/sample_settings.json"));
        assert_eq!(settings.is_ok(), true);
        assert_eq!(
            settings
                .unwrap()
                .provisioning
                .manual
                .device_connection_string,
            "sample"
        );
    }
}
