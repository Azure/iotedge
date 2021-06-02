use std::fs::File;

use failure::{self, Context, ResultExt};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ContainerEngineLogrotate {
    daemon_config: Option<DaemonConfig>,
}

impl Checker for ContainerEngineLogrotate {
    fn id(&self) -> &'static str {
        "container-engine-logrotate"
    }
    fn description(&self) -> &'static str {
        "production readiness: logs policy"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl ContainerEngineLogrotate {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        const MESSAGE: &str =
        "Container engine is not configured to rotate module logs which may cause it run out of disk space.\n\
         Please see https://aka.ms/iotedge-prod-checklist-logs for best practices.\n\
         You can ignore this warning if you are setting log policy per module in the Edge deployment.";

        let daemon_config_file = File::open(&check.container_engine_config_path)
            .with_context(|_| {
                format!(
                    "Could not open container engine config file {}",
                    check.container_engine_config_path.display(),
                )
            })
            .context(MESSAGE);
        let daemon_config_file = match daemon_config_file {
            Ok(daemon_config_file) => daemon_config_file,
            Err(err) => {
                return Ok(CheckResult::Warning(err.into()));
            }
        };
        let daemon_config: DaemonConfig = serde_json::from_reader(daemon_config_file)
            .with_context(|_| {
                format!(
                    "Could not parse container engine config file {}",
                    check.container_engine_config_path.display(),
                )
            })
            .context(MESSAGE)?;
        self.daemon_config = Some(daemon_config.clone());

        if daemon_config.log_driver.is_none() {
            return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
        }

        if let Some(log_opts) = &daemon_config.log_opts {
            if log_opts.max_file.is_none() {
                return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
            }

            if log_opts.max_size.is_none() {
                return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
            }
        } else {
            return Ok(CheckResult::Warning(Context::new(MESSAGE).into()));
        }

        Ok(CheckResult::Ok)
    }
}

#[derive(serde_derive::Deserialize, serde_derive::Serialize, Clone)]
struct DaemonConfig {
    #[serde(rename = "log-driver")]
    log_driver: Option<String>,

    #[serde(rename = "log-opts")]
    log_opts: Option<DaemonConfigLogOpts>,
}

#[derive(serde_derive::Deserialize, serde_derive::Serialize, Clone)]
struct DaemonConfigLogOpts {
    #[serde(rename = "max-file")]
    max_file: Option<String>,

    #[serde(rename = "max-size")]
    max_size: Option<String>,
}
