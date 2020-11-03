use crate::check::{checker::Checker, Check, CheckResult};
use edgelet_core::RuntimeSettings;
use failure::ResultExt;

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct PullAgentFromUpstream {}

impl Checker for PullAgentFromUpstream {
    fn id(&self) -> &'static str {
        "Pull-Agent-from-upstream"
    }
    fn description(&self) -> &'static str {
        "EdgeAgent module can be pulled from upstream"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl PullAgentFromUpstream {
    #[allow(clippy::unused_self)]
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
            docker_host_arg
        } else {
            return Ok(CheckResult::Skipped);
        };

        if let (Some(username), Some(password), Some(server_address)) = (
            &settings
                .agent()
                .config()
                .auth()
                .and_then(docker::models::AuthConfig::username),
            &settings
                .agent()
                .config()
                .auth()
                .and_then(docker::models::AuthConfig::password),
            &settings
                .agent()
                .config()
                .auth()
                .and_then(docker::models::AuthConfig::serveraddress),
        ) {
            super::docker(
                docker_host_arg,
                vec![
                    "login",
                    server_address,
                    "-p",
                    password,
                    "--username",
                    username,
                ],
            )
            .map_err(|(_, err)| err)
            .context(format!("Failed to login to {}", server_address))?;
        }

        super::docker(
            docker_host_arg,
            vec!["pull", &settings.agent().config().image()],
        )
        .map_err(|(_, err)| err)
        .context("Failed to get edge Agent image")?;

        Ok(CheckResult::Ok)
    }
}
