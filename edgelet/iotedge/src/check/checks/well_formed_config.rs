use std::fs::File;

use edgelet_docker::{Settings, CONFIG_FILE_DEFAULT, UPSTREAM_PARENT_KEYWORD};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct WellFormedConfig {}

impl Checker for WellFormedConfig {
    fn id(&self) -> &'static str {
        "aziot-edged-config-well-formed"
    }
    fn description(&self) -> &'static str {
        "aziot-edged configuration is well-formed"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        Self::inner_execute(check).unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl WellFormedConfig {
    fn inner_execute(check: &mut Check) -> anyhow::Result<CheckResult> {
        // The config crate just returns a "file not found" error when it can't open the file for any reason,
        // even if the real error was a permissions issue.
        //
        // So we first try to open the file for reading ourselves.
        //
        // It's okay if the file doesn't exist.
        if let Err(err) = File::open(CONFIG_FILE_DEFAULT) {
            if err.kind() == std::io::ErrorKind::PermissionDenied {
                return Ok(CheckResult::Fatal(
                    anyhow::anyhow!(err).context("Could not open IoT Edge configuration. You might need to run this command as root."),
                ));
            } else if err.kind() != std::io::ErrorKind::NotFound {
                return Err(anyhow::anyhow!(err)
                    .context(format!("Could not open file {}", CONFIG_FILE_DEFAULT)));
            }
        }

        let settings = match Settings::new() {
            Ok(settings) => settings,
            Err(err) => {
                let message = if check.verbose {
                    "\
                        The IoT Edge configuration is not well-formed.\n\
                        Note: In case of syntax errors, the error may not be exactly at the reported line number and position.\
                    "
                } else {
                    "The IoT Edge daemon's configuration file is not well-formed."
                };
                return Err(err.context(message).into());
            }
        };

        if let Some(parent_hostname) = check.parent_hostname.as_ref() {
            if let Some(image_tail) = check
                .diagnostics_image_name
                .strip_prefix(UPSTREAM_PARENT_KEYWORD)
            {
                check.diagnostics_image_name = format!("{}{}", parent_hostname, image_tail);
            }
        }

        check.settings = Some(settings);

        Ok(CheckResult::Ok)
    }
}
