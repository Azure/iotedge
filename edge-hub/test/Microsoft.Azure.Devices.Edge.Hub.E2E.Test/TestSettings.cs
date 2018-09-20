// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    public class TestSettings
    {
        static TestSettings()
        {
            bool.TryParse(ConfigHelper.TestConfig["enableWebSocketsTests"], out bool enableWebSocketsTests);

            TransportSettings = new List<object[]>
            {
                new object[] { AmqpTransportSettings },
                new object[] { MqttTransportSettings },

            };

            if (enableWebSocketsTests)
            {
                TransportSettings.Add(new object[] { MqttWebSocketsTransportSettings });
                TransportSettings.Add(new object[] { AmqpWebSocketsTransportSettings });
            }
        }

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

        public static readonly ITransportSettings[] MqttWebSocketsTransportSettings =
        {
            new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only)
        };

        public static readonly ITransportSettings[] AmqpWebSocketsTransportSettings =
        {
            new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only)
        };

        public static IList<object[]> TransportSettings { get; }
    }
}
