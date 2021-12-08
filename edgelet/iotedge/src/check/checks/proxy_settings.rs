use failure::{self, Context};

use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ProxySettings {}

#[async_trait::async_trait]
impl Checker for ProxySettings {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "proxy-settings",
            description:
                "proxy settings are consistent in aziot-edged, moby daemon and config.toml",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .await
            .unwrap_or_else(CheckResult::Failed)
    }
}

impl ProxySettings {
    async fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &mut check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        // Pull the proxy address from the aziot-edged settings
        // for Edge Agent's environment variables.
        let edge_agent_proxy_uri = match settings.base.agent.env().get("https_proxy") {
            Some(edge_agent_proxy_uri) => edge_agent_proxy_uri.clone(),
            None => "".into(),
        };

        // Pull local service env variables for Moby and Edge Daemon
        let moby_proxy_uri = match check.docker_proxy.clone() {
            Some(moby_proxy_uri) => moby_proxy_uri,
            None => "".into(),
        };

        let edge_daemon_proxy_uri = match check.aziot_edge_proxy.clone() {
            Some(edge_daemon_proxy_uri) => edge_daemon_proxy_uri,
            None => "".into(),
        };

        if edge_agent_proxy_uri.eq(&moby_proxy_uri)
            && edge_agent_proxy_uri.eq(&edge_daemon_proxy_uri)
        {
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
