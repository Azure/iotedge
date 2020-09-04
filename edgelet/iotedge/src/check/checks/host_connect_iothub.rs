use edgelet_core::RuntimeSettings;

use crate::check::{
    checker::Checker, upstream_protocol_port::UpstreamProtocolPort, Check, CheckResult,
};

pub(crate) fn get_host_connect_iothub_tests() -> Vec<Box<dyn Checker>> {
    vec![
        make_check(
            "host-connect-iothub-amqp",
            "host can connect to and perform TLS handshake with IoT Hub AMQP port",
            UpstreamProtocolPort::Amqp,
        ),
        make_check(
            "host-connect-iothub-https",
            "host can connect to and perform TLS handshake with IoT Hub HTTPS / WebSockets port",
            UpstreamProtocolPort::Https,
        ),
        make_check(
            "host-connect-iothub-mqtt",
            "host can connect to and perform TLS handshake with IoT Hub MQTT port",
            UpstreamProtocolPort::Mqtt,
        ),
    ]
}

#[derive(serde_derive::Serialize)]
pub(crate) struct HostConnectIotHub {
    port_number: u16,
    iothub_hostname: Option<String>,
    proxy: Option<String>,
    #[serde(skip)]
    id: &'static str,
    #[serde(skip)]
    description: &'static str,
}

impl Checker for HostConnectIotHub {
    fn id(&self) -> &'static str {
        self.id
    }
    fn description(&self) -> &'static str {
        self.description
    }
    fn execute(&mut self, check: &mut Check, runtime: &mut tokio::runtime::Runtime) -> CheckResult {
        self.inner_execute(check, runtime)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl HostConnectIotHub {
    fn inner_execute(
        &mut self,
        check: &mut Check,
        runtime: &mut tokio::runtime::Runtime,
    ) -> Result<CheckResult, failure::Error> {
        let iothub_hostname = if let Some(iothub_hostname) = &check.iothub_hostname {
            iothub_hostname
        } else {
            return Ok(CheckResult::Skipped);
        };
        self.iothub_hostname = Some(iothub_hostname.clone());

        self.proxy = check
            .settings
            .as_ref()
            .and_then(|settings| settings.agent().env().get("https_proxy").cloned());

        if let Some(proxy) = &self.proxy {
            runtime.block_on(
                super::host_connect_dps_endpoint::resolve_and_tls_handshake_proxy(
                    iothub_hostname.clone(),
                    Some(self.port_number),
                    proxy.clone(),
                ),
            )?;
        } else {
            super::host_connect_dps_endpoint::resolve_and_tls_handshake(
                &(&**iothub_hostname, self.port_number),
                iothub_hostname,
                &format!("{}:{}", iothub_hostname, self.port_number),
            )?;
        }

        Ok(CheckResult::Ok)
    }
}

fn make_check(
    id: &'static str,
    description: &'static str,
    upstream_protocol_port: UpstreamProtocolPort,
) -> Box<HostConnectIotHub> {
    Box::new(HostConnectIotHub {
        id,
        description,
        port_number: upstream_protocol_port.as_port(),
        iothub_hostname: None,
        proxy: None,
    })
}
