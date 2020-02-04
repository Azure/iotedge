#[cfg(windows)]
use failure::{self, Context, ResultExt};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct WindowsHostVersion {
    moby_runtime_uri: Option<String>,
    #[cfg(windows)]
    os_version: Option<(
        winapi::shared::minwindef::DWORD,
        winapi::shared::minwindef::DWORD,
        winapi::shared::minwindef::DWORD,
        String,
    )>,
}

impl Checker for WindowsHostVersion {
    fn id(&self) -> &'static str {
        "windows-host-version"
    }
    fn description(&self) -> &'static str {
        "Windows host version is supported"
    }
    fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl WindowsHostVersion {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        #[cfg(unix)]
        {
            // We want to use the same as Windows' Checker::inner_execute() but the self is unused in Unix. So silence the allow_unused clippy lint.
            let _ = self;

            let _ = check;
            Ok(CheckResult::Ignored)
        }

        #[cfg(windows)]
        {
            let settings = if let Some(settings) = &check.settings {
                settings
            } else {
                return Ok(CheckResult::Skipped);
            };

            let moby_runtime_uri = settings.moby_runtime().uri().to_string();
            self.moby_runtime_uri = Some(moby_runtime_uri.clone());

            if moby_runtime_uri != "npipe://./pipe/iotedge_moby_engine" {
                // Host OS version restriction only applies when using Windows containers,
                // which in turn only happens when using Moby
                return Ok(CheckResult::Ignored);
            }

            let os_version =
                super::super::additional_info::os_version().context("Could not get OS version")?;
            self.os_version = Some(os_version.clone());
            match os_version {
            // When using Windows containers, the host OS version must match the container OS version.
            // Since our containers are built with 10.0.17763 base images, we require the same for the host OS.
            //
            // If this needs to be changed, also update the host OS version check in the Windows install script
            // (scripts/windows/setup/IotEdgeSecurityDaemon.ps1)
            (10, 0, 17763, _) => (),

            (major_version, minor_version, build_number, _) => {
                return Ok(CheckResult::Fatal(Context::new(format!(
                    "The host has an unsupported OS version {}.{}.{}. IoT Edge on Windows only supports OS version 10.0.17763.\n\
                    Please see https://aka.ms/iotedge-platsup for details.",
                    major_version, minor_version, build_number,
                )).into()))
            }
        }

            Ok(CheckResult::Ok)
        }
    }
}
