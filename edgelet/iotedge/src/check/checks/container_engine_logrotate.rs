use std::fs::File;

use anyhow::{anyhow, Context};

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde::Serialize)]
pub(crate) struct ContainerEngineLogrotate {
    daemon_config: Option<DaemonConfig>,
}

#[async_trait::async_trait]
impl Checker for ContainerEngineLogrotate {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "container-engine-logrotate",
            description: "production readiness: logs policy",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ContainerEngineLogrotate {
    fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        const MESSAGE: &str =
        "Container engine is not configured to rotate module logs which may cause it run out of disk space.\n\
         Please see https://aka.ms/iotedge-prod-checklist-logs for best practices.\n\
         You can ignore this warning if you are setting log policy per module in the Edge deployment.";

        let daemon_config_file = File::open(&check.container_engine_config_path)
            .with_context(|| {
                format!(
                    "Could not open container engine config file {}",
                    check.container_engine_config_path.display(),
                )
            })
            .context(MESSAGE);
        let daemon_config_file = match daemon_config_file {
            Ok(daemon_config_file) => daemon_config_file,
            Err(err) => {
                return Ok(CheckResult::Warning(err));
            }
        };
        let daemon_config: DaemonConfig = serde_json::from_reader(daemon_config_file)
            .with_context(|| {
                format!(
                    "Could not parse container engine config file {}",
                    check.container_engine_config_path.display(),
                )
            })
            .context(MESSAGE)?;
        self.daemon_config = Some(daemon_config.clone());

        match daemon_config.log_driver.as_deref() {
            Some("journald") => return Ok(CheckResult::Ok),
            None => return Ok(CheckResult::Warning(anyhow!(MESSAGE))),
            _ => (),
        }

        if let Some(log_opts) = &daemon_config.log_opts {
            if log_opts.max_file.is_none() {
                return Ok(CheckResult::Warning(anyhow!(MESSAGE)));
            }

            if log_opts.max_size.is_none() {
                return Ok(CheckResult::Warning(anyhow!(MESSAGE)));
            }
        } else {
            return Ok(CheckResult::Warning(anyhow!(MESSAGE)));
        }

        Ok(CheckResult::Ok)
    }
}

#[derive(serde::Deserialize, serde::Serialize, Clone)]
struct DaemonConfig {
    #[serde(rename = "log-driver")]
    log_driver: Option<String>,

    #[serde(rename = "log-opts")]
    log_opts: Option<DaemonConfigLogOpts>,
}

#[derive(serde::Deserialize, serde::Serialize, Clone)]
struct DaemonConfigLogOpts {
    #[serde(rename = "max-file")]
    max_file: Option<String>,

    #[serde(rename = "max-size")]
    max_size: Option<String>,
}
