use failure::{self, Context, Fail};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ContainerEngineIsMoby {
    docker_server_version: Option<String>,
    moby_runtime_uri: Option<String>,
}

impl Checker for ContainerEngineIsMoby {
    fn id(&self) -> &'static str {
        "container-engine-is-moby"
    }
    fn description(&self) -> &'static str {
        "production readiness: container engine"
    }
    fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl ContainerEngineIsMoby {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        const MESSAGE: &str =
            "Device is not using a production-supported container engine (moby-engine).\n\
             Please see https://aka.ms/iotedge-prod-checklist-moby for details.";

        let docker_server_version =
            if let Some(docker_server_version) = &check.docker_server_version {
                self.docker_server_version = Some(docker_server_version.clone());
                docker_server_version
            } else {
                return Ok(CheckResult::Skipped);
            };

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
                return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
            }
        }

        let docker_server_major_version = docker_server_version
            .split('.')
            .next()
            .map(std::str::FromStr::from_str);
        let docker_server_major_version: u32 = match docker_server_major_version {
            Some(Ok(docker_server_major_version)) => docker_server_major_version,
            Some(Err(_)) | None => {
                return Ok(CheckResult::Warning(
                    Context::new(format!(
                        "Container engine returned malformed version string {:?}",
                        docker_server_version,
                    ))
                    .context(MESSAGE)
                    .into(),
                ));
            }
        };

        // Moby does not identify itself in any unique way. Moby devs recommend assuming that anything less than version 10 is Moby,
        // since it's currently 3.x and regular Docker is in the high 10s.
        if docker_server_major_version >= 10 {
            return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
        }

        Ok(CheckResult::Ok)
    }
}
