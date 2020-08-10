use edgelet_core::RuntimeSettings;

use crate::check::{
    checker::Checker, upstream_protocol_port::UpstreamProtocolPort, Check, CheckResult,
};

pub(crate) fn get_host_connect_iothub_tests<'a>(
    runtime: &'static mut tokio::runtime::Runtime,
) -> Vec<Box<dyn Checker>> {
    vec![
        make_check(
            "host-connect-iothub-amqp",
            "host can connect to and perform TLS handshake with IoT Hub AMQP port",
            UpstreamProtocolPort::Amqp,
            runtime,
        ),
        make_check(
            "host-connect-iothub-https",
            "host can connect to and perform TLS handshake with IoT Hub HTTPS / WebSockets port",
            UpstreamProtocolPort::Https,
            runtime,
        ),
        make_check(
            "host-connect-iothub-mqtt",
            "host can connect to and perform TLS handshake with IoT Hub MQTT port",
            UpstreamProtocolPort::Mqtt,
            runtime,
        ),
    ]
}

#[derive(serde_derive::Serialize)]
pub(crate) struct HostConnectIotHub<'a> {
    port_number: u16,
    iothub_hostname: Option<String>,
    proxy: Option<String>,
    #[serde(skip)]
    id: &'static str,
    #[serde(skip)]
    description: &'static str,
    #[serde(skip)]
    runtime: &'a mut tokio::runtime::Runtime,
}

impl<'a> Checker for HostConnectIotHub<'a> {
    fn id(&self) -> &'static str {
        self.id
    }
    fn description(&self) -> &'static str {
        self.description
    }
    fn execute(&mut self, check: &'static mut Check) -> CheckResult {
        self.inner_execute(check)
            .unwrap_or_else(CheckResult::Failed)
    }
    fn get_json(&self) -> serde_json::Value {
        serde_json::to_value(self).unwrap()
    }
}

impl<'a> HostConnectIotHub<'a> {
    fn inner_execute(&mut self, check: &'static mut Check) -> Result<CheckResult, failure::Error> {
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

        if let Some(proxy) = self.proxy {
            self.runtime.block_on(
                super::host_connect_dps_endpoint::resolve_and_tls_handshake_proxy(
                    iothub_hostname,
                    self.port_number,
                    &proxy,
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

fn make_check<'a>(
    id: &'static str,
    description: &'static str,
    upstream_protocol_port: UpstreamProtocolPort,
    runtime: &'a mut tokio::runtime::Runtime,
) -> Box<HostConnectIotHub<'a>> {
    Box::new(HostConnectIotHub::<'a> {
        id,
        description,
        port_number: upstream_protocol_port.as_port(),
        iothub_hostname: None,
        proxy: None,
        runtime,
    })
}
