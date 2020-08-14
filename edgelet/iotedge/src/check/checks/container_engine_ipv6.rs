use std::fs::File;

use failure::{self, Context, Fail, ResultExt};

use edgelet_core::{self, MobyNetwork};

use crate::check::{checker::Checker, Check, CheckResult};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ContainerEngineIPv6 {
    expected_use_ipv6: Option<bool>,
    actual_use_ipv6: Option<bool>,
}

impl Checker for ContainerEngineIPv6 {
    fn id(&self) -> &'static str {
        "container-engine-ipv6"
    }
    fn description(&self) -> &'static str {
        "IPv6 network configuration"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl ContainerEngineIPv6 {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
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
                if is_edge_ipv6_configured {
                    return Err(err.context(MESSAGE).into());
                } else {
                    return Ok(CheckResult::Ignored);
                }
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
        self.actual_use_ipv6 = daemon_config.ipv6;

        match (daemon_config.ipv6.unwrap_or_default(), is_edge_ipv6_configured) {
            (true, _) if cfg!(windows) => Err(Context::new("IPv6 container network configuration is not supported for the Windows operating system.").into()),
            (true, _) => Ok(CheckResult::Ok),
            (false, true) => Err(Context::new(MESSAGE).into()),
            (false, false) => Ok(CheckResult::Ignored),
        }
    }
}

#[derive(serde_derive::Deserialize)]
struct DaemonConfig {
    ipv6: Option<bool>,
}
