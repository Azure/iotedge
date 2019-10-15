use crate::check::{
    checker::Checker, upstream_protocol_port::UpstreamProtocolPort, Check, CheckResult,
};

pub(crate) fn get_host_container_iothub_tests() -> Vec<Box<dyn Checker>> {
    vec![
        #[cfg(unix)]
        make_check(
            "container-default-connect-iothub-amqp",
            "container on the default network can connect to IoT Hub AMQP port",
            UpstreamProtocolPort::Amqp,
            false,
        ),
        #[cfg(unix)]
        make_check(
            "container-default-connect-iothub-https",
            "container on the default network can connect to IoT Hub HTTPS / WebSockets port",
            UpstreamProtocolPort::Https,
            false,
        ),
        #[cfg(unix)]
        make_check(
            "container-default-connect-iothub-mqtt",
            "container on the default network can connect to IoT Hub MQTT port",
            UpstreamProtocolPort::Mqtt,
            false,
        ),
        make_check(
            "container-connect-iothub-amqp",
            "container on the IoT Edge module network can connect to IoT Hub AMQP port",
            UpstreamProtocolPort::Amqp,
            true,
        ),
        make_check(
            "container-connect-iothub-https",
            "container on the IoT Edge module network can connect to IoT Hub HTTPS / WebSockets port",
            UpstreamProtocolPort::Https,
            true,
        ),
        make_check(
            "container-connect-iothub-mqtt",
            "container on the IoT Edge module network can connect to IoT Hub MQTT port",
            UpstreamProtocolPort::Mqtt,
            true,
        ),
    ]
}

#[derive(serde_derive::Serialize)]
pub(crate) struct ContainerConnectIotHub {
    port_number: u16,
    iothub_hostname: Option<String>,
    network_name: Option<String>,
    diagnostics_image_name: Option<String>,
    #[serde(skip)]
    id: &'static str,
    #[serde(skip)]
    description: &'static str,
    #[serde(skip)]
    use_container_runtime_network: bool,
}

impl Checker for ContainerConnectIotHub {
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
impl ContainerConnectIotHub {
    fn inner_execute(&mut self, check: &mut Check) -> Result<CheckResult, failure::Error> {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return Ok(CheckResult::Skipped);
        };

        let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
            docker_host_arg
        } else {
            return Ok(CheckResult::Skipped);
        };

        let iothub_hostname = if let Some(iothub_hostname) = &check.iothub_hostname {
            iothub_hostname
        } else {
            return Ok(CheckResult::Skipped);
        };
        self.iothub_hostname = Some(iothub_hostname.to_owned());

        let network_name = settings.moby_runtime().network().name();
        self.network_name = Some(network_name.to_owned());

        let mut args = vec!["run", "--rm"];

        let port = self.port_number.to_string();

        if self.use_container_runtime_network {
            args.extend(&["--network", network_name]);
        }

        args.extend(&[
            &check.diagnostics_image_name,
            "/iotedge-diagnostics",
            "iothub",
            "--hostname",
            iothub_hostname,
            "--port",
            &port,
        ]);
        self.diagnostics_image_name = Some(check.diagnostics_image_name.clone());

        if let Err((_, err)) = super::docker(docker_host_arg, args) {
            return Err(err
                .context(format!(
                    "Container on the {} network could not connect to {}:{}",
                    if self.use_container_runtime_network {
                        network_name
                    } else {
                        "default"
                    },
                    iothub_hostname,
                    port,
                ))
                .into());
        }

        Ok(CheckResult::Ok)
    }
}

fn make_check(
    id: &'static str,
    description: &'static str,
    upstream_protocol_port: UpstreamProtocolPort,
    use_container_runtime_network: bool,
) -> Box<ContainerConnectIotHub> {
    Box::new(ContainerConnectIotHub {
        id,
        description,
        port_number: upstream_protocol_port.as_port(),
        use_container_runtime_network,
        iothub_hostname: None,
        network_name: None,
        diagnostics_image_name: None,
    })
}
