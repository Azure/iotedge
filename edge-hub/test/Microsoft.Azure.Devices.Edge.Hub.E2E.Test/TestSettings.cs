// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

        static IList<object[]> GetTransportSettings()
        {
            IList<object[]> transportSettings = new List<object[]>
            {
                new object[] { AmqpTransportSettings },
                new object[] { MqttTransportSettings },
            };

            if (bool.TryParse(ConfigHelper.TestConfig["enableWebSocketsTests"], out bool enableWebSocketsTests) && enableWebSocketsTests)
            {
                transportSettings.Add(new object[] { MqttWebSocketsTransportSettings });
                transportSettings.Add(new object[] { AmqpWebSocketsTransportSettings });
            }

            return transportSettings;
        }
    }
}
