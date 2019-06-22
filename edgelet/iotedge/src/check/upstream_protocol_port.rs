// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd)]
pub(super) enum UpstreamProtocolPort {
    Amqp,
    Https,
    Mqtt,
}

impl UpstreamProtocolPort {
    pub(super) fn as_port(self) -> u16 {
        #[allow(clippy::match_same_arms)]
        match self {
            UpstreamProtocolPort::Amqp => 5671,
            UpstreamProtocolPort::Https => 443,
            UpstreamProtocolPort::Mqtt => 8883,
        }
    }
}

impl std::str::FromStr for UpstreamProtocolPort {
    type Err = String;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        match &*s.to_lowercase() {
            "amqp" => Ok(UpstreamProtocolPort::Amqp),
            "amqpws" | "mqttws" => Ok(UpstreamProtocolPort::Https),
            "mqtt" => Ok(UpstreamProtocolPort::Mqtt),
            _ => Err(format!(
                r#"expected one of "Amqp", "AmqpWs", "Mqtt" or "MqttWs" but got {:?}"#,
                s,
            )),
        }
    }
}
