// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    public class TestSettings
    {
        public static readonly ITransportSettings[] MqttTransportSettings =
        {
            new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            }
        };

        public static readonly ITransportSettings[] AmqpTransportSettings =
        {
            new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
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

        static readonly Lazy<IList<object[]>> TransportSettingsLazy = new Lazy<IList<object[]>>(() => GetTransportSettings(), true);

        public static IList<object[]> TransportSettings => TransportSettingsLazy.Value;

        static readonly Lazy<IList<object[]>> AmqpTransportTestSettingsLazy = new Lazy<IList<object[]>>(() => GetAmqpTransportSettings(false), true);

        public static IList<object[]> AmqpTransportTestSettings => AmqpTransportTestSettingsLazy.Value;

        static IList<object[]> GetTransportSettings()
        {
            bool.TryParse(ConfigHelper.TestConfig["enableWebSocketsTests"], out bool enableWebSocketsTests);
            IList<object[]> transportSettings = GetAmqpTransportSettings(enableWebSocketsTests);

            transportSettings.Add(new object[] { MqttTransportSettings });
            if (enableWebSocketsTests)
            {
                transportSettings.Add(new object[] { MqttWebSocketsTransportSettings });
            }

            return transportSettings;
        }

        static IList<object[]> GetAmqpTransportSettings(bool webSockets = false)
        {
            IList<object[]> transportSettings = new List<object[]>
            {
                new object[] { AmqpTransportSettings },
            };

            if (webSockets)
            {
                transportSettings.Add(new object[] { AmqpWebSocketsTransportSettings });
            }

            return transportSettings;
        }
    }
}
