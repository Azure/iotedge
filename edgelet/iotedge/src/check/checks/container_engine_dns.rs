use std::fs::File;

use anyhow::Context;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ContainerEngineDns {
    container_engine_config_path: Option<String>,
    dns: Option<Vec<String>>,
}

#[async_trait::async_trait]
impl Checker for ContainerEngineDns {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "container-engine-dns",
            description: "DNS server",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ContainerEngineDns {
    fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        const MESSAGE: &str =
        "Container engine is not configured with DNS server setting, which may impact connectivity to IoT Hub.\n\
         Please see https://aka.ms/iotedge-prod-checklist-dns for best practices.\n\
         You can ignore this warning if you are setting DNS server per module in the Edge deployment.";

        self.container_engine_config_path = Some(
            check
                .container_engine_config_path
                .to_string_lossy()
                .into_owned(),
        );

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
        self.dns = daemon_config.dns.clone();

        if daemon_config.dns.map_or(true, |e| e.is_empty()) {
            return Ok(CheckResult::Warning(anyhow::Error::msg(MESSAGE)));
        }

        Ok(CheckResult::Ok)
    }
}

#[derive(serde_derive::Deserialize)]
struct DaemonConfig {
    dns: Option<Vec<String>>,
}
