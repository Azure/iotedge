use failure::{self, Context, Fail};

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ContainerEngineIsMoby {
    docker_server_version: Option<String>,
    moby_runtime_uri: Option<String>,
}

#[async_trait::async_trait]
impl Checker for ContainerEngineIsMoby {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "container-engine-is-moby",
            description: "production readiness: container engine",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
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

        // Older releases of Moby do not identify themselves in any unique way. Moby devs recommended assuming that anything less than version 10 is Moby,
        // since these old releases are v3.x and regular Docker is in the high 10s.
        //
        // Newer releases of Moby follow Docker CE versioning, but have a "+azure" suffix, eg "19.03.12+azure"
        //
        // Therefore Docker CE is anything with major version >= 10 but without a "+azure" suffix.
        if docker_server_major_version >= 10 && !docker_server_version.ends_with("+azure") {
            return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
        }

        Ok(CheckResult::Ok)
    }
}
