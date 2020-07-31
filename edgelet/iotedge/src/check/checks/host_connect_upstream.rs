use crate::check::{
    checker::Checker, upstream_protocol_port::UpstreamProtocolPort, Check, CheckResult,
};
use edgelet_core::RuntimeSettings;

pub(crate) fn get_host_connect_upstream_tests() -> Vec<Box<dyn Checker>> {
    vec![
        make_check(
            "host-connect-upstream-amqp",
            "host can connect to and perform TLS handshake with upstream AMQP port",
            UpstreamProtocolPort::Amqp,
        ),
        make_check(
            "host-connect-upstream-https",
            "host can connect to and perform TLS handshake with upstream HTTPS / WebSockets port",
            UpstreamProtocolPort::Https,
        ),
        make_check(
            "host-connect-upstream-mqtt",
            "host can connect to and perform TLS handshake with upstream MQTT port",
            UpstreamProtocolPort::Mqtt,
        ),
    ]
}

#[derive(serde_derive::Serialize)]
pub(crate) struct HostConnectUpstream {
    port_number: u16,
    iothub_hostname: Option<String>,
    #[serde(skip)]
    id: &'static str,
    #[serde(skip)]
    description: &'static str,
}

impl Checker for HostConnectUpstream {
    fn id(&self) -> &'static str {
        self.id
    }
    fn description(&self) -> &'static str {
        self.description
    }
    fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl HostConnectUpstream {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        let upstream_hostname =
            if let Some(parent_hostname) = RuntimeSettings::parent_hostname(settings) {
                parent_hostname
            } else if let Some(iothub_hostname) = &check.iothub_hostname {
                iothub_hostname
            } else {
                return Ok(CheckResult::Skipped);
            };

        super::host_connect_dps_endpoint::resolve_and_tls_handshake(
            &(upstream_hostname, self.port_number),
            upstream_hostname,
            &format!("{}:{}", upstream_hostname, self.port_number),
        )?;

        Ok(CheckResult::Ok)
    }
}

fn make_check(
    id: &'static str,
    description: &'static str,
    upstream_protocol_port: UpstreamProtocolPort,
) -> Box<HostConnectUpstream> {
    Box::new(HostConnectUpstream {
        id,
        description,
        port_number: upstream_protocol_port.as_port(),
        iothub_hostname: None,
    })
}
