use crate::check::{checker::Checker, Check, CheckResult};
use edgelet_core::RuntimeSettings;
use failure::{Context, ResultExt};
use regex::Regex;

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct CheckAgentImage {}

impl Checker for CheckAgentImage {
    fn id(&self) -> &'static str {
        "Check-Agent-image"
    }
    fn description(&self) -> &'static str {
        "Agent image is valid and can be pulled from upstream"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl CheckAgentImage {
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

        let agent_image = settings.agent().config().image();

        if settings.parent_hostname().is_some() {
            match check_agent_image_version(&agent_image) {
                Ok(CheckResult::Ok) => (),
                error => return error,
            };
        }

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

        super::docker(docker_host_arg, vec!["pull", &agent_image])
            .map_err(|(_, err)| err)
            .context("Failed to get edge Agent image")?;

        Ok(CheckResult::Ok)
    }
}

fn check_agent_image_version(agent_image: &str) -> Result<CheckResult, failure::Error> {
    // We don't match the repo mcr.microsoft.com because in nested edge we expect the repo to be parent_hostname:443
    let re = Regex::new(r".*?/azureiotedge-agent:(?P<Major>\d+).(?P<Minor>\d+).*")
        .context("Failed to create regex")?;

    if let Some(caps) = re.captures(&agent_image) {
        let minor_version: i32 = caps
            .name("Minor")
            .expect("output does not match expected format")
            .as_str()
            .parse()
            .expect("output does not match expected format");
        let major_version: i32 = caps
            .name("Major")
            .expect("output does not match expected format")
            .as_str()
            .parse()
            .expect("output does not match expected format");

        if major_version < 1 {
            return Ok(CheckResult::Failed(
                Context::new("In nested edge, edgeAgent version need to be 1.2 or above").into(),
            ));
        }

        if (major_version == 1) && (minor_version < 2) {
            return Ok(CheckResult::Failed(
                Context::new("In nested edge, edgeAgent version need to be 1.2 or above").into(),
            ));
        }
    }

    Ok(CheckResult::Ok)
}

#[cfg(test)]
mod tests {
    use super::check_agent_image_version;
    use crate::check::CheckResult;

    #[test]
    fn test_check_agent_image_version() {
        let test_cases = &[
            (
                "mcr.microsoft.com/azureiotedge-agent:1.0.9.5-linux-amd64",
                false,
            ),
            (
                "$upstream:4443/azureiotedge-agent:1.0.9.5-linux-arm32v7",
                false,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.0.9.5-linux-arm64v8",
                false,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.0.9.5-windows-amd64",
                false,
            ),
            ("mcr.microsoft.com/azureiotedge-agent:1.1", false),
            ("mcr.microsoft.com/azureiotedge-agent:1.1.0", false),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.1.0-linux-amd64",
                false,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.1.0-linux-arm32v7",
                false,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.1.0-linux-arm64v8",
                false,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.1.0-windows-amd64",
                false,
            ),
            ("mcr.microsoft.com/azureiotedge-agent:1.2.0-rc1", true),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc1-linux-amd64",
                true,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc1-linux-arm32v7",
                true,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc1-linux-arm64v8",
                true,
            ),
            ("mcr.microsoft.com/azureiotedge-agent:1.2.0-rc2", true),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc2-linux-amd64",
                true,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc2-linux-arm32v7",
                true,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc2-linux-arm64v8",
                true,
            ),
            ("mcr.microsoft.com/azureiotedge-agent:1.2.0-rc3", true),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc3-linux-amd64",
                true,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc3-linux-arm32v7",
                true,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc3-linux-arm64v8",
                true,
            ),
            ("mcr.microsoft.com/azureiotedge-agent:1.2.0-rc4", true),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc4-linux-amd64",
                true,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc4-linux-arm32v7",
                true,
            ),
            (
                "mcr.microsoft.com/azureiotedge-agent:1.2.0-rc4-linux-arm64v8",
                true,
            ),
            (
                "$upstream:4443/azureiotedge-agent:3.0.9.5-linux-arm32v7",
                true,
            ),
            ("randomImage/randomImage:1.0", true),
        ];

        for (agent_image, expected_is_valid) in test_cases {
            let actual_is_valid =
                matches!(check_agent_image_version(agent_image), Ok(CheckResult::Ok));
            assert_eq!(*expected_is_valid, actual_is_valid);
        }
    }
}
