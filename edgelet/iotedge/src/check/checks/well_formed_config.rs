use std;
use std::fs::File;

use failure::{self, Fail};

use edgelet_docker::Settings;

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct WellFormedConfig {}

impl Checker for WellFormedConfig {
    fn id(&self) -> &'static str {
        "config-yaml-well-formed"
    }
    fn description(&self) -> &'static str {
        "config.yaml is well-formed"
    }
    fn execute(&mut self, check: &mut Check) -> CheckResult {
        Self::inner_execute(check).unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl WellFormedConfig {
    fn inner_execute(check: &mut Check) -> Result<CheckResult, failure::Error> {
        let config_file = &check.config_file;

        // The config crate just returns a "file not found" error when it can't open the file for any reason,
        // even if the real error was a permissions issue.
        //
        // So we first try to open the file for reading ourselves.
        if let Err(err) = File::open(config_file) {
            if err.kind() == std::io::ErrorKind::PermissionDenied {
                return Ok(CheckResult::Fatal(
                    err.context(format!(
                        "Could not open file {}. You might need to run this command as {}.",
                        config_file.display(),
                        if cfg!(windows) {
                            "Administrator"
                        } else {
                            "root"
                        },
                    ))
                    .into(),
                ));
            } else {
                return Err(err
                    .context(format!("Could not open file {}", config_file.display()))
                    .into());
            }
        }

        let settings = match Settings::new(config_file) {
            Ok(settings) => settings,
            Err(err) => {
                let message = if check.verbose {
                    format!(
                    "The IoT Edge daemon's configuration file {} is not well-formed.\n\
                     Note: In case of syntax errors, the error may not be exactly at the reported line number and position.",
                    config_file.display(),
                )
                } else {
                    format!(
                        "The IoT Edge daemon's configuration file {} is not well-formed.",
                        config_file.display(),
                    )
                };
                return Err(err.context(message).into());
            }
        };

        check.settings = Some(settings);

        Ok(CheckResult::Ok)
    }
}
