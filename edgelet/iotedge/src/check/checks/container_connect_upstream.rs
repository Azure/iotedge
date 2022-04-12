use edgelet_settings::RuntimeSettings;

use serde::Deserialize;

use crate::check::{
    upstream_protocol_port::UpstreamProtocolPort, Check, CheckResult, Checker, CheckerMeta,
};

pub(crate) fn get_host_container_upstream_tests() -> Vec<Box<dyn Checker>> {
    vec![
        #[cfg(unix)]
        make_check(
            "container-default-connect-upstream-amqp",
            "container on the default network can connect to upstream AMQP port",
            UpstreamProtocolPort::Amqp,
            false,
        ),
        #[cfg(unix)]
        make_check(
            "container-default-connect-upstream-https",
            "container on the default network can connect to upstream HTTPS / WebSockets port",
            UpstreamProtocolPort::Https,
            false,
        ),
        #[cfg(unix)]
        make_check(
            "container-default-connect-upstream-mqtt",
            "container on the default network can connect to upstream MQTT port",
            UpstreamProtocolPort::Mqtt,
            false,
        ),
        make_check(
            "container-connect-upstream-amqp",
            "container on the IoT Edge module network can connect to upstream AMQP port",
            UpstreamProtocolPort::Amqp,
            true,
        ),
        make_check(
            "container-connect-upstream-https",
            "container on the IoT Edge module network can connect to upstream HTTPS / WebSockets port",
            UpstreamProtocolPort::Https,
            true,
        ),
        make_check(
            "container-connect-upstream-mqtt",
            "container on the IoT Edge module network can connect to upstream MQTT port",
            UpstreamProtocolPort::Mqtt,
            true,
        ),
    ]
}

#[derive(serde_derive::Serialize)]
pub(crate) struct ContainerConnectUpstream {
    upstream_port: UpstreamProtocolPort,
    upstream_hostname: Option<String>,
    network_name: Option<String>,
    diagnostics_image_name: Option<String>,
    proxy: Option<String>,
    #[serde(skip)]
    id: &'static str,
    #[serde(skip)]
    description: &'static str,
    #[serde(skip)]
    use_container_runtime_network: bool,
}

#[async_trait::async_trait]
impl Checker for ContainerConnectUpstream {
    fn meta(&self) -> CheckerMeta {
        CheckerMeta {
            id: self.id,
            description: self.description,
        }
    }

    async fn execute(&mut self, check: &mut Check) -> CheckResult {
        self.inner_execute(check).await
    }
}

impl ContainerConnectUpstream {
    async fn inner_execute(&mut self, check: &mut Check) -> CheckResult {
        let settings = if let Some(settings) = &check.settings {
            settings
        } else {
            return CheckResult::Skipped;
        };

        let docker_host_arg = if let Some(docker_host_arg) = &check.docker_host_arg {
            docker_host_arg
        } else {
            return CheckResult::Skipped;
        };

        let diagnostics_image_name = if check
            .diagnostics_image_name
            .starts_with("/azureiotedge-diagnostics:")
        {
            check.parent_hostname.as_ref().map_or_else(
                || "mcr.microsoft.com".to_string() + &check.diagnostics_image_name,
                |upstream_hostname| upstream_hostname.to_string() + &check.diagnostics_image_name,
            )
        } else {
            check.diagnostics_image_name.clone()
        };

        let parent_hostname: String;
        let upstream_hostname = if let Some(upstream_hostname) = check.parent_hostname.as_ref() {
            parent_hostname = upstream_hostname.to_string();
            &parent_hostname
        } else if let Some(iothub_hostname) = &check.iothub_hostname {
            iothub_hostname
        } else {
            return CheckResult::Skipped;
        };

        self.upstream_hostname = Some(upstream_hostname.clone());

        let upstream_protocol =
            get_env_from_container(docker_host_arg, "edgeAgent", "UpstreamProtocol")
                .await
                .unwrap_or(
                    // We should default to AMQP with fallback to AMQPWS.
                    if self.upstream_port == UpstreamProtocolPort::Https {
                        UpstreamProtocol::AmqpWs
                    } else {
                        UpstreamProtocol::Amqp
                    },
                );

        let should_skip_instead = should_skip_instead(self.upstream_port, upstream_protocol);

        let workload_uri = settings.connect().workload_uri().to_string();
        let workload_uri_path = settings.connect().workload_uri().path().to_string();
        let map_volume = format!("{}:{}", workload_uri_path, workload_uri_path);

        let network_name = settings.moby_runtime().network().name();
        self.network_name = Some(network_name.to_owned());

        let mut args = vec!["run", "--rm"];

        let port = self.upstream_port.as_port().to_string();

        if self.use_container_runtime_network {
            args.extend(&["--network", network_name]);
        }

        if check.parent_hostname.is_some() {
            args.extend(&["-v", &map_volume]);
        }

        self.diagnostics_image_name = Some(check.diagnostics_image_name.clone());
        args.extend(&[
            &diagnostics_image_name,
            "dotnet",
            "IotedgeDiagnosticsDotnet.dll",
            "upstream",
            "--hostname",
            upstream_hostname,
            "--port",
            &port,
        ]);

        if check.parent_hostname.is_some() {
            args.extend(&["--isNested", "true"]);
            args.extend(&["--workload_uri", &workload_uri]);
        }

        if &port == "443" {
            self.proxy = check.proxy_uri.clone();
            if let Some(proxy) = &check.proxy_uri {
                args.extend(&["--proxy", proxy.as_str()]);
            }
        }

        if should_skip_instead {
            return CheckResult::SkippedDueTo("not required in this configuration".into());
        }

        if let Err((_, err)) = super::docker(docker_host_arg, args).await {
            let err = err.context(format!(
                "Container on the {} network could not connect to {}:{}",
                if self.use_container_runtime_network {
                    network_name
                } else {
                    "default"
                },
                upstream_hostname,
                port,
            ));
            return CheckResult::Failed(err);
        }

        CheckResult::Ok
    }
}

fn make_check(
    id: &'static str,
    description: &'static str,
    upstream_protocol_port: UpstreamProtocolPort,
    use_container_runtime_network: bool,
) -> Box<ContainerConnectUpstream> {
    Box::new(ContainerConnectUpstream {
        id,
        description,
        upstream_port: upstream_protocol_port,
        use_container_runtime_network,
        upstream_hostname: None,
        network_name: None,
        diagnostics_image_name: None,
        proxy: None,
    })
}

#[derive(Clone, Copy, Debug, Deserialize)]
enum UpstreamProtocol {
    Amqp,
    AmqpWs,
    Mqtt,
    MqttWs,
}

async fn get_env_from_container(
    docker_host_arg: &str,
    name: &str,
    env_var_name: &str,
) -> Option<UpstreamProtocol> {
    let shell_var = to_shell_var(env_var_name);
    let command = format!("echo {}", shell_var);
    super::docker(docker_host_arg, &["exec", name, "/bin/sh", "-c", &command])
        .await
        .map_err(|(_, err)| err)
        .and_then(|output| {
            let mut s = String::from_utf8(output)?;
            // Remove newline
            if s.ends_with('\n') {
                s.pop();
            }
            Ok(s)
        })
        .and_then(|string| {
            let string = to_serde_enum(string);
            let up = serde_json::from_str::<UpstreamProtocol>(&string)?;
            Ok(up)
        })
        .ok()
}

fn to_shell_var(val: impl Into<String>) -> String {
    let dollar = String::from("$");
    let val_str = val.into();
    dollar + &val_str
}

fn to_serde_enum(val: impl Into<String>) -> String {
    format!("{:?}", val.into())
}

fn should_skip_instead(upp: UpstreamProtocolPort, up: UpstreamProtocol) -> bool {
    match upp {
        UpstreamProtocolPort::Amqp => matches!(up, UpstreamProtocol::Mqtt),
        UpstreamProtocolPort::Https => {
            matches!(up, UpstreamProtocol::Amqp | UpstreamProtocol::Mqtt)
        }
        UpstreamProtocolPort::Mqtt => matches!(up, UpstreamProtocol::Amqp),
    }
}

#[cfg(test)]
#[allow(clippy::bool_assert_comparison)]
mod tests {
    use super::*;

    #[test]
    fn should_skip_instead_is_true_if_testing_amqp_and_protocol_is_mqtt() {
        assert_eq!(
            should_skip_instead(UpstreamProtocolPort::Amqp, UpstreamProtocol::Mqtt),
            true
        );
    }

    #[test]
    fn should_skip_instead_is_true_if_testing_mqtt_and_protocol_is_amqp() {
        assert_eq!(
            should_skip_instead(UpstreamProtocolPort::Mqtt, UpstreamProtocol::Amqp),
            true
        );
    }

    #[test]
    fn should_skip_instead_is_false_if_testing_amqp_and_protocol_is_amqp() {
        assert_eq!(
            should_skip_instead(UpstreamProtocolPort::Amqp, UpstreamProtocol::Amqp),
            false
        );
    }

    #[test]
    fn should_skip_instead_is_false_if_testing_mqtt_and_protocol_is_mqtt() {
        assert_eq!(
            should_skip_instead(UpstreamProtocolPort::Mqtt, UpstreamProtocol::Mqtt),
            false
        );
    }

    #[test]
    fn should_skip_instead_is_true_if_testing_https_and_protocol_is_mqtt() {
        assert_eq!(
            should_skip_instead(UpstreamProtocolPort::Https, UpstreamProtocol::Mqtt),
            true
        );
    }

    #[test]
    fn should_skip_instead_is_true_if_testing_https_and_protocol_is_amqp() {
        assert_eq!(
            should_skip_instead(UpstreamProtocolPort::Https, UpstreamProtocol::Amqp),
            true
        );
    }

    #[test]
    fn should_skip_instead_is_false_if_testing_https_and_protocol_is_mqttws() {
        assert_eq!(
            should_skip_instead(UpstreamProtocolPort::Https, UpstreamProtocol::MqttWs),
            false
        );
    }

    #[test]
    fn should_skip_instead_is_false_if_testing_https_and_protocol_is_amqpws() {
        assert_eq!(
            should_skip_instead(UpstreamProtocolPort::Https, UpstreamProtocol::AmqpWs),
            false
        );
    }
}
