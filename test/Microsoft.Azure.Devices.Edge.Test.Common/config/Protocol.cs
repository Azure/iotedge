// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.ComponentModel;
    using Microsoft.Azure.Devices.Client;

    public enum Protocol
    {
        Amqp,
        AmqpWs,
        Mqtt,
        MqttWs
    }

    public static class ProtocolExtensions
    {
        public static IotHubClientTransportProtocol ToTransportProtocol(this Protocol p)
        {
            switch (p)
            {
                case Protocol.Amqp:
                case Protocol.Mqtt:
                    return IotHubClientTransportProtocol.Tcp;
                case Protocol.AmqpWs:
                case Protocol.MqttWs:
                    return IotHubClientTransportProtocol.WebSocket;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        public static IotHubClientTransportSettings ToTransportSettings(this Protocol p)
        {
            switch (p)
            {
                case Protocol.Amqp:
                case Protocol.AmqpWs:
                    return new IotHubClientAmqpSettings(p.ToTransportProtocol());
                case Protocol.Mqtt:
                case Protocol.MqttWs:
                    return new IotHubClientMqttSettings(p.ToTransportProtocol());
                default:
                    throw new InvalidEnumArgumentException();
            }
        }
    }
}
