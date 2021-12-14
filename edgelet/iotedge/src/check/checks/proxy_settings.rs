use edgelet_core::RuntimeSettings;
use failure::{self, Context};

use crate::check::{Check, CheckResult, Checker};

#[derive(Default, serde_derive::Serialize)]
pub(crate) struct ProxySettings {}

impl Checker for ProxySettings {
    fn id(&self) -> &'static str {
        "proxy-settings"
    }
    fn description(&self) -> &'static str {
        "proxy settings are consistent in aziot-edged, aziot-identityd, moby daemon and config.toml"
    }
    fn execute(&mut self, check: &mut Check, _: &mut tokio::runtime::Runtime) -> CheckResult {
        Self::inner_execute(check).unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl ProxySettings {
    fn inner_execute(check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &mut check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        // Pull the proxy address from the aziot-edged settings
        // for Edge Agent's environment variables.
        let edge_agent_proxy_uri = match settings.agent().env().get("https_proxy") {
            Some(edge_agent_proxy_uri) => edge_agent_proxy_uri.clone(),
            None => "".into(),
        };

        // Pull local service env variables for Moby, Identity Daemon and Edge Daemon
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
            Ok(CheckResult::Warning(Context::new(
                format!(
                    "The proxy setting for IoT Edge Agent {:?}, IoT Edge Daemon {:?}, and Moby {:?} may need to be identical.",
                    edge_agent_proxy_uri,
                    edge_daemon_proxy_uri,
                    moby_proxy_uri
                )
            ).into()))
        }
    }
}
