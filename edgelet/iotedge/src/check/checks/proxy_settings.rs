use failure::{self, Context};

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ProxySettings {}

#[async_trait::async_trait]
impl Checker for ProxySettings {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "proxy-settings",
            description: "proxy settings are consistent in aziot-edged, moby daemon and config.toml",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ProxySettings{
    async fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &mut check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        // Pull the proxy address from the aziot-edged settings
        // for Edge Agent's environment variables.
        let edge_agent_proxy_uri = settings.base.agent.env().get("https_proxy");
        // Pull local service env variables for Moby and Edge Daemon
        let moby_proxy_uri = check.docker_proxy.clone();
        let edge_daemon_proxy_uri = check.aziot_edge_proxy.clone();

        if edge_agent_proxy_uri.is_some() && moby_proxy_uri.is_some() && edge_daemon_proxy_uri.is_some() &&
            edge_agent_proxy_uri == moby_proxy_uri.as_ref() && edge_agent_proxy_uri == edge_daemon_proxy_uri.as_ref() {
            Ok(CheckResult::Ok)
        } else if edge_agent_proxy_uri.is_none() && moby_proxy_uri.is_none() && edge_daemon_proxy_uri.is_none() {
            Ok(CheckResult::Ok)
        } else {
            return Err(Context::new(
                format!(
                    "The proxy setting for IoT Edge Agent {:?}, IoT Edge Daemon {:?} and Moby {:?} must be identical.",
                    edge_agent_proxy_uri,
                    edge_daemon_proxy_uri,
                    moby_proxy_uri
                )
            ).into());
        }
    }
}