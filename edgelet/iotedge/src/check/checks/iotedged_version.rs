use std::process::Command;

use failure::{self, Context, Fail, ResultExt};
use regex::Regex;

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct IotedgedVersion {
    actual_version: Option<String>,
    expected_version: Option<String>,
}

impl Checker for IotedgedVersion {
    fn id(&self) -> &'static str {
        "iotedged-version"
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

impl IotedgedVersion {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let latest_versions = match &mut check.latest_versions {
            Ok(latest_versions) => &*latest_versions,
            Err(err) => match err.take() {
                Some(err) => return Ok(CheckResult::Warning(err.into())),
                None => return Ok(CheckResult::Skipped),
            },
        };
        self.expected_version = Some(latest_versions.iotedged.to_owned());

        let mut process = Command::new(&check.iotedged);
        process.arg("--version");

        if cfg!(windows) {
            process.env("IOTEDGE_RUN_AS_CONSOLE", "true");
        }

        let output = process
            .output()
            .context("Could not spawn iotedged process")?;
        if !output.status.success() {
            return Err(Context::new(format!(
                "iotedged returned {}, stderr = {}",
                output.status,
                String::from_utf8_lossy(&*output.stderr),
            ))
            .context("Could not spawn iotedged process")
            .into());
        }

        let output = String::from_utf8(output.stdout)
            .context("Could not parse output of iotedged --version")?;

        let iotedged_version_regex = Regex::new(r"^iotedged ([^ ]+)(?: \(.*\))?$")
            .expect("This hard-coded regex is expected to be valid.");
        let captures = iotedged_version_regex
            .captures(output.trim())
            .ok_or_else(|| {
                Context::new(format!(
                    "output {:?} does not match expected format",
                    output,
                ))
                .context("Could not parse output of iotedged --version")
            })?;
        let version = captures
            .get(1)
            .expect("unreachable: regex defines one capturing group")
            .as_str();
        self.actual_version = Some(version.to_owned());

        check.additional_info.iotedged_version = Some(version.to_owned());

        if version != latest_versions.iotedged {
            return Ok(CheckResult::Warning(
            Context::new(format!(
                "Installed IoT Edge daemon has version {} but {} is the latest stable version available.\n\
                 Please see https://aka.ms/iotedge-update-runtime for update instructions.",
                version, latest_versions.iotedged,
            ))
            .into(),
        ));
        }

        Ok(CheckResult::Ok)
    }
}
