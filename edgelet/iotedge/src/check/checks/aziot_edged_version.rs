use std::process::Command;

use failure::{self, Context, Fail, ResultExt};
use regex::Regex;

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct AziotEdgedVersion {
    actual_version: Option<String>,
    expected_version: Option<String>,
}

impl Checker for AziotEdgedVersion {
    fn id(&self) -> &'static str {
        "aziot-edged-version"
    }
    fn description(&self) -> &'static str {
        "latest security daemon"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl AziotEdgedVersion {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let latest_versions = match &mut check.latest_versions {
            Ok(latest_versions) => &*latest_versions,
            Err(err) => match err.take() {
                Some(err) => return Ok(CheckResult::Warning(err.into())),
                None => return Ok(CheckResult::Skipped),
            },
        };
        self.expected_version = Some(latest_versions.aziot_edged.to_owned());

        let mut process = Command::new(&check.aziot_edged);
        process.arg("--version");

        let output = process
            .output()
            .context("Could not spawn aziot-edged process")?;
        if !output.status.success() {
            return Err(Context::new(format!(
                "aziot-edged returned {}, stderr = {}",
                output.status,
                String::from_utf8_lossy(&*output.stderr),
            ))
            .context("Could not spawn aziot-edged process")
            .into());
        }

        let output = String::from_utf8(output.stdout)
            .context("Could not parse output of aziot-edged --version")?;

        let aziot_edged_version_regex = Regex::new(r"^aziot-edged ([^ ]+)(?: \(.*\))?$")
            .expect("This hard-coded regex is expected to be valid.");
        let captures = aziot_edged_version_regex
            .captures(output.trim())
            .ok_or_else(|| {
                Context::new(format!(
                    "output {:?} does not match expected format",
                    output,
                ))
                .context("Could not parse output of aziot-edged --version")
            })?;
        let version = captures
            .get(1)
            .expect("unreachable: regex defines one capturing group")
            .as_str();
        self.actual_version = Some(version.to_owned());

        check.additional_info.aziot_edged_version = Some(version.to_owned());

        if version != latest_versions.aziot_edged {
            return Ok(CheckResult::Warning(
            Context::new(format!(
                "Installed IoT Edge daemon has version {} but {} is the latest stable version available.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                version, latest_versions.aziot_edged,
            ))
            .into(),
        ));
        }

        Ok(CheckResult::Ok)
    }
}
