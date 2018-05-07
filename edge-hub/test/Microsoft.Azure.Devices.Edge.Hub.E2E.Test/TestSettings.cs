// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    public class TestSettings
    {
        public static readonly ITransportSettings[] MqttTransportSettings =
        {
            new MqttTransportSettings(Client.TransportType.Mqtt_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            }
        };

        public static readonly ITransportSettings[] AmqpTransportSettings =
        {
            new AmqpTransportSettings(Client.TransportType.Amqp_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            }
        };

        public static IEnumerable<object[]> TransportSettings => new List<object[]>
        {
            new object[] { AmqpTransportSettings },
            new object[] { MqttTransportSettings }
        };
    }
}
