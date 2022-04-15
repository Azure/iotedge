use crate::check::{Check, CheckResult, Checker, CheckerMeta};

use anyhow::anyhow;

#[derive(Default, serde::Serialize)]
pub(crate) struct ContainerEngineInstalled {
    docker_host_arg: Option<String>,
    docker_server_version: Option<String>,
}

#[async_trait::async_trait]
impl Checker for ContainerEngineInstalled {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "container-engine-uri",
            description: "container engine is installed and functional",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ContainerEngineInstalled {
    async fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        let uri = settings.moby_runtime().uri();

        let docker_host_arg = match uri.scheme() {
            "unix" => uri.to_string(),

            "npipe" => {
                let mut uri = uri.to_string();
                uri.replace_range(0.."npipe://".len(), "npipe:////");
                uri
            }

            scheme => {
                return Err(anyhow!(
                    "Could not communicate with container engine at {}. The scheme {} is invalid.",
                    uri,
                    scheme,
                ));
            }
        };

        let output = super::docker(
            &docker_host_arg,
            &["version", "--format", "{{.Server.Version}}"],
        )
        .await;
        let output = match output {
            Ok(output) => output,
            Err((message, err)) => {
                let mut error_message = format!(
                    "Could not communicate with container engine at {}.\n\
                     Please check your moby-engine installation and ensure the service is running.",
                    uri,
                );

                if let Some(message) = message {
                    if message.contains("Got permission denied") {
                        error_message += "\nYou might need to run this command as root.";
                        return Ok(CheckResult::Fatal(err.context(error_message)));
                    }
                }

                return Err(err.context(error_message));
            }
        };

        check.docker_host_arg = Some(docker_host_arg);

        check.docker_server_version = Some(String::from_utf8_lossy(&output).trim().to_owned());
        check.additional_info.docker_version = check.docker_server_version.clone();

        self.docker_host_arg = check.docker_host_arg.clone();
        self.docker_server_version = check.docker_server_version.clone();

        Ok(CheckResult::Ok)
    }
}
