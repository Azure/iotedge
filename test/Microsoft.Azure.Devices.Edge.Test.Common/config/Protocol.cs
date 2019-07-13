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
        public static TransportType ToTransportType(this Protocol p)
        {
            switch (p)
            {
                case Protocol.Amqp:
                    return TransportType.Amqp_Tcp_Only;
                case Protocol.AmqpWs:
                    return TransportType.Amqp_WebSocket_Only;
                case Protocol.Mqtt:
                    return TransportType.Mqtt_Tcp_Only;
                case Protocol.MqttWs:
                    return TransportType.Mqtt_WebSocket_Only;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }
    }
}
