use std::time::Duration;

use anyhow::Context;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ContainerLocalTime {
    expected_duration: Option<Duration>,
    actual_duration: Option<Duration>,
    diff: Option<u64>,
}

#[async_trait::async_trait]
impl Checker for ContainerLocalTime {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "container-local-time",
            description: "container time is close to host time",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ContainerLocalTime {
    async fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
            docker_host_arg
        } else {
            return Ok(CheckResult::Skipped);
        };

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

        let output = super::docker(
            docker_host_arg,
            vec![
                "run",
                "--rm",
                &diagnostics_image_name,
                "dotnet",
                "IotedgeDiagnosticsDotnet.dll",
                "local-time",
            ],
        )
        .await
        .map_err(|(_, err)| err)
        .context("Could not query local time inside container")?;

        let output = std::str::from_utf8(&output)
            .map_err(anyhow::Error::from)
            .and_then(|output| output.trim_end().parse::<u64>().map_err(Into::into))
            .context("Could not parse container output")?;

        let actual_duration = std::time::Duration::from_secs(output);
        self.actual_duration = Some(actual_duration);

        let expected_duration = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .context("Could not query local time of host")?;
        self.expected_duration = Some(expected_duration);

        let diff = std::cmp::max(actual_duration, expected_duration)
            - std::cmp::min(actual_duration, expected_duration);
        self.diff = Some(diff.as_secs());

        if diff.as_secs() >= 10 {
            return Err(anyhow::Error::msg(
                "Detected time drift between host and container",
            ));
        }

        Ok(CheckResult::Ok)
    }
}
