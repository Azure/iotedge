use std::{net::IpAddr, str::FromStr};

use crate::check::{Check, CheckResult, Checker, CheckerMeta};
use edgelet_settings::RuntimeSettings;
use failure::ResultExt;

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ContainerResolveParentHostname {}

#[async_trait::async_trait]
impl Checker for ContainerResolveParentHostname {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "container-resolve-parent-hostname",
            description: "parent hostname is resolvable from inside container",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ContainerResolveParentHostname {
    #[allow(clippy::unused_self)]
    async fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        let parent_hostname = if let Some(hub_hostname) = check.parent_hostname.as_ref() {
            hub_hostname.to_string()
        } else {
            return Ok(CheckResult::Ignored);
        };

        //If parent hostname is an IP, we ignore
        if IpAddr::from_str(&parent_hostname).is_ok() {
            return Ok(CheckResult::Ignored);
        }

        let diagnostics_image_name = if check
            .diagnostics_image_name
            .starts_with("/azureiotedge-diagnostics:")
        {
            check.parent_hostname.as_ref().map_or_else(
                || "mcr.microsoft.com".to_string() + &check.diagnostics_image_name,
                |upstream_hostname| upstream_hostname.to_string() + &check.diagnostics_image_name,
            )
        } else {
            check.diagnostics_image_name.clone()
        };

        let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
            docker_host_arg
        } else {
            return Ok(CheckResult::Skipped);
        };

        let mut args = vec!["run".to_owned(), "--rm".to_owned()];

        settings
            .agent()
            .config()
            .create_options()
            .host_config()
            .and_then(docker::models::HostConfig::extra_hosts)
            .iter()
            .for_each(|extra_hosts| {
                extra_hosts
                    .iter()
                    .for_each(|host| args.push(format!("--add-host={}", host)))
            });

        args.extend(vec![
            diagnostics_image_name,
            "dotnet".to_owned(),
            "IotedgeDiagnosticsDotnet.dll".to_owned(),
            "parent-hostname".to_owned(),
            "--parent-hostname".to_owned(),
            parent_hostname.clone(),
        ]);

        super::docker(docker_host_arg, args)
            .await
            .map_err(|(_, err)| err)
            .context(format!(
                "Failed to resolve parent hostname {}",
                parent_hostname
            ))?;

        Ok(CheckResult::Ok)
    }
}
