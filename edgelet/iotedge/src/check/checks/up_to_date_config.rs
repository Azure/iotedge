use std::fs;
use std::path::Path;

use failure::{self, Context, Fail};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct UpToDateConfig {}

impl Checker for UpToDateConfig {
    fn id(&self) -> &'static str {
        "aziot-configs-up-to-date"
    }
    fn description(&self) -> &'static str {
        "aziot configurations up-to-date with config.toml"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        Self::inner_execute(check).unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl UpToDateConfig {
    fn inner_execute(_check: &mut Check) -> Result<CheckResult, failure::Error> {
        let config = Path::new("/etc/aziot/config.toml");

        if !config.exists() {
            return Ok(CheckResult::Ignored);
        }

        let config_metadata = match fs::metadata(config) {
            Ok(m) => m,
            Err(err) => {
                return Ok(CheckResult::Failed(
                    err.context(format!("Failed to query metadata of {}", config.display()))
                        .into(),
                ))
            }
        };

        let config_last_modified = config_metadata
            .modified()
            .expect("file metadata should contain valid last_modified");

        for service in &["keyd", "certd", "identityd", "tpmd", "edged"] {
            let service_config = format!("/etc/aziot/{}/config.d/00-super.toml", service);
            let service_config = Path::new(&service_config);

            if !service_config.exists() {
                return Ok(CheckResult::Warning(
                    Context::new(format!(
                        "{} does not exist.\n\
                        Did you run 'iotedge config apply'?",
                        service_config.display()
                    ))
                    .into(),
                ));
            }

            let service_config_metadata = match fs::metadata(service_config) {
                Ok(m) => m,
                Err(err) => {
                    return Ok(CheckResult::Failed(
                        err.context(format!(
                            "Failed to query metadata of {}",
                            service_config.display()
                        ))
                        .into(),
                    ))
                }
            };

            let service_config_last_modified = service_config_metadata
                .modified()
                .expect("file metadata should contain valid last_modified");

            if config_last_modified > service_config_last_modified {
                return Ok(CheckResult::Warning(
                    Context::new(format!(
                        "{} was modified after {}'s config\n\
                        You must run 'iotedge config apply' to update {}'s config with the latest config.toml",
                        config.display(),
                        service,
                        service
                    ))
                    .into(),
                ));
            }
        }

        Ok(CheckResult::Ok)
    }
}
