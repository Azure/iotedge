use std::fs::File;

use anyhow::Context;

use edgelet_settings::MobyNetwork;

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde::Serialize)]
pub(crate) struct ContainerEngineIPv6 {
    expected_use_ipv6: Option<bool>,
    actual_use_ipv6: Option<bool>,
}

#[async_trait::async_trait]
impl Checker for ContainerEngineIPv6 {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "container-engine-ipv6",
            description: "IPv6 network configuration",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ContainerEngineIPv6 {
    fn inner_execute(&mut self, check: &mut Check) -> anyhow::Result<CheckResult> {
        const MESSAGE: &str =
            "Container engine is not configured for IPv6 communication.\n\
             Please see https://aka.ms/iotedge-docker-ipv6 for a guide on how to enable IPv6 support.";

        let is_edge_ipv6_configured = check.settings.as_ref().map_or(false, |settings| {
            let moby_network = settings.moby_runtime().network();
            if let MobyNetwork::Network(network) = moby_network {
                network.ipv6().unwrap_or_default()
            } else {
                false
            }
        });
        self.expected_use_ipv6 = Some(is_edge_ipv6_configured);

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
                return if is_edge_ipv6_configured {
                    Err(err.context(MESSAGE))
                } else {
                    Ok(CheckResult::Ignored)
                }
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
        self.actual_use_ipv6 = daemon_config.ipv6;

        if daemon_config.ipv6.unwrap_or_default() {
            Ok(CheckResult::Ok)
        } else if is_edge_ipv6_configured {
            Err(anyhow::anyhow!(MESSAGE))
        } else {
            Ok(CheckResult::Ignored)
        }
    }
}

#[derive(serde::Deserialize)]
struct DaemonConfig {
    ipv6: Option<bool>,
}
