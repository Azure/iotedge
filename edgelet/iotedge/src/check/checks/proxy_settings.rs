use crate::check::{Check, CheckResult, Checker, CheckerMeta};

#[derive(Default, serde::Serialize)]
pub(crate) struct ProxySettings {}

#[async_trait::async_trait]
impl Checker for ProxySettings {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: "proxy-settings",
            description:
                "proxy settings are consistent in aziot-edged, aziot-identityd, moby daemon and config.toml",
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        Self::inner_execute(check)
    }
}

impl ProxySettings {
    fn inner_execute(check: &mut Check) -> CheckResult {
        let settings = if let Some(settings) = &mut check.settings {
            settings
        } else {
            return CheckResult::Skipped;
        };

        // Pull the proxy address from the aziot-edged settings
        // for Edge Agent's environment variables.
        let edge_agent_proxy_uri = settings
            .base
            .agent
            .env()
            .get("https_proxy")
            .cloned()
            .unwrap_or_default();

        // Pull local service env variables for Moby, Identity Daemon and Edge Daemon
        let moby_proxy_uri = check.docker_proxy.clone().unwrap_or_default();

        let edge_daemon_proxy_uri = check.aziot_edge_proxy.clone().unwrap_or_default();

        let identity_daemon_proxy_uri = check.aziot_identity_proxy.clone().unwrap_or_default();

        if edge_agent_proxy_uri == moby_proxy_uri
            && edge_agent_proxy_uri == edge_daemon_proxy_uri
            && edge_agent_proxy_uri == identity_daemon_proxy_uri
        {
            CheckResult::Ok
        } else {
            CheckResult::Warning(anyhow::anyhow!(
                    "The proxy setting for IoT Edge Agent {:?}, IoT Edge Daemon {:?}, IoT Identity Daemon {:?}, and Moby {:?} may need to be identical.",
                    edge_agent_proxy_uri,
                    edge_daemon_proxy_uri,
                    identity_daemon_proxy_uri,
                    moby_proxy_uri
                )
            )
        }
    }
}
