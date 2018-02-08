// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public enum UpstreamProtocol
    {
        Amqp,
        AmqpWs,
        Mqtt,
        MqttWs
    }

    public static class UpstreamProtocolHelper
    {
        public static Option<UpstreamProtocol> ToUpstreamProtocol(this string value) =>
            !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out UpstreamProtocol upstreamProtocol)
                ? Option.Some(upstreamProtocol)
                : Option.None<UpstreamProtocol>();
    }
}
