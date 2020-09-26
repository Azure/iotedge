use crate::check::{checker::Checker, Check, CheckResult};
use edgelet_core::RuntimeSettings;
use failure::ResultExt;

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ContainerResolveParentHostname {}

impl Checker for ContainerResolveParentHostname {
    fn id(&self) -> &'static str {
        "container-resolve-parent-hostname"
    }
    fn description(&self) -> &'static str {
        "config.yaml parent hostname is resolvable from inside container"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl ContainerResolveParentHostname {
    #[allow(clippy::unused_self)]
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        let diagnostics_image_name = if check
            .diagnostics_image_name
            .starts_with("/azureiotedge-diagnostics:")
        {
            if let Some(upstream_hostname) = settings.parent_hostname() {
                upstream_hostname.to_string() + &check.diagnostics_image_name
            } else {
                "mcr.microsoft.com".to_string() + &check.diagnostics_image_name
            }
        } else {
            return Ok(CheckResult::Skipped);
        };

        let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
            docker_host_arg
        } else {
            return Ok(CheckResult::Skipped);
        };

        let parent_hostname = if let Some(hub_hostname) = settings.parent_hostname() {
            hub_hostname.to_string()
        } else {
            return Ok(CheckResult::Skipped);
        };

        super::docker(
            docker_host_arg,
            vec![
                "run",
                "--rm",
                &diagnostics_image_name,
                "dotnet",
                "IotedgeDiagnosticsDotnet.dll",
                "parent-hostname",
                "--parent-hostname",
                &parent_hostname,
            ],
        )
        .map_err(|(_, err)| err)
        .context("Failed to resolve parent hostname")?;

        Ok(CheckResult::Ok)
    }
}
