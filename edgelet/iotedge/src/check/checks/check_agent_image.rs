use anyhow::Context;
use edgelet_core::RuntimeSettings;
use regex::Regex;

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct CheckAgentImage {}

impl Checker for CheckAgentImage {
    fn id(&self) -> &'static str {
        "check-agent-image"
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
    fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        let settings = if let Some(settings) = &mut check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        let parent_hostname: String;
        let upstream_hostname = if let Some(upstream_hostname) = check.parent_hostname.as_ref() {
            parent_hostname = upstream_hostname.to_string();
            &parent_hostname
        } else if let Some(iothub_hostname) = &check.iothub_hostname {
            iothub_hostname
        } else {
            return Ok(CheckResult::Skipped);
        };

        let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
            docker_host_arg
        } else {
            return Ok(CheckResult::Skipped);
        };

        settings
            .agent_mut()
            .parent_hostname_resolve(upstream_hostname);

        let agent_image = settings.agent().config().image().to_string();

        if check.parent_hostname.is_some() {
            match check_agent_image_version_nested(&agent_image) {
                CheckResult::Ok => (),
                other => return Ok(other),
            }
        }

        let server_address = settings
            .agent()
            .config()
            .auth()
            .and_then(docker::models::AuthConfig::serveraddress);

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
            &server_address,
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

fn check_agent_image_version_nested(agent_image: &str) -> CheckResult {
    // We don't match the repo mcr.microsoft.com because in nested edge we expect the repo to be $upstream:443
    //
    // If the image spec doesn't match what we expected, it's a custom image, and we can't make
    // any determination of whether it's the right version or not. In that case we assume it is right.

    let re = Regex::new(r".*?/azureiotedge-agent:(?P<Major>\d+)\.(?P<Minor>\d+).*")
        .expect("hard-coded regex cannot fail to parse");

    if let Some(caps) = re.captures(&agent_image) {
        let major = caps
            .name("Major")
            .and_then(|version| version.as_str().parse::<u32>().ok());
        let minor = caps
            .name("Minor")
            .and_then(|version| version.as_str().parse::<u32>().ok());

        if let (Some(major), Some(minor)) = (major, minor) {
            if major < 1 || (major == 1) && (minor < 2) {
                return CheckResult::Failed(
                    anyhow::anyhow!("In nested Edge, edgeAgent version need to be 1.2 or above")
                );
            }
        }
    }

    CheckResult::Ok
}

#[cfg(test)]
mod tests {
    use super::check_agent_image_version_nested;
    use crate::check::CheckResult;

    #[test]
    fn test_check_agent_image_version_nested() {
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
            let actual_is_valid = matches!(
                check_agent_image_version_nested(agent_image),
                CheckResult::Ok
            );
            assert_eq!(*expected_is_valid, actual_is_valid);
        }
    }
}
