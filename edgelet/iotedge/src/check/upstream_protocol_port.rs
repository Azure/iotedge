// Copyright (c) Microsoft. All rights reserved.

#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd, serde::Serialize)]
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
